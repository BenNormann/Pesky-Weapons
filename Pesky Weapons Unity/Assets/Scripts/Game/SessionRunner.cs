using System;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Game
{
    /// <summary>
    /// Every gameplay scene runs inside a NetSession, and this is the component that guarantees it.
    /// It runs before everything else (-1000): in Awake it adopts the session a lobby left behind in
    /// <see cref="Shared"/>, or, when there is none, starts an OFFLINE session on the loopback transport,
    /// so solo play and the FeelBox need no setup at all. It pumps NetSession.Update once per frame
    /// (before any view reads the sim) and, as the host, moves the session from Lobby to Playing.
    /// The session itself outlives the scene: it sits in a static so the next scene's runner adopts it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed partial class SessionRunner : MonoBehaviour
    {
        [Tooltip("The one data asset the sim is built from (weapon, enemy and modifier tables by wire id).")]
        [SerializeField] GameData gameData;
        [Tooltip("Room code of the automatic offline session.")]
        [SerializeField] string offlineRoomCode = "SOLO";
        [Tooltip("Player name of the automatic offline session.")]
        [SerializeField] string offlinePlayerName = "Player";
        [Tooltip("Host only: go from Lobby to Playing as soon as the session is ready. There is no lobby yet.")]
        [SerializeField] bool autoStartGame = true;

        [Tooltip("The scene a finished run returns to. Only used when the menu started this session.")]
        [SerializeField] string menuScene = SceneNames.MainMenu;
        bool _returnQueued;
        bool _returnDead;
        bool _returnError;
        string _returnMessage;

        bool _returning;

        /// <summary>
        /// The session that outlives a scene load. It lives in <see cref="GameLocator"/>; this is
        /// the name the netcode notes call it by.
        /// </summary>
        public static NetSession Shared
        {
            get { return GameLocator.Session; }
            set { GameLocator.Session = value; }
        }

        public NetSession Session { get; private set; }
        public GameData Data { get { return gameData; } }
        public bool IsHost { get { return Session != null && Session.IsHost; } }
        public bool HasSlot { get { return Session != null && Session.Slots.HasLocalSlot; } }
        public byte LocalSlot { get { return Session != null ? Session.LocalSlot : Wire.NoSlot; } }

        /// <summary>The host left (or the link to it died): the session is over for this client.</summary>
        public event Action HostLost;

        /// <summary>True once the shared level clock means something: the run is Playing and this peer knows the room time.</summary>
        public bool HasLevelClock
        {
            get
            {
                if (Session == null || !Session.IsStarted || Session.Phase != SessionPhase.Playing) return false;
                return Session.IsHost || Session.Clock.HasEstimate;
            }
        }

        /// <summary>Milliseconds since the run went to Playing, the same number on every peer. LevelClock follows it.</summary>
        public long LevelMs
        {
            get
            {
                if (!HasLevelClock) return 0L;
                long ms = Session.Clock.NowMs - Tick.ToMs(Session.Sim.PhaseStartTick);
                return ms > 0L ? ms : 0L;
            }
        }

        void Awake()
        {
            if (gameData == null) Debug.LogError("SessionRunner has no GameData: the host cannot work out any damage.", this);
            // The menu leaves its session in GameLocator. A gameplay scene opened straight from the
            // Editor finds none and starts an offline loopback session of its own, so solo play and
            // the FeelBox still need no setup at all.
            NetSession session = GameLocator.Session;
            if (session == null || !session.IsStarted)
            {
                session = new NetSession();
                session.StartOffline(offlineRoomCode, offlinePlayerName, gameData);
                GameLocator.Session = session;
                GameLocator.FromMenu = false;
            }
            Session = session;
            Session.Error += OnError;
            Session.HostLost += OnHostLost;
            Session.PhaseChanged += OnPhaseChanged;
            // The loopback transport raises Ready inside Start; draining it here gives the local slot to
            // everything that asks for it in Awake or Start.
            Session.Update();
            TryStartGame();
        }

        void Update()
        {
            if (Session == null) return;
            Session.Update();
            if (_returnQueued)
            {
                ReturnToMenu();
                return;
            }
            TryStartGame();
        }

        void TryStartGame()
        {
            if (!autoStartGame || !Session.IsHost || !Session.IsReady) return;
            if (Session.Phase == SessionPhase.Lobby) Session.StartGame();
        }

        void OnDestroy()
        {
            if (Session == null) return;
            Session.Error -= OnError;
            Session.HostLost -= OnHostLost;
            Session.PhaseChanged -= OnPhaseChanged;
        }

        void OnApplicationQuit()
        {
            // The host's Leave emits SESSION_END(HostLeft) first, exactly as in ATCK.
            if (Session != null && Session.IsStarted) Session.Leave();
            GameLocator.Session = null;
        }

        void OnError(string message)
        {
            Debug.LogWarning("[net] " + message, this);
        }

        void OnHostLost()
        {
            Debug.LogWarning("[net] the host left: the session has ended.", this);
            if (HostLost != null) HostLost();
            QueueReturn("the host left the room", true, true);
        }


        /// <summary>The run ended: everybody goes back to the room page the menu left them on.</summary>
        /// <summary>The run ended: everybody goes back to the room page the menu left them on.</summary>
        void OnPhaseChanged(SessionPhase phase)
        {
            if (phase == SessionPhase.Ended) QueueReturn("the run is over", false, false);
        }

        /// <summary>
        /// Back to the menu, but only when the menu started this session: a level opened straight
        /// from the Editor stays where it is. A finished run and a lost host can both land here in
        /// the same frame, so the load is issued once and the last message wins - SceneManager only
        /// acts at the end of the frame.
        /// </summary>
        /// <summary>
        /// Remembers that this level is over, but does nothing yet: both calls arrive from inside
        /// NetSession.Update, and a run that ends because the host left raises the phase change and
        /// the lost host in the same frame. The dead flag sticks, the later message wins, and
        /// Update does the work once the pump has returned.
        /// </summary>
        void QueueReturn(string message, bool error, bool sessionIsDead)
        {
            if (!GameLocator.FromMenu || _returning) return;
            _returnQueued = true;
            _returnMessage = message;
            _returnError = error;
            if (sessionIsDead) _returnDead = true;
        }

        /// <summary>
        /// Back to the menu, but only when the menu started this session: a level opened straight
        /// from the Editor stays where it is.
        /// </summary>
        void ReturnToMenu()
        {
            _returnQueued = false;
            GameLocator.SetMessage(_returnMessage, _returnError);
            if (_returnDead)
            {
                if (Session != null && Session.IsStarted) Session.Leave();
                GameLocator.Session = null;
            }
            if (!MenuFlow.IsInBuild(menuScene))
            {
                Debug.LogWarning("[net] " + _returnMessage + ", but the menu scene \"" + menuScene + "\" is not in the build settings.", this);
                return;
            }
            _returning = true;
            SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
        }

    }
}
