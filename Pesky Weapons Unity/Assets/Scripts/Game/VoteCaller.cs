using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pesky.Game
{
    /// <summary>
    /// V: call a vote (docs/VOTING.md). Everybody has it. The key counts only while the pointer is locked to
    /// the game and no overlay owns the input (the same gate as the curses), so it does nothing while paused.
    /// The local pre-check mirrors the host's rules (a call left, the group cooldown, the run timer running
    /// for voteNoVoteBeforeSeconds, not banished, not paused) and is what the VOTE slot of the ability bar
    /// shows: the seconds of a cooldown, or a word saying why not (USED, WAIT, PAUSED, OUT). The host decides;
    /// a refusal comes back to this peer alone (VoteRefused) as a quiet line. In the tutorial's practice hall
    /// the call names the practice dummy as the only candidate (VoteTarget.Dummy).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoteCaller : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] WorldAuthority authority;
        [SerializeField] SessionRunner sessionRunner;
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("Shows the VOTE slot's cooldown / lock and the refusal line. Empty in a scene without a run HUD.")]
        [OptionalRef][SerializeField] RunHud runHud;
        [Tooltip("The numbers when the session has no GameData (opening the scene straight from the Editor).")]
        [OptionalRef][SerializeField] RunDef fallbackDef;

        [Header("Input")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [SerializeField] string voteAction = "Vote";

        InputAction _vote;

        enum Block { None, NoRound, Banished, Paused, Used, Cooldown, TooEarly, NoDummy }

        RunDef Def
        {
            get
            {
                GameData data = authority != null && authority.Session != null ? authority.Session.Data : null;
                if (data != null && data.run != null) return data.run;
                return fallbackDef;
            }
        }

        /// <summary>The pointer belongs to the game: a key here is a key in the world, not in a menu.</summary>
        bool PointerInWorld
        {
            get { return orbitCamera != null && orbitCamera.InputEnabled && orbitCamera.PointerLocked; }
        }

        bool Practice { get { return authority != null && authority.PracticeDummy != null; } }

        void OnEnable()
        {
            if (controls != null)
            {
                InputActionMap map = controls.FindActionMap(actionMap, false);
                _vote = map != null ? map.FindAction(voteAction, false) : null;
                if (_vote != null) _vote.Enable();
            }
            if (authority != null) authority.VoteRefused += OnRefused;
        }

        void OnDisable()
        {
            if (authority != null) authority.VoteRefused -= OnRefused;
        }

        void Update()
        {
            float wait;
            Block block = WhyNot(out wait);
            RefreshSlot(block, wait);
            if (_vote == null || !_vote.WasPressedThisFrame() || !PointerInWorld) return;
            if (runHud != null && !runHud.IsAwake) return;
            if (block != Block.None)
            {
                Note(Text(block, wait));
                DebugGate.Log("vote: refused locally - " + block);
                return;
            }
            if (authority == null || !authority.RequestVoteCall(Practice ? VoteTarget.Dummy : VoteTarget.None)) return;
            if (runHud != null) runHud.PulseAbility(AbilityCooldownSource.Vote, CurseKind.None);
            DebugGate.Log("vote: call requested" + (Practice ? " (practice)" : ""));
        }

        // ---------------------------------------------------------------- the local pre-check

        /// <summary>The same rules the host applies, read from this peer's sim. None = V may be pressed. wait = seconds until a timed block lifts (0 when unknown).</summary>
        Block WhyNot(out float wait)
        {
            wait = 0f;
            NetSession session = authority != null ? authority.Session : null;
            WorldSim sim = session != null ? session.Sim : null;
            if (sim == null || sim.Phase != SessionPhase.Playing) return Block.NoRound;
            VoteState vote = sim.Vote;
            if (authority.LocalIsGhost) return Block.Banished;
            if (authority.IsPaused || vote.MeetingOpen) return Block.Paused;
            if (Practice) return authority.PracticeDummy.isActiveAndEnabled ? Block.None : Block.NoDummy;

            RunDef def = Def;
            int calls = def != null ? def.voteCallsPerPlayer : 1;
            if (vote.CallsUsed(authority.LocalSlot) >= calls) return Block.Used;
            long gameMs = sessionRunner != null ? sessionRunner.GameMs : 0L;
            if (vote.HasResult)
            {
                long readyMs = Protocol.Tick.ToMs(vote.LastEndGameTick) + (long)((def != null ? def.voteGroupCooldown : 45f) * 1000f);
                if (gameMs < readyMs)
                {
                    wait = (readyMs - gameMs) / 1000f;
                    return Block.Cooldown;
                }
            }
            RunState run = sim.Run;
            if (!run.Started) return Block.TooEarly;
            long earlyMs = Protocol.Tick.ToMs(run.StartTick) + (long)((def != null ? def.voteNoVoteBeforeSeconds : 30f) * 1000f);
            if (gameMs < earlyMs)
            {
                wait = (earlyMs - gameMs) / 1000f;
                return Block.TooEarly;
            }
            return Block.None;
        }

        /// <summary>The VOTE slot: a cooldown with seconds for the timed blocks, a word for the rest, nothing when V is live.</summary>
        void RefreshSlot(Block block, float wait)
        {
            if (runHud == null) return;
            RunDef def = Def;
            if (block == Block.Cooldown || (block == Block.TooEarly && wait > 0f))
            {
                float total = block == Block.Cooldown ? (def != null ? def.voteGroupCooldown : 45f) : (def != null ? def.voteNoVoteBeforeSeconds : 30f);
                runHud.SetCooldown(AbilityCooldownSource.Vote, Time.time + wait, Mathf.Max(total, wait));
                runHud.SetAbilityLock(AbilityCooldownSource.Vote, "");
                return;
            }
            runHud.SetCooldown(AbilityCooldownSource.Vote, 0f, 0f);
            runHud.SetAbilityLock(AbilityCooldownSource.Vote, LockText(block));
        }

        static string LockText(Block block)
        {
            switch (block)
            {
                case Block.Used: return "USED";
                case Block.TooEarly: return "WAIT";
                case Block.Paused: return "PAUSED";
                case Block.Banished: return "OUT";
                case Block.NoDummy: return "GONE";
                default: return "";
            }
        }

        static string Text(Block block, float wait)
        {
            switch (block)
            {
                case Block.Used: return "your vote call is used up";
                case Block.Cooldown: return "the group cannot vote again for " + Mathf.CeilToInt(Mathf.Max(0.1f, wait)) + " s";
                case Block.TooEarly: return wait > 0f ? "no votes for another " + Mathf.CeilToInt(wait) + " s" : "no votes before the timer starts";
                case Block.Paused: return "a vote is already running";
                case Block.Banished: return "the banished cannot call a vote";
                case Block.NoDummy: return "the dummy is gone - re-enter the hall";
                default: return "no vote now";
            }
        }

        void OnRefused(VoteRefusal reason, float secondsLeft)
        {
            string text;
            switch (reason)
            {
                case VoteRefusal.NoCallsLeft: text = "your vote call is used up"; break;
                case VoteRefusal.GroupCooldown: text = "the group cannot vote again for " + Mathf.CeilToInt(Mathf.Max(0.1f, secondsLeft)) + " s"; break;
                case VoteRefusal.TooEarly: text = secondsLeft > 0f ? "no votes for another " + Mathf.CeilToInt(secondsLeft) + " s" : "no votes before the timer starts"; break;
                case VoteRefusal.AlreadyPaused: text = "a vote is already running"; break;
                case VoteRefusal.Banished: text = "the banished cannot vote"; break;
                case VoteRefusal.NoTarget: text = "nothing to vote on here"; break;
                case VoteRefusal.NoMeeting:
                case VoteRefusal.NotEligible:
                case VoteRefusal.AlreadyVoted:
                case VoteRefusal.BadTarget:
                    return; // a cast's refusal is the vote screen's business
                default: text = "no vote now"; break;
            }
            Note(text);
        }

        void Note(string text)
        {
            if (runHud != null) runHud.ShowNote(text);
        }
    }
}
