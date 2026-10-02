using System;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The settings screen's elements (Assets/UI/Settings.uxml + Settings.uss on top of Menu.uss), the way
    /// MenuView is the menu's: it queries the document by name, shows one page at a time (GENERAL, KEYBINDS,
    /// the host's exit CONFIRM), writes values into the controls without raising their events, and raises
    /// plain events. No session, no scene, no PlayerPrefs: SettingsFlow decides what any of it means.
    /// </summary>
    public sealed class SettingsView
    {
        public event Action ResumeClicked;
        public event Action ExitClicked;
        public event Action ConfirmExitClicked;
        public event Action ConfirmCancelClicked;
        public event Action KeybindsClicked;
        public event Action BackClicked;
        /// <summary>A click on the dimmed sheet around the panel (not on the panel): the resume click, as in ATCK.</summary>
        public event Action ScrimClicked;
        public event Action<float> SensitivityChanged;
        public event Action<int> MasterChanged;
        public event Action<int> MusicChanged;
        public event Action<int> SfxChanged;
        public event Action<int> VoiceChanged;

        readonly VisualElement _root;
        readonly VisualElement _general;
        readonly VisualElement _keysPage;
        readonly VisualElement _confirm;
        readonly Label _title;
        readonly Label _sub;
        readonly Label _confirmText;
        readonly Slider _sensitivity;
        readonly Label _sensitivityValue;
        readonly SliderInt _master;
        readonly SliderInt _music;
        readonly SliderInt _sfx;
        readonly SliderInt _voice;
        readonly Label _masterValue;
        readonly Label _musicValue;
        readonly Label _sfxValue;
        readonly Label _voiceValue;

        /// <summary>The KEYBINDS page's rows and rebinding.</summary>
        public KeybindsPage Keys { get; private set; }

        /// <summary>False when the document has no settings-root: the UXML is missing or renamed.</summary>
        public bool IsValid { get { return _root != null; } }

        public bool IsKeysPage { get; private set; }
        public bool IsConfirmPage { get; private set; }

        public SettingsView(VisualElement documentRoot, InputActionAsset controls)
        {
            if (documentRoot == null) return;
            _root = documentRoot.Q<VisualElement>("settings-root");
            if (_root == null) return;
            _general = _root.Q<VisualElement>("general-page");
            _keysPage = _root.Q<VisualElement>("keys-page");
            _confirm = _root.Q<VisualElement>("confirm-page");
            _title = _root.Q<Label>("settings-title");
            _sub = _root.Q<Label>("settings-sub");
            _confirmText = _root.Q<Label>("confirm-text");

            _sensitivity = _root.Q<Slider>("sensitivity-slider");
            _sensitivityValue = _root.Q<Label>("sensitivity-value");
            _master = _root.Q<SliderInt>("master-slider");
            _music = _root.Q<SliderInt>("music-slider");
            _sfx = _root.Q<SliderInt>("sfx-slider");
            _voice = _root.Q<SliderInt>("voice-slider");
            _masterValue = _root.Q<Label>("master-value");
            _musicValue = _root.Q<Label>("music-value");
            _sfxValue = _root.Q<Label>("sfx-value");
            _voiceValue = _root.Q<Label>("voice-value");

            if (_sensitivity != null)
            {
                _sensitivity.lowValue = GameSettings.MinSensitivity;
                _sensitivity.highValue = GameSettings.MaxSensitivity;
                _sensitivity.RegisterValueChangedCallback(e =>
                {
                    SetSensitivityText(e.newValue);
                    if (SensitivityChanged != null) SensitivityChanged(e.newValue);
                });
            }
            HookVolume(_master, _masterValue, v => { if (MasterChanged != null) MasterChanged(v); });
            HookVolume(_music, _musicValue, v => { if (MusicChanged != null) MusicChanged(v); });
            HookVolume(_sfx, _sfxValue, v => { if (SfxChanged != null) SfxChanged(v); });
            HookVolume(_voice, _voiceValue, v => { if (VoiceChanged != null) VoiceChanged(v); });

            Hook("resume-button", () => ResumeClicked);
            Hook("exit-button", () => ExitClicked);
            Hook("confirm-exit", () => ConfirmExitClicked);
            Hook("confirm-cancel", () => ConfirmCancelClicked);
            Hook("keybinds-button", () => KeybindsClicked);
            Hook("keys-back", () => BackClicked);

            // The full-screen toggle sits in the top-right corner of the whole screen, outside the panel. A click
            // is a user gesture, which is what the browser needs to grant full screen on WebGL.
            _fullscreen = _root.Q<Button>("fullscreen-button");
            if (_fullscreen != null)
            {
                _fullscreen.clicked += () =>
                {
                    bool want = !Screen.fullScreen;
                    Screen.fullScreen = want;
                    SetFullscreenText(want);
                };
            }
            SetFullscreenText(Screen.fullScreen);

            // ClickEvent arrives on the release, so the press that started it already went by with the game's
            // input off: closing on it cannot also fire a nudge on the left button.
            _root.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target == _root && ScrimClicked != null) ScrimClicked();
            });

            Keys = new KeybindsPage(_keysPage, controls);
        }

        Button _fullscreen;

        void SetFullscreenText(bool fullscreen)
        {
            if (_fullscreen != null) _fullscreen.text = fullscreen ? "WINDOWED" : "FULL SCREEN";
        }

        void Hook(string buttonName, Func<Action> handler)
        {
            Button b = _root.Q<Button>(buttonName);
            if (b == null) return;
            b.clicked += () =>
            {
                Action a = handler();
                if (a != null) a();
            };
        }

        static void HookVolume(SliderInt slider, Label value, Action<int> changed)
        {
            if (slider == null) return;
            slider.lowValue = 0;
            slider.highValue = GameSettings.MaxVolume;
            slider.RegisterValueChangedCallback(e =>
            {
                if (value != null) value.text = e.newValue.ToString();
                changed(e.newValue);
            });
        }

        // ---------------------------------------------------------------- showing

        public void SetVisible(bool on)
        {
            if (on) SetFullscreenText(Screen.fullScreen);
            if (_root == null) return;
            _root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            // A hidden button must not keep the focus: Space or Enter would "click" it in the middle of a run.
            if (!on && _root.focusController != null && _root.focusController.focusedElement is VisualElement focused) focused.Blur();
        }

        public void ShowGeneral()
        {
            ShowPage(_general);
            IsKeysPage = false;
            IsConfirmPage = false;
            SetTitle("PAUSED", "the run keeps going for everybody else");
        }

        public void ShowKeys()
        {
            ShowPage(_keysPage);
            IsKeysPage = true;
            IsConfirmPage = false;
            SetTitle("KEYBINDS", "keyboard and mouse; saved on this machine");
            if (Keys != null) Keys.Show();
        }

        public void ShowConfirm(string text)
        {
            ShowPage(_confirm);
            IsKeysPage = false;
            IsConfirmPage = true;
            if (_confirmText != null && !string.IsNullOrEmpty(text)) _confirmText.text = text;
            SetTitle("EXIT", "");
        }

        void ShowPage(VisualElement page)
        {
            if (Keys != null && page != _keysPage) Keys.Cancel();
            Show(_general, page == _general);
            Show(_keysPage, page == _keysPage);
            Show(_confirm, page == _confirm);
        }

        void SetTitle(string title, string sub)
        {
            if (_title != null) _title.text = title;
            if (_sub != null)
            {
                _sub.text = sub;
                Show(_sub, !string.IsNullOrEmpty(sub));
            }
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------- values (no events raised)

        public void SetValues(float sensitivity, int master, int music, int sfx, int voice)
        {
            if (_sensitivity != null) _sensitivity.SetValueWithoutNotify(sensitivity);
            SetSensitivityText(sensitivity);
            SetVolume(_master, _masterValue, master);
            SetVolume(_music, _musicValue, music);
            SetVolume(_sfx, _sfxValue, sfx);
            SetVolume(_voice, _voiceValue, voice);
        }

        void SetSensitivityText(float value)
        {
            if (_sensitivityValue != null) _sensitivityValue.text = GameSettings.ClampSensitivity(value).ToString("0.00") + "x";
        }

        static void SetVolume(SliderInt slider, Label label, int value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
            if (label != null) label.text = value.ToString();
        }
    }
}
