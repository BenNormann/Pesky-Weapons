using System;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The only thing MainMenu.unity does. It owns the NetSession while the menu is on screen
    /// (pumping NetSession.Update once a frame, exactly as SessionRunner does in a level), answers
    /// the MenuView's events, and hands the session over to the next scene through GameLocator.
    ///
    ///  HOST      opens a room (a generated code in a browser, this machine's port on a desktop)
    ///            and shows the room page.
    ///  JOIN      validates what was typed, starts the handshake and shows the room page when the
    ///            host answers. A version mismatch, an unreachable host or a silence longer than
    ///            joinTimeoutSeconds each come back to the title page with a plain sentence.
    ///  TUTORIAL  starts an offline loopback session and loads the tutorial scene at once.
    ///  START     (host only) broadcasts SESSION_PHASE(Playing); every peer, host included, loads
    ///            the labyrinth from its own PhaseChanged. The scene is checked first, so a build
    ///            without it says so instead of throwing.
    ///  LEAVE     ends the session. A host leaving sends SESSION_END(HostLeft), which lands on the
    ///            other peers as HostLost wherever they are.
    ///
    /// It never Finds anything: the document, the data asset and the scene names are serialized,
    /// and the session comes from GameLocator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuFlow : MonoBehaviour
    {
        /// <summary>PlayerPrefs key the callsign is remembered under.</summary>
        public const string CallsignKey = "Pesky.Callsign";
        public const string DefaultCallsign = "WEAPON";

        [SerializeField] UIDocument document;
        [Tooltip("Applied at runtime as well, so the menu is styled even if the UXML's Style tag is lost.")]
        [SerializeField] StyleSheet style;
        [Tooltip("The one data asset every session is built from.")]
        [SerializeField] GameData gameData;
        [Tooltip("Optional: the data the offline TUTORIAL session runs on. Its labyrinth is the tutorial's practice grid; everything else matches gameData. Empty falls back to gameData.")]
        [OptionalRef][SerializeField] GameData tutorialData;

        [Header("Scenes")]
        [Tooltip("Loaded on START, by every peer. It does not exist yet: a missing scene shows a message.")]
        [SerializeField] string labyrinthScene = SceneNames.Labyrinth;
        [Tooltip("Loaded by TUTORIAL. Zone1 until the tutorial stage builds its own.")]
        [SerializeField] string tutorialScene = SceneNames.Tutorial;

        [Header("Session")]
        [Tooltip("Room code of the offline tutorial session.")]
        [SerializeField] string tutorialRoomCode = "SOLO";
        [Tooltip("Seconds to wait for the host's answer before giving up on a join.")]
        [SerializeField] float joinTimeoutSeconds = 12f;

        [Header("Room page")]
        [SerializeField] string smallLabyrinth = "LABYRINTH   small   -   one floor, few rooms";
        [SerializeField] string mediumLabyrinth = "LABYRINTH   medium   -   one floor, more rooms";
        [SerializeField] string largeLabyrinth = "LABYRINTH   large   -   two floors";

        readonly MenuView _view = new MenuView();
        NetSession _session;
        string _roomCodeShown = "";
        float _joinDeadline;
        bool _built;
        bool _joining;
        bool _loading;
        string _pendingFailure;
        bool _crewDirty;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            TryBuild();
        }

        void OnEnable()
        {
            TryBuild();
        }

        void Start()
        {
            if (!TryBuild())
            {
                Debug.LogError("MenuFlow has no UIDocument root: the menu cannot be built.", this);
                return;
            }
            // A locked cursor reaches no button; a level locks it again when it takes over.
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            Adopt(GameLocator.Session);
            bool error;
            string message = GameLocator.TakeMessage(out error);
            if (_session != null && _session.IsStarted) ShowRoom(message, error);
            else _view.ShowTitle(message, error);
        }

        void Update()
        {
            if (_session != null && _session.IsStarted) _session.Update();
            if (_joining && Time.unscaledTime >= _joinDeadline)
            {
                QueueFailure("no answer from the host: it may be running a different version, be full, or not be reachable");
            }
            if (_pendingFailure != null)
            {
                string message = _pendingFailure;
                _pendingFailure = null;
                DropSession();
                _view.ShowTitle(message, true);
                return;
            }
            if (_crewDirty && _view.IsRoomShowing)
            {
                _crewDirty = false;
                RefreshCrew();
            }
        }

        void OnDestroy()
        {
            // The session deliberately lives on in GameLocator: the next scene adopts it.
            Unsubscribe();
        }

        void OnApplicationQuit()
        {
            // A host's Leave emits SESSION_END(HostLeft) first, so the room does not just go quiet.
            if (_session != null && _session.IsStarted) _session.Leave();
            GameLocator.Session = null;
        }

        bool TryBuild()
        {
            if (_built) return true;
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return false;
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);
            _view.Build(root);
            _view.HostClicked += OnHost;
            _view.JoinClicked += OnJoin;
            _view.TutorialClicked += OnTutorial;
            _view.StartClicked += OnStart;
            _view.LeaveClicked += OnLeave;
            _view.CopyClicked += OnCopy;
            _view.QuitClicked += OnQuit;
            _view.SetCallsign(PlayerPrefs.GetString(CallsignKey, ""));
            _view.SetJoinHint(RoomCodes.JoinHint());
            _view.ShowQuit(Application.platform != RuntimePlatform.WebGLPlayer);
            _built = true;
            return true;
        }

        // ---- the buttons ----

        void OnHost()
        {
            if (_loading || !HasData()) return;
            StartSession(RoomCodes.NewHostRoom(), true, false, "opening the room...");
        }

        void OnJoin()
        {
            if (_loading || !HasData()) return;
            string error;
            string room = RoomCodes.NormaliseJoin(_view.JoinCode, out error);
            if (error.Length > 0)
            {
                _view.SetStatus(error, true);
                return;
            }
            StartSession(room, false, false, "joining " + room + "...", true);
        }

        void OnTutorial()
        {
            if (_loading || !HasData()) return;
            // Straight into the level: the room page is for a session other people can join.
            StartSession(tutorialRoomCode, true, true, "starting the tutorial...");
            if (LoadScene(tutorialScene, "the tutorial scene")) return;
            DropSession();
            _view.ShowTitle("the tutorial scene (\"" + tutorialScene + "\") is not in the build settings", true);
        }

        void OnStart()
        {
            if (_loading || _session == null || !_session.IsStarted || !_session.IsHost) return;
            // A room that has already played a run goes back to the lobby before it starts another.
            if (_session.Phase == SessionPhase.Ended) _session.ReturnToLobby();
            if (_session.Phase != SessionPhase.Lobby)
            {
                _view.SetStatus("the run has already started", true);
                return;
            }
            if (!IsInBuild(labyrinthScene))
            {
                _view.SetStatus("the labyrinth scene (\"" + labyrinthScene + "\") is not in the build settings yet", true);
                return;
            }
            // Every peer, this one included, loads the level from its own PhaseChanged.
            _view.SetStatus("starting...", false);
            _session.StartGame();
        }

        void OnLeave()
        {
            DropSession();
            _view.ShowTitle("you left the room", false);
        }

        void OnCopy()
        {
            Clipboard.Copy(_roomCodeShown);
            _view.SetStatus("room code copied", false);
        }

        void OnQuit()
        {
            if (_session != null && _session.IsStarted) _session.Leave();
            GameLocator.Session = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- the session ----

        void StartSession(string room, bool isHost, bool offline, string status, bool joining = false)
        {
            DropSession();
            _view.SetStatus(status, false);
            // Armed before the transport starts, so a connection that fails on the first pump is
            // already a failed join rather than a stray error line.
            if (joining)
            {
                _joining = true;
                _joinDeadline = Time.unscaledTime + Mathf.Max(3f, joinTimeoutSeconds);
                _view.SetBusy(true);
            }
            _session = new NetSession();
            Subscribe();
            GameLocator.Session = _session;
            GameLocator.FromMenu = true;
            string callsign = ReadCallsign();
            // The tutorial is the only offline session, and it runs on its own small labyrinth.
            GameData data = offline && tutorialData != null ? tutorialData : gameData;
            if (offline) _session.StartOffline(room, callsign, data);
            else _session.StartOnline(room, isHost, callsign, data);
            // Loopback and the desktop host both raise Ready inside Start, so draining the inbox
            // here puts the room page up on this frame rather than the next.
            _session.Update();
        }

        void Adopt(NetSession session)
        {
            if (session == null || !session.IsStarted) return;
            _session = session;
            Subscribe();
        }

        void Subscribe()
        {
            if (_session == null) return;
            _session.Ready += OnSessionReady;
            _session.PhaseChanged += OnPhaseChanged;
            _session.SlotsChanged += OnSlotsChanged;
            _session.HostLost += OnHostLost;
            _session.Error += OnSessionError;
        }

        void Unsubscribe()
        {
            if (_session == null) return;
            _session.Ready -= OnSessionReady;
            _session.PhaseChanged -= OnPhaseChanged;
            _session.SlotsChanged -= OnSlotsChanged;
            _session.HostLost -= OnHostLost;
            _session.Error -= OnSessionError;
        }

        /// <summary>Ends the session and forgets it, so the title page is honest about there being no room.</summary>
        void DropSession()
        {
            _joining = false;
            if (_session == null) return;
            Unsubscribe();
            if (_session.IsStarted) _session.Leave();
            if (GameLocator.Session == _session)
            {
                GameLocator.Session = null;
                GameLocator.FromMenu = false;
            }
            _session = null;
            _view.SetBusy(false);
        }

        void OnSessionReady()
        {
            _joining = false;
            _view.SetBusy(false);
            if (_loading) return;
            ShowRoom("", false);
        }

        void OnPhaseChanged(SessionPhase phase)
        {
            if (_loading) return;
            if (phase == SessionPhase.Playing) LoadScene(labyrinthScene, "the labyrinth");
            else if (phase == SessionPhase.Ended) ShowRoom("the run is over", false);
        }

        void OnSlotsChanged()
        {
            _crewDirty = true;
        }

        void OnHostLost()
        {
            QueueFailure("the host left the room");
        }

        void OnSessionError(string message)
        {
            Debug.LogWarning("[menu] " + message, this);
            string friendly = Friendly(message);
            if (_joining)
            {
                QueueFailure(friendly);
                return;
            }
            _view.SetStatus(friendly, true);
        }

        /// <summary>
        /// The session is over and the title page should say why. It is only remembered here:
        /// these calls arrive from inside NetSession.Update (a lost host, a refused join), and
        /// leaving a session while it is draining its own inbox is asking for trouble. Update
        /// acts on it once the pump has returned.
        /// </summary>
        void QueueFailure(string message)
        {
            _joining = false;
            if (_pendingFailure == null) _pendingFailure = message;
        }

        // ---- the room page ----

        void ShowRoom(string message, bool error)
        {
            if (_session == null || !_session.IsStarted)
            {
                _view.ShowTitle(message, error);
                return;
            }
            // Back from a finished run: the host takes the room to the lobby so START works again.
            if (_session.IsHost && _session.Phase == SessionPhase.Ended) _session.ReturnToLobby();

            bool lobby = _session.Phase == SessionPhase.Lobby;
            _roomCodeShown = _session.IsHost ? RoomCodes.HostDisplay(_session.RoomCode) : _session.RoomCode;
            string alternatives = _session.IsHost ? RoomCodes.HostAlternatives() : "";
            _view.ShowRoom(_roomCodeShown, alternatives, _session.IsHost && lobby, !_session.IsHost && lobby);
            RefreshCrew();
            if (!string.IsNullOrEmpty(message)) _view.SetStatus(message, error);
        }

        void RefreshCrew()
        {
            if (_session == null) return;
            _view.SetCrew(_session.Slots, _session.LocalSlot);
            int count = _session.Slots.Count;
            _view.SetLabyrinth(LabyrinthLine(count));
            _view.SetStartEnabled(count >= 1);
        }

        string LabyrinthLine(int crew)
        {
            string size = crew <= 3 ? smallLabyrinth : crew <= 5 ? mediumLabyrinth : largeLabyrinth;
            return size + "   for " + crew + (crew == 1 ? " weapon" : " weapons");
        }

        // ---- odds and ends ----

        string ReadCallsign()
        {
            string name = _view.Callsign.Trim();
            if (name.Length > MenuView.MaxCallsignLength) name = name.Substring(0, MenuView.MaxCallsignLength);
            if (name.Length == 0) name = DefaultCallsign + "-" + RoomCodes.Generate().Substring(0, 2);
            _view.SetCallsign(name);
            PlayerPrefs.SetString(CallsignKey, name);
            PlayerPrefs.Save();
            return name;
        }

        bool HasData()
        {
            if (gameData != null) return true;
            _view.SetStatus("MenuFlow has no GameData asset: nothing can start", true);
            return false;
        }

        /// <summary>Loads a scene, or says plainly that it is not in the build settings yet.</summary>
        bool LoadScene(string sceneName, string what)
        {
            if (!IsInBuild(sceneName))
            {
                _view.SetStatus(what + " (\"" + sceneName + "\") is not in the build settings yet", true);
                return false;
            }
            _loading = true;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }

        /// <summary>True when a scene of that name is in the build settings (enabled), by name alone.</summary>
        public static bool IsInBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path)) continue;
                int slash = path.LastIndexOf('/');
                int dot = path.LastIndexOf('.');
                if (dot <= slash) continue;
                string name = path.Substring(slash + 1, dot - slash - 1);
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Turns what the session or the transport said into a sentence a player can act on.</summary>
        static string Friendly(string message)
        {
            if (string.IsNullOrEmpty(message)) return "network error";
            if (message.IndexOf("protocol version", StringComparison.OrdinalIgnoreCase) >= 0)
                return "version mismatch: the host is running a different build of the game";
            if (message.IndexOf("room full", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("are already here", StringComparison.OrdinalIgnoreCase) >= 0)
                return "the room is full";
            if (message.IndexOf("could not reach", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("is not a host address", StringComparison.OrdinalIgnoreCase) >= 0)
                return "host not found - " + message;
            if (message.IndexOf("lost the host", StringComparison.OrdinalIgnoreCase) >= 0)
                return "the host went away";
            return message;
        }
    }
}
