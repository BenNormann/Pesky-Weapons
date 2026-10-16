using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Pesky.Protocol;

namespace Pesky.Game
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class RunExitCompass : MonoBehaviour
    {
        const string ResourcePath = "UI/ExitCompass";
        [SerializeField] UIDocument document;
        [SerializeField] StyleSheet style;
        [Tooltip("Needle settling time in seconds. Short enough to track a turn, long enough to remove body jitter.")]
        [Range(0.03f, 0.4f)] [SerializeField] float smoothTime = 0.14f;

        RunDirector _run;
        PlayerSpawner _spawner;
        OrbitCamera _camera;
        SessionRunner _runner;
        VisualElement _root, _card, _dial;
        VisualElement _weaponPanel;
        StyleLength _weaponMargin;
        bool _weaponInset;
        Label _status, _progress;
        CompassView _view;
        ExitCompassBearing _bearing;
        LabyrinthRoom _room;
        Vector3 _lastPosition;
        bool _hadPosition;
        float _needleOpacity;
        int _shownStep = -1, _shownCount = -1;
        string _shownStatus;

        public float DisplayedAngle { get { return _bearing.Angle; } }
        public bool HasReading { get; private set; }
        public Vector3 TargetPosition { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallInitial() { Install(SceneManager.GetActiveScene()); }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(scene); }

        static void Install(Scene scene)
        {
            if (!scene.isLoaded) return;
            RunDirector run = FindInScene<RunDirector>(scene);
            if (run == null || FindInScene<RunExitCompass>(scene) != null) return;
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) return;
            GameObject go = Instantiate(prefab);
            go.name = "ExitCompass";
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            go.GetComponent<RunExitCompass>().BindScene(scene);
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            BindScene(gameObject.scene);
        }

        void BindScene(Scene scene)
        {
            SetWeaponInset(false);
            _run = FindInScene<RunDirector>(scene);
            _spawner = FindInScene<PlayerSpawner>(scene);
            _camera = FindInScene<OrbitCamera>(scene);
            _runner = FindInScene<SessionRunner>(scene);
            HudController hud = FindInScene<HudController>(scene);
            UIDocument hudDocument = hud != null ? hud.GetComponent<UIDocument>() : null;
            _weaponPanel = hudDocument != null ? hudDocument.rootVisualElement.Q<VisualElement>("weapon-panel") : null;
            if (_weaponPanel != null) _weaponMargin = _weaponPanel.style.marginLeft;
        }

        void SetWeaponInset(bool visible)
        {
            if (_weaponPanel == null || _weaponInset == visible) return;
            _weaponInset = visible;
            _weaponPanel.style.marginLeft = visible ? new StyleLength(184f) : _weaponMargin;
        }

        void OnEnable()
        {
            BindScene(gameObject.scene);
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore;
            if (style != null && !_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            _card = _root.Q<VisualElement>("exit-compass");
            _dial = _root.Q<VisualElement>("exit-compass-dial");
            _status = _root.Q<Label>("exit-compass-status");
            _progress = _root.Q<Label>("exit-compass-progress");
            _view = new CompassView(_dial, true);
            _bearing.Reset();
            _hadPosition = false;
            _room = null;
            _needleOpacity = 0f;
            _shownStatus = null;
            _shownStep = _shownCount = -1;
            if (_card != null) _card.style.display = DisplayStyle.None;
        }

        void OnDisable()
        {
            SetWeaponInset(false);
            if (_card != null) _card.style.display = DisplayStyle.None;
            if (_view != null) _view.Dispose();
            _view = null;
            HasReading = false;
        }

        void LateUpdate()
        {
            if (_card == null || _view == null) return;
            PlayerSoul soul = _spawner != null ? _spawner.LocalSoul : null;
            bool ended = _runner != null && _runner.Session != null && _runner.Session.Phase == SessionPhase.Ended;
            bool visible = _run != null && soul != null && !ended && (_runner == null || !_runner.IsPaused);
            _card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            SetWeaponInset(visible);
            if (!visible) { HasReading = false; return; }

            WeaponBody weapon = soul.Weapon;
            Vector3 position = weapon != null ? weapon.Body.position : soul.Body.position;
            ExitCompassReading reading;
            HasReading = ExitCompassRoute.TryRead(_run, position, out reading);
            if (!HasReading)
            {
                _card.EnableInClassList("is-arrived", false);
                _view.SetExit(false, _bearing.Angle, false);
                SetStatus(_run.Run == null || _run.Run.Count == 0 ? "FINDING THE EXIT" : "RETURN TO THE ROOMS");
                SetProgress(-1, 0);
                _hadPosition = false;
                _room = null;
                return;
            }

            TargetPosition = reading.target;
            bool jumped = !_hadPosition || reading.room != _room || (position - _lastPosition).sqrMagnitude > 900f;
            _lastPosition = position;
            _hadPosition = true;
            _room = reading.room;
            float targetAngle;
            bool aim = ExitCompassBearing.TryHeading(position, reading.target, _camera != null ? _camera.Yaw : 0f, out targetAngle);
            if (!aim) targetAngle = _bearing.Angle;
            float angle = _bearing.Step(targetAngle, Time.unscaledDeltaTime, smoothTime, jumped);
            if (jumped) _needleOpacity = 0f;
            _needleOpacity = Mathf.MoveTowards(_needleOpacity, 1f, Time.unscaledDeltaTime / 0.16f);
            _view.SetExit(!reading.arrived, angle, reading.arrived, _needleOpacity);
            _card.EnableInClassList("is-arrived", reading.arrived);
            SetStatus(reading.arrived ? "WAIT FOR YOUR TEAM" : reading.finalRoom ? "GATHER AT THE EXIT"
                : reading.locked ? "SOLVE TO OPEN" : "FOLLOW THE ARROW");
            SetProgress(reading.step, reading.roomCount);
        }

        void SetStatus(string value)
        {
            if (_shownStatus == value) return;
            _shownStatus = value;
            if (_status != null) _status.text = value;
        }

        void SetProgress(int step, int count)
        {
            if (step == _shownStep && count == _shownCount) return;
            _shownStep = step;
            _shownCount = count;
            if (_progress != null) _progress.text = step <= 0 ? ""
                : step > count ? "YOU MADE IT" : "ROOM " + step + " / " + count;
        }
    }
}
