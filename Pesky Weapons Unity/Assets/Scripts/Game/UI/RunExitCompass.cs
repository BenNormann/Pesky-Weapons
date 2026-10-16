using UnityEngine;
using UnityEngine.UIElements;
using Pesky.Protocol;

namespace Pesky.Game
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class RunExitCompass : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] RunDirector run;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] OrbitCamera orbitCamera;
        [SerializeField] SessionRunner sessionRunner;
        [Tooltip("Needle settling time in seconds. Short enough to track a turn, long enough to remove body jitter.")]
        [Range(0.03f, 0.4f)] [SerializeField] float smoothTime = 0.14f;

        VisualElement _card, _dial;
        Label _status, _progress;
        ExitCompassView _view;
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

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            _card = root.Q<VisualElement>("exit-compass");
            _dial = root.Q<VisualElement>("exit-compass-dial");
            _status = root.Q<Label>("exit-compass-status");
            _progress = root.Q<Label>("exit-compass-progress");
            _view = new ExitCompassView(_dial);
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
            if (_card != null) _card.style.display = DisplayStyle.None;
            if (_view != null) _view.Dispose();
            _view = null;
            HasReading = false;
        }

        void LateUpdate()
        {
            if (_card == null || _view == null) return;
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            bool ended = sessionRunner != null && sessionRunner.Session != null && sessionRunner.Session.Phase == SessionPhase.Ended;
            bool visible = run != null && soul != null && !ended && (sessionRunner == null || !sessionRunner.IsPaused);
            _card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) { HasReading = false; return; }

            WeaponBody weapon = soul.Weapon;
            Vector3 position = weapon != null ? weapon.Body.position : soul.Body.position;
            ExitCompassReading reading;
            HasReading = ExitCompassRoute.TryRead(run, position, out reading);
            if (!HasReading)
            {
                _card.EnableInClassList("is-arrived", false);
                _view.SetExit(false, _bearing.Angle, false);
                SetStatus(run.Run == null || run.Run.Count == 0 ? "FINDING THE EXIT" : "RETURN TO THE ROOMS");
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
            bool aim = ExitCompassBearing.TryHeading(position, reading.target, orbitCamera != null ? orbitCamera.Yaw : 0f, out targetAngle);
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
