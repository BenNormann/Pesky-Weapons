using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// A small numbers panel for play-testing the web build, toggled with F3: FPS (one-second average),
    /// physics steps per frame, the session's role, peer count, the clock ping's round trip, message and
    /// byte rates in and out, the POSE rate coming in, how far the sim is behind the room clock, the
    /// local labyrinth cell and the compass reading. It also prints the same numbers as one compact
    /// console line every few seconds, so a browser console (or read_console_messages) tells the story
    /// without a screenshot. It exists only where DebugGate is open: the Editor, development builds and
    /// ?debug=1 pages; everywhere else the component disables itself in Awake and builds nothing.
    /// The label is built in code rather than from a UXML because it is a tool, not a screen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] SessionRunner sessionRunner;
        [OptionalRef][SerializeField] LabyrinthDirector labyrinth;
        [OptionalRef][SerializeField] CompassModel compass;
        [OptionalRef][SerializeField] OrbitCamera orbitCamera;
        [Tooltip("Seconds between the compact console lines.")]
        [SerializeField] float logInterval = 5f;
        [Tooltip("Shown from the start. F3 toggles it either way, and ?overlay=1 on a debug page shows it from the start too.")]
        [SerializeField] bool startVisible;

        Label _label;
        bool _visible;
        bool _visibilityResolved;

        int _frames;
        int _fixedSteps;
        int _fixedThisFrame;
        int _fixedMax;
        float _windowStart;
        float _nextLog;

        float _fps;
        float _fixedAvg;
        int _fixedMaxShown;
        long _lastBytesIn, _lastBytesOut, _lastMsgsIn, _lastMsgsOut, _lastPoses;
        float _inKb, _outKb, _inMsg, _outMsg, _poseHz;

        void Awake()
        {
            if (!DebugGate.Available)
            {
                enabled = false;
                return;
            }
            if (document == null) document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            _windowStart = Time.unscaledTime;
            _nextLog = _windowStart + Mathf.Max(1f, logInterval);
            _lastBytesIn = NetStats.BytesIn;
            _lastBytesOut = NetStats.BytesOut;
            _lastMsgsIn = NetStats.MsgsIn;
            _lastMsgsOut = NetStats.MsgsOut;
            _lastPoses = NetStats.PosesIn;
        }

        void FixedUpdate()
        {
            _fixedThisFrame++;
        }

        void Update()
        {
            EnsureLabel();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) SetVisible(!_visible);

            _frames++;
            _fixedSteps += _fixedThisFrame;
            if (_fixedThisFrame > _fixedMax) _fixedMax = _fixedThisFrame;
            _fixedThisFrame = 0;

            float now = Time.unscaledTime;
            float window = now - _windowStart;
            if (window >= 1f)
            {
                _fps = _frames / window;
                _fixedAvg = _frames > 0 ? (float)_fixedSteps / _frames : 0f;
                _fixedMaxShown = _fixedMax;
                _frames = 0;
                _fixedSteps = 0;
                _fixedMax = 0;
                _windowStart = now;

                long bytesIn = NetStats.BytesIn, bytesOut = NetStats.BytesOut;
                long msgsIn = NetStats.MsgsIn, msgsOut = NetStats.MsgsOut, poses = NetStats.PosesIn;
                _inKb = (bytesIn - _lastBytesIn) / window / 1024f;
                _outKb = (bytesOut - _lastBytesOut) / window / 1024f;
                _inMsg = (msgsIn - _lastMsgsIn) / window;
                _outMsg = (msgsOut - _lastMsgsOut) / window;
                _poseHz = (poses - _lastPoses) / window;
                _lastBytesIn = bytesIn;
                _lastBytesOut = bytesOut;
                _lastMsgsIn = msgsIn;
                _lastMsgsOut = msgsOut;
                _lastPoses = poses;

                if (_visible && _label != null) _label.text = Compose('\n');
            }

            if (now >= _nextLog)
            {
                _nextLog = now + Mathf.Max(1f, logInterval);
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[pesky] dbg {0}", Compose(' '));
            }
        }

        void EnsureLabel()
        {
            if (_label != null) return;
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            _label = new Label("debug overlay (F3)");
            _label.name = "debug-overlay";
            _label.pickingMode = PickingMode.Ignore;
            IStyle s = _label.style;
            s.position = Position.Absolute;
            s.top = 8f;
            s.left = 8f;
            s.paddingLeft = 6f;
            s.paddingRight = 6f;
            s.paddingTop = 4f;
            s.paddingBottom = 4f;
            s.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            s.color = new Color(0.62f, 0.9f, 0.69f);
            s.fontSize = 12f;
            s.whiteSpace = WhiteSpace.Normal;
            root.Add(_label);
            if (!_visibilityResolved)
            {
                _visibilityResolved = true;
                _visible = startVisible || DebugGate.UrlFlag("overlay");
            }
            SetVisible(_visible);
        }

        void SetVisible(bool on)
        {
            _visible = on;
            if (_label == null) return;
            _label.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) _label.text = Compose('\n');
        }

        string Compose(char sep)
        {
            NetSession session = sessionRunner != null ? sessionRunner.Session : null;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            sb.Append("fps=").Append(_fps.ToString("F1"));
            sb.Append(" fixed/frame=").Append(_fixedAvg.ToString("F2")).Append("(max ").Append(_fixedMaxShown).Append(')');
            sb.Append(sep);
            if (session == null || !session.IsStarted)
            {
                sb.Append("role=none");
            }
            else
            {
                sb.Append("role=").Append(session.IsHost ? "host" : "client");
                sb.Append(" transport=").Append(session.TransportName);
                sb.Append(" slot=").Append(session.LocalSlot == Wire.NoSlot ? "-" : session.LocalSlot.ToString());
                sb.Append(" peers=").Append(session.PeerCount);
                sb.Append(" rtt=").Append(session.IsHost ? "-" : (session.Clock.LastRttMs < 0 ? "?" : session.Clock.LastRttMs + "ms"));
                long lag = session.Sim != null && session.Clock.IsRunning ? (long)session.Clock.Tick - session.Sim.Tick : 0;
                sb.Append(" simLag=").Append(lag).Append("t");
                sb.Append(" phase=").Append(session.Phase);
            }
            sb.Append(sep);
            sb.Append("in=").Append(_inMsg.ToString("F0")).Append("/s,").Append(_inKb.ToString("F1")).Append("KB/s");
            sb.Append(" out=").Append(_outMsg.ToString("F0")).Append("/s,").Append(_outKb.ToString("F1")).Append("KB/s");
            sb.Append(" poseIn=").Append(_poseHz.ToString("F0")).Append("Hz");
            sb.Append(sep);
            if (labyrinth != null)
            {
                sb.Append("cell=").Append(labyrinth.LocalCell);
                LabyrinthState lab = labyrinth.Lab;
                if (lab != null)
                {
                    sb.Append(" target=").Append(lab.LocalCompassKind);
                    if (lab.LocalCompassKind == CompassTargetKind.Cell) sb.Append('#').Append(lab.LocalCompassCell);
                    sb.Append(" role=").Append(lab.LocalRole);
                }
            }
            if (compass != null)
            {
                sb.Append(" compass=");
                if (!compass.HasReading) sb.Append("none");
                else if (compass.Spinning) sb.Append("spin");
                else sb.Append(Dir(compass.Doorway));
                if (compass.HasRedReading) sb.Append(" red=").Append(compass.RedSpinning ? "spin" : Dir(compass.RedDoorway));
            }
            if (orbitCamera != null)
            {
                sb.Append(" lookDrops=").Append(orbitCamera.Filter.DroppedCount);
                if (orbitCamera.Filter.ScaledCount > 0) sb.Append(" lookScaled=").Append(orbitCamera.Filter.ScaledCount);
                sb.Append(" lock=").Append(orbitCamera.PointerLocked ? "yes" : "no");
            }
            if (DebugGate.PageHidden) sb.Append(" TAB-HIDDEN");
            return sb.ToString();
        }

        static string Dir(int doorway)
        {
            switch (doorway)
            {
                case (int)Heading.North: return "N";
                case (int)Heading.East: return "E";
                case (int)Heading.South: return "S";
                case (int)Heading.West: return "W";
                default: return "?";
            }
        }
    }
}
