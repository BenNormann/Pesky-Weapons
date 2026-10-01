using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The settings screen of a gameplay scene (docs/SETTINGS.md), on Assets/Prefabs/UI/Settings.prefab under
    /// _UI in Tutorial and Run. Escape (the Gameplay map's Pause action) opens it and frees the cursor;
    /// RESUME, a click on the dimmed sheet, or Escape again closes it and takes the pointer back through
    /// OrbitCamera (InputEnabled on again, which also makes the look filter drop the first delta, and
    /// LockPointer). Losing the pointer lock any other way - alt-tab, clicking out of the page, the browser
    /// dropping it - opens it too, as ATCK does, so nobody stands in a live run with a free cursor; only a
    /// lock held for lockArmSeconds counts, because a fresh request flickers.
    ///
    /// While it is open OrbitCamera.InputEnabled is off: that is the one "an overlay owns the input" flag the
    /// labyrinth's Tab overlay used, and the launch / possess / release / flight (PlayerSoul), the nudge
    /// (MageNudge) and the curses (MageCurse) all stand still on it. The game itself does not pause: it is
    /// multiplayer.
    ///
    /// It also applies the saved key overrides to the controls in Awake, before any Update reads an action,
    /// and pushes the saved audio levels; it runs after the default order so its Escape handling follows
    /// OrbitCamera's (which frees the pointer on the same key).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class SettingsFlow : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] UIDocument document;
        [Tooltip("Applied at runtime as well, so the page is styled even if the UXML's Style tags are lost (Settings.uss; Menu.uss is the base).")]
        [SerializeField] StyleSheet style;
        [Tooltip("The menu's stylesheet, which the settings screen is built on.")]
        [SerializeField] StyleSheet baseStyle;
        [Tooltip("PeskyControls. The saved key overrides are applied to it in Awake; the KEYBINDS page rebinds it.")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [SerializeField] string pauseAction = "Pause";

        [Header("Scene (set on each scene's instance)")]
        [Tooltip("The camera whose InputEnabled is the overlay flag and whose LockPointer / FreePointer own the cursor.")]
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("EXIT TO MAIN MENU leaves through it (SessionRunner.LeaveToMenu).")]
        [SerializeField] SessionRunner sessionRunner;

        [Header("Pointer lock")]
        [Tooltip("Seconds the pointer lock must have been held before losing it opens this screen. A lock the browser has not granted yet flickers.")]
        [SerializeField] float lockArmSeconds = 0.5f;
        [Tooltip("Seconds after the screen opened itself on a lost lock in which Escape does not close it again: the same press did both.")]
        [SerializeField] float escapeGraceSeconds = 0.4f;

        [Header("Text")]
        [SerializeField] string hostExitText = "YOU ARE THE HOST. LEAVING ENDS THE SESSION FOR EVERYBODY IN THIS ROOM: THEY ALL GO BACK TO THE TITLE SCREEN.";

        SettingsView _view;
        InputAction _pause;
        bool _lockArmed;
        float _lockHeldSince = -1f;
        float _autoOpenedAt = -10f;

        /// <summary>True while the screen is up (and the game's input is off).</summary>
        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            // Before any gameplay script's Update reads an action: a scene load may have reloaded the asset
            // without them (docs/SETTINGS.md).
            Keybinds.Load(controls);
            AudioSettings.Apply();
        }

        void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError("SettingsFlow has no UIDocument root: the settings screen cannot be built.", this);
                enabled = false;
                return;
            }
            if (baseStyle != null && !root.styleSheets.Contains(baseStyle)) root.styleSheets.Add(baseStyle);
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);
            // The document is a full-screen layer; only the settings sheet inside it takes the pointer.
            root.pickingMode = PickingMode.Ignore;

            _view = new SettingsView(root, controls);
            if (!_view.IsValid)
            {
                Debug.LogError("SettingsFlow: the document has no 'settings-root' (Settings.uxml).", this);
                enabled = false;
                return;
            }
            _view.ResumeClicked += Close;
            _view.ScrimClicked += Close;
            _view.ExitClicked += OnExit;
            _view.ConfirmExitClicked += Leave;
            _view.ConfirmCancelClicked += _view.ShowGeneral;
            _view.KeybindsClicked += _view.ShowKeys;
            _view.BackClicked += _view.ShowGeneral;
            _view.SensitivityChanged += v => GameSettings.MouseSensitivity = v;
            _view.MasterChanged += v => { GameSettings.MasterVolume = v; AudioSettings.Apply(); };
            _view.MusicChanged += v => { GameSettings.MusicVolume = v; AudioSettings.Apply(); };
            _view.SfxChanged += v => { GameSettings.SfxVolume = v; AudioSettings.Apply(); };
            _view.VoiceChanged += v => { GameSettings.VoiceVolume = v; AudioSettings.Apply(); };
            _view.SetVisible(false);

            InputActionMap map = controls != null ? controls.FindActionMap(actionMap, false) : null;
            _pause = map != null ? map.FindAction(pauseAction, false) : null;
            if (_pause != null) _pause.Enable();
        }

        void OnDisable()
        {
            if (_view != null && _view.Keys != null) _view.Keys.Cancel();
            GameSettings.Flush();
        }

        void OnApplicationQuit()
        {
            GameSettings.Flush();
        }

        void Update()
        {
            if (_view == null) return;
            TrackPointerLock();
            if (_pause == null || !_pause.WasPressedThisFrame()) return;
            // Escape that cancelled a rebind is the rebind's.
            if (_view.Keys != null && _view.Keys.SwallowsEscape) return;
            if (!IsOpen)
            {
                Open();
                return;
            }
            // The Escape that dropped the pointer lock is the one that opened this screen: the browser's lock
            // change lands a frame either side of the key, so one press never both opens and closes it.
            if (Time.unscaledTime - _autoOpenedAt < escapeGraceSeconds) return;
            Close();
        }

        /// <summary>
        /// The lock is the game: once it has been held for lockArmSeconds, losing it for any reason opens this
        /// screen. Nothing is armed while the screen is open or another overlay has the input.
        /// </summary>
        void TrackPointerLock()
        {
            if (IsOpen || orbitCamera == null || !orbitCamera.InputEnabled)
            {
                _lockArmed = false;
                _lockHeldSince = -1f;
                return;
            }
            float now = Time.unscaledTime;
            if (orbitCamera.PointerLocked && (Application.isFocused || DebugGate.PointerLockBypass))
            {
                if (_lockHeldSince < 0f) _lockHeldSince = now;
                if (!_lockArmed && now - _lockHeldSince >= lockArmSeconds) _lockArmed = true;
                return;
            }
            _lockHeldSince = -1f;
            if (!_lockArmed) return;
            _lockArmed = false;
            _autoOpenedAt = now;
            Open();
        }

        public void Open()
        {
            if (IsOpen || _view == null) return;
            IsOpen = true;
            if (orbitCamera != null)
            {
                orbitCamera.InputEnabled = false;
                orbitCamera.FreePointer();
            }
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            _view.SetValues(GameSettings.MouseSensitivity, GameSettings.MasterVolume,
                GameSettings.MusicVolume, GameSettings.SfxVolume, GameSettings.VoiceVolume);
            _view.ShowGeneral();
            _view.SetVisible(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_view != null)
            {
                if (_view.Keys != null) _view.Keys.Cancel();
                _view.SetVisible(false);
            }
            GameSettings.Flush();
            _lockArmed = false;
            _lockHeldSince = -1f;
            if (orbitCamera != null)
            {
                // InputEnabled back on makes the look filter skip the next delta (the cursor's jump back to the
                // centre); the lock itself makes it skip one more when it lands (LookFilter.Track).
                orbitCamera.InputEnabled = true;
                orbitCamera.LockPointer();
            }
        }

        /// <summary>EXIT TO MAIN MENU. A host with other players in the room is asked first, because it ends the session for all of them.</summary>
        void OnExit()
        {
            if (sessionRunner != null && sessionRunner.LeavingEndsSessionForOthers)
            {
                _view.ShowConfirm(hostExitText);
                return;
            }
            Leave();
        }

        void Leave()
        {
            if (_view != null && _view.Keys != null) _view.Keys.Cancel();
            GameSettings.Flush();
            if (sessionRunner == null)
            {
                Debug.LogWarning("SettingsFlow has no SessionRunner: EXIT cannot leave the session.", this);
                return;
            }
            sessionRunner.LeaveToMenu();
        }
    }
}
