using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The end of a run on this machine (docs/RUN.md 4.3): a full-screen sheet in Run.uxml (end-root) on the
    /// same UIDocument as RunHud and VoteScreen. ROUND_RESULT opens it on every peer at the same tick (the host's
    /// RunRule.Finish emits it, then SESSION_END). It says VICTORY or GAME OVER for THIS player's side: the Mage
    /// wins on TimedOut / WeaponsGone, the weapons on Escaped; the side is this slot's bit in mageMask, the one
    /// message that ever names the Mages, so a banished player still sees the result of the side the host drew.
    /// Then the reason, the Mage's name(s), and BACK TO THE ROOM.
    ///
    /// While it is open PauseGate treats the game as paused on this machine (input off through
    /// OrbitCamera.InputEnabled, the local body frozen, the cursor free and owned by this screen through
    /// PauseGate.PointerFree), so Escape may still open and close the settings screen on top without taking
    /// the input back. SessionRunner holds the trip back to the menu (HoldReturnOnEnd) until the button; the
    /// menu's room page then takes the host to the lobby and START plays again. The shared vote pause state is
    /// NOT used: SESSION_END resets it on the same tick (WorldSim.SetPhase).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class RunEndScreen : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] UIDocument document;
        [SerializeField] WorldAuthority authority;
        [SerializeField] SessionRunner sessionRunner;
        [Tooltip("FreePointer on open. PauseGate keeps the input off and the cursor free while IsOpen.")]
        [SerializeField] OrbitCamera orbitCamera;

        [Header("Text")]
        [SerializeField] string victoryTitle = "VICTORY";
        [SerializeField] string defeatTitle = "GAME OVER";
        [SerializeField] string escapedReason = "the weapons escaped";
        [SerializeField] string timedOutReason = "the timer ran out: the arch mage wins";
        [SerializeField] string weaponsGoneReason = "no weapon is left standing: the arch mage wins";
        [SerializeField] string otherMageReason = "the arch mage wins";
        [SerializeField] string magePrefix = "the arch mage was:   ";
        [SerializeField] string noMage = "no fragment was drawn";

        VisualElement _root, _banner;
        Label _title, _reason, _mage;
        Button _button;
        bool _open;

        /// <summary>True while the sheet is up: PauseGate freezes the game on this machine and leaves the cursor to it.</summary>
        public bool IsOpen { get { return _open; } }

        NetSession Session
        {
            get { return authority != null ? authority.Session : (sessionRunner != null ? sessionRunner.Session : null); }
        }

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            _root = root != null ? root.Q<VisualElement>("end-root") : null;
            if (_root == null)
            {
                Debug.LogError("RunEndScreen: the document has no 'end-root' (Run.uxml).", this);
                enabled = false;
                return;
            }
            _title = _root.Q<Label>("end-title");
            _reason = _root.Q<Label>("end-reason");
            _mage = _root.Q<Label>("end-mage");
            _button = _root.Q<Button>("end-button");
            // RunHud's one-line banner rode the same event; this sheet replaces it.
            _banner = root.Q<VisualElement>("result-banner");
            if (_button != null) _button.clicked += OnBack;
            Show(_root, false);
            _open = false;
            if (authority != null) authority.RoundEnded += OnRoundEnded;
            // From here on the Ended phase waits for the button instead of loading the menu by itself.
            if (sessionRunner != null) sessionRunner.HoldReturnOnEnd = true;
        }

        void OnDisable()
        {
            if (authority != null) authority.RoundEnded -= OnRoundEnded;
            if (_button != null) _button.clicked -= OnBack;
            if (sessionRunner != null) sessionRunner.HoldReturnOnEnd = false;
            _open = false;
        }

        void Update()
        {
            if (_open) return;
            // A peer that lands with the round already decided (a late resync) never sees the event: read the sim.
            NetSession session = Session;
            WorldSim sim = session != null ? session.Sim : null;
            if (sim == null || sim.Phase != SessionPhase.Ended) return;
            LabyrinthState lab = sim.Labyrinth;
            if (lab == null || lab.Outcome == RoundOutcome.None) return;
            OnRoundEnded(lab.Outcome, lab.EscapedMask, lab.MageMask);
        }

        // ---------------------------------------------------------------- the result

        void OnRoundEnded(RoundOutcome outcome, byte escapedMask, byte mageMask)
        {
            NetSession session = Session;
            WorldSim sim = session != null ? session.Sim : null;
            byte local = session != null ? session.LocalSlot : Wire.NoSlot;
            bool mageWon = outcome != RoundOutcome.Escaped;
            // This player's side as the host drew it, from the one message that names it. A banished Mage or
            // weapon (a ghost) is still on that side. A peer with no slot falls back to its own secret role.
            bool isMage = local < Wire.MaxPlayers
                ? (mageMask & (1 << local)) != 0
                : (authority != null && authority.LocalRole == LabyrinthRole.Mage);
            bool won = isMage == mageWon;

            if (_title != null) _title.text = won ? victoryTitle : defeatTitle;
            if (_root != null) _root.EnableInClassList("is-victory", won);
            if (_reason != null)
            {
                string reason = otherMageReason;
                if (outcome == RoundOutcome.Escaped) reason = escapedReason;
                else if (outcome == RoundOutcome.TimedOut) reason = timedOutReason;
                else if (outcome == RoundOutcome.WeaponsGone) reason = weaponsGoneReason;
                _reason.text = reason;
            }
            if (_mage != null) _mage.text = MageNames(sim, mageMask);
            Open();
        }

        void Open()
        {
            if (_open || _root == null) return;
            _open = true;
            if (orbitCamera != null) orbitCamera.FreePointer();
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            Show(_banner, false);
            Show(_root, true);
            DebugGate.Log("run: end screen opened");
        }

        /// <summary>BACK TO THE ROOM: the menu, with the session alive (SessionRunner.ReturnAfterRun). A click, never mid-drain.</summary>
        void OnBack()
        {
            if (!_open || sessionRunner == null) return;
            DebugGate.Log("run: end screen, back to the room");
            sessionRunner.ReturnAfterRun();
        }

        /// <summary>The same words as RunHud.MageNames: ROUND_RESULT is the one message that ever names the fragments.</summary>
        string MageNames(WorldSim sim, byte mageMask)
        {
            PlayerTable players = sim != null ? sim.Players : null;
            if (players == null) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < players.Count && i < Wire.MaxPlayers; i++)
            {
                if ((mageMask & (1 << i)) == 0) continue;
                PlayerState p = players[i];
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(p != null && !string.IsNullOrEmpty(p.name) ? p.name : "P" + i);
            }
            if (sb.Length == 0) return noMage;
            sb.Insert(0, magePrefix);
            return sb.ToString();
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
