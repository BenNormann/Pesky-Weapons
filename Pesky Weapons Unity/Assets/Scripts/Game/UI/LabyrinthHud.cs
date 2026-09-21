using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The labyrinth's own HUD layer, above the weapon HUD: the COMPASS, the TAB OVERLAY, the role reveal
    /// at round start and the round-result banner.
    ///
    /// THE TAB OVERLAY. Holding Tab opens it. For a weapon it is ONE page: the team's shared SCRATCH PAD, a
    /// blank dark canvas with nothing of the labyrinth on it at all - whatever the crew works out, they draw
    /// themselves. For a Mage it has two top tabs: MAP (his interactive grid, the drag that swaps two rooms
    /// and the chips that bend a compass) and PAD (the same shared pad everybody else has).
    ///
    /// It is a pure view. The only shared state it ever touches is through WorldAuthority's requests (swap a
    /// pair of rooms, bend a compass, add a pad stroke, and on the host wipe the pad), and the host refuses
    /// the first two in silence.
    ///
    /// SECRETS. This peer's role lives in its own sim and nowhere else; the reveal is shown to this player
    /// alone, the MAP tab is not even built for a weapon, and nothing Mage-specific is ever drawn while the
    /// overlay is closed - the whole overlay is display:none, so it cannot even be picked. The compass reads
    /// CompassModel, which cannot tell a bent reading from a true one, and neither can this.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class LabyrinthHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] UIDocument document;
        [Tooltip("Applied at runtime as well, so the page is styled even if the UXML's Style tag is lost.")]
        [SerializeField] StyleSheet style;
        [SerializeField] WorldAuthority authority;
        [SerializeField] SessionRunner sessionRunner;
        [SerializeField] LabyrinthDirector labyrinth;
        [SerializeField] CompassModel compass;
        [Tooltip("The compass spikes are drawn relative to where the camera is looking.")]
        [SerializeField] OrbitCamera orbitCamera;

        [Header("Input")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [Tooltip("Held down to open the overlay. Bound to Tab in the action asset.")]
        [SerializeField] string mapActionName = "Map";

        [Header("Timing")]
        [Tooltip("How long after the layout arrives the role reveal appears, so a ROLE_ASSIGN can land first.")]
        [SerializeField] float roleRevealDelay = 1.5f;
        [SerializeField] float roleRevealSeconds = 6f;

        [Header("Text")]
        [SerializeField] string weaponTitle = "YOU ARE A WEAPON";
        [SerializeField] string weaponSub = "find the way out. one of you is not helping.";
        [SerializeField] string mageTitle = "YOU ARE A FRAGMENT OF THE ARCH MAGE";
        [SerializeField] string mageSub = "hold TAB: drag a room to move it, bend a compass. never all of them.";
        [SerializeField] string escapedTitle = "THE WEAPONS ESCAPED";
        [SerializeField] string resurrectedTitle = "THE ARCH MAGE WINS";

        [Header("Tutorial")]
        [Tooltip("Draw nothing at all until Wake() is called. The tutorial's Mage room wakes it; a real round leaves this off.")]
        [SerializeField] bool startAsleep;
        [Tooltip("A stand-in crew member on the Mage bar, so a solo tutorial has somebody to bend. Empty in a real round.")]
        [SerializeField] string practiceChipName = "";

        // the page
        VisualElement _compassPanel, _compassDial, _overlay, _rolePanel, _resultPanel;
        VisualElement _tabBar, _mapPage, _padPage;
        Button _tabMap, _tabPad;
        Label _roleTitle, _roleSub, _resultTitle, _resultSub, _mapTitle;
        LabyrinthMapView _map;
        ScratchPadView _pad;
        CompassView _compassView;

        InputAction _mapAction;
        NetSession _session;

        bool _overlayOpen;
        bool _padTab;
        bool _pointerFreed;
        bool _roleShown;
        float _gridSeenAt = -1f;
        float _roleUntil = -1f;
        bool _roundOver;
        bool _awake = true;

        // local cooldown guides. The host owns the real ones and never reports them.
        float _swapReadyAt;
        float _bendReadyAt;
        float _swapSentAt = -1f;
        int _swapSentA = -1, _swapSentB = -1;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            _session = sessionRunner != null ? sessionRunner.Session : null;
        }

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);

            // Everything below is queried once, here, off that root.
            _compassPanel = root.Q<VisualElement>("compass");
            _compassDial = root.Q<VisualElement>("compass-dial");
            _overlay = root.Q<VisualElement>("map-overlay");
            _tabBar = root.Q<VisualElement>("tab-bar");
            _tabMap = root.Q<Button>("tab-map");
            _tabPad = root.Q<Button>("tab-pad");
            _mapPage = root.Q<VisualElement>("map-page");
            _padPage = root.Q<VisualElement>("pad-page");
            _mapTitle = root.Q<Label>("map-title");
            _rolePanel = root.Q<VisualElement>("role-reveal");
            _roleTitle = root.Q<Label>("role-title");
            _roleSub = root.Q<Label>("role-sub");
            _resultPanel = root.Q<VisualElement>("result-banner");
            _resultTitle = root.Q<Label>("result-title");
            _resultSub = root.Q<Label>("result-sub");

            if (_compassDial != null) _compassView = new CompassView(_compassDial);

            if (_overlay != null)
            {
                _map = new LabyrinthMapView(_overlay);
                _map.SwapRequested = OnSwapRequested;
                _map.BendRequested = OnBendRequested;
                _map.PracticeName = practiceChipName;
            }

            if (_padPage != null)
            {
                _pad = new ScratchPadView(_padPage);
                _pad.SendStroke = OnPadStrokeDrawn;
                _pad.ClearRequested = OnPadClearPressed;
            }

            if (_tabMap != null) _tabMap.clicked += delegate { SetTab(false); };
            if (_tabPad != null) _tabPad.clicked += delegate { SetTab(true); };

            Show(_overlay, false);
            Show(_rolePanel, false);
            Show(_resultPanel, false);
            Show(_compassPanel, false);
            _awake = !startAsleep;

            if (controls != null)
            {
                InputActionMap map = controls.FindActionMap(actionMap, false);
                _mapAction = map != null ? map.FindAction(mapActionName, false) : null;
                if (_mapAction != null) _mapAction.Enable();
            }

            if (authority != null)
            {
                authority.RoleLearned += OnRoleLearned;
                authority.RoundEnded += OnRoundEnded;
                authority.RoomsSwapped += OnRoomsSwapped;
                authority.PadStroked += OnPadStroked;
                authority.PadCleared += OnPadCleared;
            }
        }

        void OnDisable()
        {
            if (authority != null)
            {
                authority.RoleLearned -= OnRoleLearned;
                authority.RoundEnded -= OnRoundEnded;
                authority.RoomsSwapped -= OnRoomsSwapped;
                authority.PadStroked -= OnPadStroked;
                authority.PadCleared -= OnPadCleared;
            }
            if (_overlayOpen) SetOverlayOpen(false);
        }

        void Update()
        {
            if (_session == null && sessionRunner != null) _session = sessionRunner.Session;
            if (!_awake) return;

            bool want = !_roundOver && _mapAction != null && _mapAction.IsPressed();
            if (want != _overlayOpen) SetOverlayOpen(want);

            RefreshCompass();
            RefreshRole();
            if (_overlayOpen) RefreshOverlay();
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        LabyrinthGrid Grid { get { return labyrinth != null ? labyrinth.Grid : null; } }

        LabyrinthDef Def { get { return labyrinth != null ? labyrinth.Def : null; } }

        bool IsMage
        {
            get { return authority != null && authority.LocalRole == LabyrinthRole.Mage; }
        }

        byte LocalSlot
        {
            get { return sessionRunner != null ? sessionRunner.LocalSlot : Wire.NoSlot; }
        }

        // ---------------------------------------------------------------- the compass

        /// <summary>
        /// A small ring with a GREEN spike towards the doorway to take, and on a Mage's machine a RED one
        /// towards the Resurrection Room. A spike spins while the player is inside its target room. Nothing
        /// else is drawn: no letters, no room name, no distance.
        /// </summary>
        void RefreshCompass()
        {
            if (_compassPanel == null || _compassView == null) return;
            bool has = compass != null && compass.HasReading;
            bool red = compass != null && compass.HasRedReading;
            Show(_compassPanel, (has || red) && !_roundOver);
            if (!has && !red) return;

            float cameraYaw = orbitCamera != null ? orbitCamera.Yaw : 0f;
            float greenAngle = has ? ScreenAngle(compass.WorldDirection, cameraYaw) : 0f;
            float redAngle = red ? ScreenAngle(compass.RedWorldDirection, cameraYaw) : 0f;
            _compassView.Set(has, greenAngle, red, redAngle);
        }

        /// <summary>A flat world direction as degrees on screen: 0 is straight ahead, positive is clockwise.</summary>
        static float ScreenAngle(Vector3 worldDirection, float cameraYaw)
        {
            float worldAngle = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(cameraYaw, worldAngle);
        }

        // ---------------------------------------------------------------- the overlay

        void SetOverlayOpen(bool open)
        {
            _overlayOpen = open;
            Show(_overlay, open);
            if (open)
            {
                // A weapon has no map at all: its overlay IS the pad.
                if (!IsMage) _padTab = true;
                ApplyTab();
                // The cursor is free for everybody now, because everybody draws on the pad with the mouse.
                if (orbitCamera != null && !_pointerFreed)
                {
                    orbitCamera.InputEnabled = false;
                    orbitCamera.FreePointer();
                    _pointerFreed = true;
                }
                RefreshOverlay();
                return;
            }

            if (_map != null) _map.ClearSelection();
            if (_pad != null) _pad.EndStroke();
            if (_pointerFreed && orbitCamera != null)
            {
                orbitCamera.InputEnabled = true;
                orbitCamera.LockPointer();
            }
            _pointerFreed = false;
        }

        void SetTab(bool pad)
        {
            if (!IsMage) pad = true;
            if (_padTab == pad) return;
            if (!pad && _pad != null) _pad.EndStroke();
            _padTab = pad;
            ApplyTab();
        }

        void ApplyTab()
        {
            bool mage = IsMage;
            // The tab bar only exists when there is a second page to reach.
            Show(_tabBar, mage);
            Show(_mapPage, mage && !_padTab);
            Show(_padPage, !mage || _padTab);
            if (_tabMap != null) _tabMap.EnableInClassList("is-selected", mage && !_padTab);
            if (_tabPad != null) _tabPad.EnableInClassList("is-selected", !mage || _padTab);
            if (_mapTitle != null) _mapTitle.text = "THE LABYRINTH";
        }

        void RefreshOverlay()
        {
            bool mage = IsMage;
            // A weapon that is somehow left on the map tab (it never can be) is put back on the pad.
            if (!mage && !_padTab) { _padTab = true; ApplyTab(); }
            else if (mage != (_tabBar != null && _tabBar.style.display == DisplayStyle.Flex)) ApplyTab();

            if (mage && !_padTab) RefreshMap();
            else RefreshPad();
        }

        void RefreshPad()
        {
            if (_pad == null) return;
            ScratchPadState pad = authority != null ? authority.Pad : null;
            bool isHost = _session != null && _session.IsHost;
            _pad.Refresh(pad, LocalSlot, isHost);
        }

        void RefreshMap()
        {
            if (_map == null) return;
            LabyrinthDef def = Def;
            PlayerTable players = _session != null && _session.Sim != null ? _session.Sim.Players : null;
            byte localSlot = LocalSlot;
            int localCell = labyrinth != null ? labyrinth.LocalCell : LabyrinthGrid.NoCell;

            float now = Time.time;
            bool waiting = _swapSentAt > 0f && now < _swapSentAt + 1f;
            bool canSwap = !waiting && now >= _swapReadyAt;

            _map.Refresh(Grid, def, localCell, players, localSlot, true, canSwap);

            float swapSeconds = def != null ? def.swapCooldownSeconds : 60f;
            float bendSeconds = def != null ? def.bendCooldownSeconds : 15f;
            float swapLeft = swapSeconds > 0.01f ? (_swapReadyAt - now) / swapSeconds : 0f;
            float bendLeft = bendSeconds > 0.01f ? (_bendReadyAt - now) / bendSeconds : 0f;
            _map.SetCooldowns(swapLeft, swapSeconds, bendLeft, bendSeconds);
        }

        void OnSwapRequested(int cellA, int cellB)
        {
            if (authority == null) return;
            _swapSentAt = Time.time;
            _swapSentA = cellA;
            _swapSentB = cellB;
            authority.RequestRoomSwap(cellA, cellB);
        }

        void OnBendRequested(byte slotMask, CompassTargetKind kind, int cell)
        {
            if (authority == null) return;
            // The host answers the bent players alone and never this peer, so the ring is optimistic:
            // it starts on the ASK. A refusal only makes this map more cautious than the host is.
            StartBendCooldown();
            authority.RequestCompassBend(slotMask, kind, cell);
        }

        void StartBendCooldown()
        {
            LabyrinthDef def = Def;
            _bendReadyAt = Time.time + (def != null ? def.bendCooldownSeconds : 15f);
        }

        /// <summary>A swap landed. If it is the one this peer asked for, the local cooldown guide starts now.</summary>
        void OnRoomsSwapped(int cellA, int cellB)
        {
            bool mine = (cellA == _swapSentA && cellB == _swapSentB) || (cellA == _swapSentB && cellB == _swapSentA);
            if (!mine) return;
            LabyrinthDef def = Def;
            _swapReadyAt = Time.time + (def != null ? def.swapCooldownSeconds : 60f);
            _swapSentAt = -1f;
            _swapSentA = -1;
            _swapSentB = -1;
        }

        // ---------------------------------------------------------------- the scratch pad

        void OnPadStrokeDrawn(ushort[] points, bool erase, byte width)
        {
            if (authority != null) authority.RequestPadStroke(points, erase, width);
        }

        void OnPadClearPressed()
        {
            if (authority != null) authority.RequestPadClear();
        }

        /// <summary>A stroke landed. If it is this player's own, its optimistic copy can go: the sim has it now.</summary>
        void OnPadStroked(byte slot)
        {
            if (_pad != null && slot == LocalSlot) _pad.NoteOwnStrokeConfirmed();
        }

        void OnPadCleared()
        {
            if (_pad != null) _pad.Wiped();
        }

        // ---------------------------------------------------------------- role and result

        void RefreshRole()
        {
            if (Grid != null && _gridSeenAt < 0f) _gridSeenAt = Time.time;

            if (!_roleShown && _gridSeenAt >= 0f && Time.time >= _gridSeenAt + roleRevealDelay)
                ShowRole();

            if (_roleUntil > 0f && Time.time >= _roleUntil)
            {
                _roleUntil = -1f;
                Show(_rolePanel, false);
            }
        }

        void ShowRole()
        {
            // Asleep, nothing is drawn at all - not even a ROLE_ASSIGN that lands early. Wake() clears
            // _roleShown and the reveal is timed from there instead.
            if (!_awake) return;
            _roleShown = true;
            bool mage = IsMage;
            if (_roleTitle != null) _roleTitle.text = mage ? mageTitle : weaponTitle;
            if (_roleSub != null) _roleSub.text = mage ? mageSub : weaponSub;
            if (_rolePanel != null) _rolePanel.EnableInClassList("is-mage", mage);
            Show(_rolePanel, true);
            _roleUntil = Time.time + roleRevealSeconds;
        }

        /// <summary>
        /// Start drawing. Used by the tutorial, whose Mage lesson begins in the middle of the level: the
        /// compass, the overlay and the role reveal all stay blank until the player walks into that room.
        /// The reveal is timed from here, so it lands a moment after the lesson starts rather than at load.
        /// </summary>
        public void Wake()
        {
            if (_awake) return;
            _awake = true;
            _gridSeenAt = -1f;
            _roleShown = false;
        }

        /// <summary>ROLE_ASSIGN arrived: this peer is a Mage. Show it again, in case the reveal already said otherwise.</summary>
        void OnRoleLearned()
        {
            ShowRole();
            // A Mage now has a MAP tab that a weapon never sees. Start him on it.
            _padTab = false;
            if (_overlayOpen) ApplyTab();
        }

        void OnRoundEnded(RoundOutcome outcome, byte escapedMask, byte mageMask)
        {
            _roundOver = true;
            if (_overlayOpen) SetOverlayOpen(false);
            Show(_compassPanel, false);
            if (_resultTitle != null)
                _resultTitle.text = outcome == RoundOutcome.Escaped ? escapedTitle : resurrectedTitle;
            if (_resultSub != null) _resultSub.text = MageNames(mageMask);
            Show(_resultPanel, true);
        }

        /// <summary>ROUND_RESULT is the one message that ever names the fragments, so this is the one place they can be said.</summary>
        string MageNames(byte mageMask)
        {
            PlayerTable players = _session != null && _session.Sim != null ? _session.Sim.Players : null;
            if (players == null) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < players.Count && i < 8; i++)
            {
                if ((mageMask & (1 << i)) == 0) continue;
                PlayerState p = players[i];
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(p != null && !string.IsNullOrEmpty(p.name) ? p.name : "P" + i);
            }
            if (sb.Length == 0) return "no fragment was drawn";
            sb.Insert(0, "the arch mage was:   ");
            return sb.ToString();
        }
    }
}
