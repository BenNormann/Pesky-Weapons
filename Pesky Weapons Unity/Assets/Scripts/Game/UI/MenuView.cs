using System;
using Pesky.Protocol;
using Pesky.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The menu's two pages, and nothing else. TITLE: the game title, the callsign field, HOST,
    /// JOIN with the code field beside it, TUTORIAL, a status line and QUIT. ROOM: the room code
    /// big with COPY, the crew list (slot colour, callsign, HOST and YOU markers, the count out of
    /// the maximum), the labyrinth line, START for the host and LEAVE.
    ///
    /// It owns no session, loads no scene and knows no rules: it queries Menu.uxml by name, builds
    /// the eight crew rows once, and raises plain events that MenuFlow answers.
    /// </summary>
    public sealed class MenuView
    {
        public const int MaxCallsignLength = 16;
        public const int SlotCount = Wire.MaxPlayers;
        const string ErrorClass = "status--error";

        public event Action HostClicked;
        public event Action JoinClicked;
        public event Action TutorialClicked;
        public event Action StartClicked;
        public event Action LeaveClicked;
        public event Action CopyClicked;
        public event Action QuitClicked;

        struct CrewRow
        {
            public VisualElement root;
            public VisualElement chip;
            public Label name;
            public Label host;
            public Label you;
            public Label open;
        }

        readonly CrewRow[] _crew = new CrewRow[SlotCount];

        VisualElement _root;
        VisualElement _titlePage;
        VisualElement _roomPage;
        TextField _callsign;
        TextField _code;
        Button _host;
        Button _join;
        Button _tutorial;
        Button _start;
        Button _leave;
        Button _copy;
        Button _quit;
        Label _titleStatus;
        Label _roomStatus;
        Label _joinHint;
        Label _roomCode;
        Label _roomAddresses;
        Label _crewHeader;
        Label _labyrinth;
        Label _wait;
        bool _built;

        /// <summary>True while the room page is the one on screen; the status line follows it.</summary>
        public bool IsRoomShowing { get; private set; }

        public string Callsign { get { return _callsign != null && _callsign.value != null ? _callsign.value : ""; } }

        public string JoinCode { get { return _code != null && _code.value != null ? _code.value : ""; } }

        // ---- building ----

        public void Build(VisualElement root)
        {
            if (_built || root == null) return;
            _built = true;
            _root = root;

            _titlePage = root.Q<VisualElement>("title-page");
            _roomPage = root.Q<VisualElement>("room-page");
            _callsign = root.Q<TextField>("callsign-field");
            _code = root.Q<TextField>("code-field");
            _joinHint = root.Q<Label>("join-hint");
            _titleStatus = root.Q<Label>("title-status");
            _roomStatus = root.Q<Label>("room-status");
            _roomCode = root.Q<Label>("room-code");
            _roomAddresses = root.Q<Label>("room-addresses");
            _crewHeader = root.Q<Label>("crew-header");
            _labyrinth = root.Q<Label>("labyrinth-line");
            _wait = root.Q<Label>("wait-label");

            _host = Hook(root, "host-button", RaiseHost);
            _join = Hook(root, "join-button", RaiseJoin);
            _tutorial = Hook(root, "tutorial-button", RaiseTutorial);
            _start = Hook(root, "start-button", RaiseStart);
            _leave = Hook(root, "leave-button", RaiseLeave);
            _copy = Hook(root, "copy-button", RaiseCopy);
            _quit = Hook(root, "quit-button", RaiseQuit);

            if (_callsign != null)
            {
                _callsign.maxLength = MaxCallsignLength;
                _callsign.RegisterCallback<KeyDownEvent>(OnCallsignKeyDown);
            }
            if (_code != null)
            {
                _code.maxLength = RoomCodes.IsAddressBased ? RoomCodes.MaxAddressLength : RoomCodes.Length;
                if (!RoomCodes.IsAddressBased) _code.RegisterValueChangedCallback(OnCodeChanged);
                _code.RegisterCallback<KeyDownEvent>(OnCodeKeyDown);
            }

            VisualElement rows = root.Q<VisualElement>("crew-rows");
            for (int i = 0; i < SlotCount; i++)
            {
                CrewRow row = new CrewRow();
                row.root = NewDiv(rows, "crew-row");
                row.chip = NewDiv(row.root, "slot-chip");
                row.name = NewLabel(row.root, "", "crew-name");
                row.open = NewLabel(row.root, "OPEN", "crew-open");
                row.host = NewLabel(row.root, "HOST", "badge");
                row.you = NewLabel(row.root, "YOU", "badge badge--you");
                _crew[i] = row;
            }

            Show(_titlePage, false);
            Show(_roomPage, false);
        }

        // ---- pages ----

        public void ShowTitle(string status = "", bool error = false)
        {
            IsRoomShowing = false;
            Show(_root, true);
            Show(_roomPage, false);
            Show(_titlePage, true);
            SetBusy(false);
            SetStatus(status, error);
            FocusSoon(_callsign != null && Callsign.Length == 0 ? (VisualElement)_callsign : _host);
        }

        /// <summary>
        /// The room page. canStart shows START (the host, while the room is still in the lobby);
        /// waiting shows the joiner's "waiting for the host" line instead.
        /// </summary>
        public void ShowRoom(string code, string alternatives, bool canStart, bool waiting)
        {
            IsRoomShowing = true;
            Show(_root, true);
            Show(_titlePage, false);
            Show(_roomPage, true);
            if (_roomCode != null) _roomCode.text = code ?? "";
            if (_roomAddresses != null)
            {
                _roomAddresses.text = alternatives ?? "";
                Show(_roomAddresses, !string.IsNullOrEmpty(alternatives));
            }
            Show(_start, canStart);
            Show(_wait, waiting);
            SetStatus("", false);
            FocusSoon(canStart ? (VisualElement)_start : _leave);
        }

        /// <summary>The status line of whichever page is showing.</summary>
        public void SetStatus(string status, bool error = false)
        {
            Label label = IsRoomShowing ? _roomStatus : _titleStatus;
            if (label == null) return;
            label.text = status ?? "";
            label.EnableInClassList(ErrorClass, error);
        }

        /// <summary>While a host or join is in flight the title page's controls are dead.</summary>
        public void SetBusy(bool busy)
        {
            SetEnabled(_host, !busy);
            SetEnabled(_join, !busy);
            SetEnabled(_tutorial, !busy);
            SetEnabled(_callsign, !busy);
            SetEnabled(_code, !busy);
        }

        public void SetCallsign(string name)
        {
            if (_callsign != null) _callsign.SetValueWithoutNotify(name ?? "");
        }

        public void SetJoinHint(string hint)
        {
            if (_joinHint != null) _joinHint.text = hint ?? "";
        }

        public void SetLabyrinth(string line)
        {
            if (_labyrinth != null) _labyrinth.text = line ?? "";
        }

        public void SetStartEnabled(bool on)
        {
            SetEnabled(_start, on);
        }

        public void ShowQuit(bool on)
        {
            Show(_quit, on);
        }

        /// <summary>One row per slot: the slot colour, the callsign, HOST, YOU, or OPEN.</summary>
        public void SetCrew(PeerSlots slots, byte localSlot)
        {
            if (slots == null) return;
            if (_crewHeader != null) _crewHeader.text = "CREW   " + slots.Count + " / " + SlotCount;
            for (byte i = 0; i < SlotCount; i++)
            {
                CrewRow row = _crew[i];
                if (row.root == null) continue;
                bool present = slots.IsPresent(i);
                Show(row.chip, present);
                Show(row.name, present);
                Show(row.open, !present);
                Show(row.host, present && slots.IsHostSlot(i));
                Show(row.you, present && i == localSlot);
                row.root.EnableInClassList("crew-row--open", !present);
                if (!present) continue;
                row.chip.style.backgroundColor = SlotColors.For(i);
                string name = slots.NameOf(i);
                row.name.text = string.IsNullOrEmpty(name) ? "?" : name;
            }
        }

        // ---- input ----

        void OnCallsignKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            RaiseHost();
        }

        void OnCodeKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            RaiseJoin();
        }

        /// <summary>Browser rooms only: upper-cases the code as it is typed and drops anything outside the alphabet.</summary>
        void OnCodeChanged(ChangeEvent<string> e)
        {
            string raw = e.newValue ?? "";
            string clean = RoomCodes.Normalise(raw);
            bool allAllowed = true;
            for (int i = 0; i < clean.Length; i++)
            {
                if (RoomCodes.IsAllowed(clean[i])) continue;
                allAllowed = false;
                break;
            }
            if (!allAllowed)
            {
                char[] chars = new char[clean.Length];
                int n = 0;
                for (int i = 0; i < clean.Length; i++)
                {
                    if (RoomCodes.IsAllowed(clean[i])) chars[n++] = clean[i];
                }
                clean = new string(chars, 0, n);
            }
            if (clean != raw) _code.SetValueWithoutNotify(clean);
        }

        // ---- helpers ----

        void RaiseHost() { if (HostClicked != null) HostClicked(); }
        void RaiseJoin() { if (JoinClicked != null) JoinClicked(); }
        void RaiseTutorial() { if (TutorialClicked != null) TutorialClicked(); }
        void RaiseStart() { if (StartClicked != null) StartClicked(); }
        void RaiseLeave() { if (LeaveClicked != null) LeaveClicked(); }
        void RaiseCopy() { if (CopyClicked != null) CopyClicked(); }
        void RaiseQuit() { if (QuitClicked != null) QuitClicked(); }

        static Button Hook(VisualElement root, string name, Action clicked)
        {
            Button button = root.Q<Button>(name);
            if (button != null && clicked != null) button.clicked += clicked;
            return button;
        }

        /// <summary>Focus after the page has a layout, so keyboard and gamepad start somewhere sensible.</summary>
        void FocusSoon(VisualElement element)
        {
            if (_root == null || element == null) return;
            _root.schedule.Execute(() =>
            {
                if (element.panel != null && element.resolvedStyle.display != DisplayStyle.None) element.Focus();
            }).StartingIn(1);
        }

        static VisualElement NewDiv(VisualElement parent, string classes)
        {
            VisualElement element = new VisualElement();
            AddClasses(element, classes);
            if (parent != null) parent.Add(element);
            return element;
        }

        static Label NewLabel(VisualElement parent, string text, string classes)
        {
            Label label = new Label(text);
            AddClasses(label, classes);
            if (parent != null) parent.Add(label);
            return label;
        }

        static void AddClasses(VisualElement element, string classes)
        {
            if (element == null || string.IsNullOrEmpty(classes)) return;
            string[] parts = classes.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0) element.AddToClassList(parts[i]);
            }
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetEnabled(VisualElement element, bool on)
        {
            if (element != null) element.SetEnabled(on);
        }
    }
}
