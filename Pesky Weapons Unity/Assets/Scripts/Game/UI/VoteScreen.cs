using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The vote meeting on this machine (docs/VOTING.md): the Among Us style screen in Run.uxml (vote-root),
    /// on the same UIDocument as RunHud. VOTE_START (or a snapshot that says a meeting is open) opens it and
    /// frees the cursor; the pause has already taken the input (PauseGate), so nothing in the world reacts
    /// to the clicks. Click a name or SKIP VOTE, then CONFIRM: one final vote (VOTE_CAST_REQ); the choice
    /// locks once the host's VOTE_TALLY lands, and every tally shows as the voter's chip beside the candidate.
    /// VOTE_END replaces the grid with the result ("NAME WAS BANISHED - they were THE ARCH MAGE / A WEAPON",
    /// or "NO ONE WAS BANISHED"); PAUSE_END, voteResultSeconds later, closes the screen, and PauseGate gives
    /// the pointer lock and the input back. A ghost or a late joiner sees the meeting read-only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class VoteScreen : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] UIDocument document;
        [SerializeField] WorldAuthority authority;
        [SerializeField] SessionRunner sessionRunner;
        [Tooltip("FreePointer on open. The pause owns InputEnabled; PauseGate re-locks the pointer on PAUSE_END.")]
        [SerializeField] OrbitCamera orbitCamera;

        [Header("Text")]
        [SerializeField] string calledByPrefix = "VOTE CALLED BY ";
        [SerializeField] string dummyName = "DUMMY";
        [SerializeField] string banishedFormat = "{0} WAS BANISHED";
        [SerializeField] string wasMage = "they were THE ARCH MAGE";
        [SerializeField] string wasWeapon = "they were A WEAPON";
        [SerializeField] string nobodyTitle = "NO ONE WAS BANISHED";
        [SerializeField] string nobodySub = "a tie, or SKIP on top: everybody stays";
        [SerializeField] string sentStatus = "your vote is in";
        [SerializeField] string watchStatus = "you are watching: you have no vote in this meeting";
        [SerializeField] string refusedStatus = "the host refused that vote";

        readonly List<VoteView.Candidate> _candidates = new List<VoteView.Candidate>(Wire.MaxPlayers);
        readonly byte[] _voteOf = new byte[Wire.MaxPlayers];
        readonly string[] _names = new string[Wire.MaxPlayers];
        readonly Color[] _colours = new Color[Wire.MaxPlayers];

        VoteView _view;
        bool _open;
        bool _confirmed;
        byte _choice = VoteTarget.None;
        uint _seenRev;
        int _timerShown = -1;

        /// <summary>True while the meeting screen is up (the cursor is free for it).</summary>
        public bool IsOpen { get { return _open; } }

        NetSession Session { get { return authority != null ? authority.Session : (sessionRunner != null ? sessionRunner.Session : null); } }

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            for (int i = 0; i < Wire.MaxPlayers; i++) _colours[i] = SlotColors.For(i);
        }

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            _view = new VoteView(root);
            if (!_view.IsValid)
            {
                Debug.LogError("VoteScreen: the document has no 'vote-root' (Run.uxml).", this);
                enabled = false;
                return;
            }
            _view.CandidateClicked += OnCandidate;
            _view.SkipClicked += OnSkip;
            _view.ConfirmClicked += OnConfirm;
            _view.SetVisible(false);
            if (authority != null)
            {
                authority.VoteStarted += OnVoteStarted;
                authority.VoteEnded += OnVoteEnded;
                authority.PauseChanged += OnPauseChanged;
                authority.VoteRefused += OnRefused;
            }
        }

        void OnDisable()
        {
            if (authority != null)
            {
                authority.VoteStarted -= OnVoteStarted;
                authority.VoteEnded -= OnVoteEnded;
                authority.PauseChanged -= OnPauseChanged;
                authority.VoteRefused -= OnRefused;
            }
            if (_open) Close();
        }

        void Update()
        {
            NetSession session = Session;
            WorldSim sim = session != null ? session.Sim : null;
            if (sim == null) return;
            VoteState vote = sim.Vote;
            // A late joiner, or a snapshot mid-meeting: the sim says a meeting is on and nothing opened this.
            if (!_open && vote.MeetingOpen && sim.Pause.Paused && sim.Phase == SessionPhase.Playing) Open();
            if (!_open) return;
            if (sim.Phase != SessionPhase.Playing)
            {
                Close();
                return;
            }
            if (vote.MeetingOpen)
            {
                long leftMs = Protocol.Tick.ToMs(vote.DeadlineTick) - session.Clock.NowMs;
                int seconds = leftMs > 0 ? (int)((leftMs + 999) / 1000) : 0;
                if (seconds != _timerShown)
                {
                    _timerShown = seconds;
                    _view.SetTimer(seconds);
                }
            }
            if (vote.Rev != _seenRev)
            {
                _seenRev = vote.Rev;
                RefreshVotes(sim);
            }
        }

        // ---------------------------------------------------------------- open and close

        void Open()
        {
            NetSession session = Session;
            WorldSim sim = session != null ? session.Sim : null;
            if (_open || _view == null || sim == null) return;
            _open = true;
            _confirmed = false;
            _choice = VoteTarget.None;
            _timerShown = -1;
            if (orbitCamera != null) orbitCamera.FreePointer();
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }

            VoteState vote = sim.Vote;
            PlayerTable players = sim.Players;
            byte local = session.LocalSlot;
            PlayerState caller = vote.CallerSlot < Wire.MaxPlayers ? players[vote.CallerSlot] : null;
            _view.SetHeader(calledByPrefix + Name(caller, vote.CallerSlot));

            bool eligible = vote.IsEligible(local) && !vote.HasVoted(local);
            _candidates.Clear();
            if (vote.Practice)
            {
                VoteView.Candidate d = new VoteView.Candidate();
                d.target = VoteTarget.Dummy;
                d.name = dummyName;
                d.colour = new Color(0.45f, 0.47f, 0.52f);
                d.selectable = eligible;
                _candidates.Add(d);
            }
            else
            {
                for (int i = 0; i < Wire.MaxPlayers; i++)
                {
                    PlayerState p = players[i];
                    if (p == null || !p.present) continue;
                    VoteView.Candidate c = new VoteView.Candidate();
                    c.target = (byte)i;
                    c.name = Name(p, (byte)i);
                    c.colour = SlotColors.For(i);
                    c.you = i == local;
                    c.banished = vote.IsBanished(i);
                    c.selectable = eligible && vote.IsCandidate(i);
                    _candidates.Add(c);
                }
            }
            _view.ShowVoting();
            _view.BuildCandidates(_candidates);
            _view.SetInteractive(eligible);
            _view.SetSelection(VoteTarget.None, false);
            _view.SetStatus(eligible ? "" : watchStatus);
            _seenRev = vote.Rev;
            RefreshVotes(sim);
            _view.SetVisible(true);
            DebugGate.Log("vote: meeting opened, called by slot " + vote.CallerSlot + (vote.Practice ? " (practice)" : ""));
        }

        void Close()
        {
            if (!_open) return;
            _open = false;
            if (_view != null) _view.SetVisible(false);
            // The pointer and the input come back with the pause's end (PauseGate), not here.
        }

        // ---------------------------------------------------------------- the votes

        void RefreshVotes(WorldSim sim)
        {
            VoteState vote = sim.Vote;
            PlayerTable players = sim.Players;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _voteOf[i] = vote.MeetingOpen ? vote.VoteOf(i) : vote.LastTallyOf(i);
                PlayerState p = players[i];
                _names[i] = p != null && p.present ? Name(p, (byte)i) : "";
            }
            _view.SetVotes(_voteOf, _names, _colours);
            // The host's VOTE_TALLY for this peer locks the choice for good.
            NetSession session = Session;
            byte local = session != null ? session.LocalSlot : Wire.NoSlot;
            if (vote.MeetingOpen && local < Wire.MaxPlayers && vote.HasVoted(local))
            {
                _confirmed = true;
                _choice = vote.VoteOf(local);
                _view.SetSelection(_choice, true);
                _view.SetStatus(sentStatus);
            }
        }

        void OnCandidate(byte target)
        {
            if (!_open || _confirmed) return;
            _choice = target;
            _view.SetSelection(target, false);
            _view.SetStatus("");
        }

        void OnSkip()
        {
            if (!_open || _confirmed) return;
            _choice = VoteTarget.Skip;
            _view.SetSelection(VoteTarget.Skip, false);
            _view.SetStatus("");
        }

        void OnConfirm()
        {
            if (!_open || _confirmed || _choice == VoteTarget.None || authority == null) return;
            if (!authority.RequestVoteCast(_choice)) return;
            // Optimistic: locked on the ask; a refusal unlocks it.
            _confirmed = true;
            _view.SetSelection(_choice, true);
            _view.SetStatus(sentStatus);
            DebugGate.Log("vote: cast " + (_choice == VoteTarget.Skip ? "SKIP" : _choice == VoteTarget.Dummy ? "the dummy" : "slot " + _choice));
        }

        // ---------------------------------------------------------------- the authority's events

        void OnVoteStarted()
        {
            if (_open) Close();
            Open();
        }

        void OnVoteEnded(byte banished, LabyrinthRole role)
        {
            NetSession session = Session;
            WorldSim sim = session != null ? session.Sim : null;
            if (!_open && sim != null && sim.Pause.Paused) Open();
            if (!_open || _view == null) return;
            if (sim != null) RefreshVotes(sim);
            if (banished == VoteTarget.None)
            {
                _view.ShowResult(nobodyTitle, nobodySub);
                return;
            }
            string who = banished == VoteTarget.Dummy ? dummyName
                : Name(sim != null && banished < Wire.MaxPlayers ? sim.Players[banished] : null, banished);
            _view.ShowResult(string.Format(banishedFormat, who), role == LabyrinthRole.Mage ? wasMage : wasWeapon);
        }

        void OnPauseChanged(bool paused)
        {
            if (!paused) Close();
        }

        void OnRefused(VoteRefusal reason, float secondsLeft)
        {
            if (!_open) return;
            if (reason != VoteRefusal.NoMeeting && reason != VoteRefusal.NotEligible && reason != VoteRefusal.AlreadyVoted
                && reason != VoteRefusal.BadTarget && reason != VoteRefusal.Banished) return;
            // A cast that was not taken: let the player choose again, unless the host says the vote is in already.
            if (reason == VoteRefusal.AlreadyVoted) return;
            _confirmed = false;
            _view.SetSelection(_choice, false);
            _view.SetStatus(refusedStatus);
        }

        static string Name(PlayerState p, byte slot)
        {
            return p != null && !string.IsNullOrEmpty(p.name) ? p.name : "Player " + (slot + 1);
        }
    }
}
