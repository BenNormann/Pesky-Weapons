using System;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The player's own preferences, kept in PlayerPrefs (docs/SETTINGS.md): the mouse sensitivity (a
    /// multiplier on OrbitCamera.lookSensitivity, 0.2 - 3.0, default 1)
    /// and four volumes 0 - 100 (Master, Music, SFX, Voice; only Master does anything yet, see AudioSettings).
    /// Read lazily from PlayerPrefs on first use, so whatever asks first (the camera, the settings screen)
    /// gets the saved value; every set writes PlayerPrefs at once and raises Changed. Flush() is the disk
    /// write (PlayerPrefs.Save), done when the settings screen closes rather than on every slider step.
    /// The shape is ATCK's GameSettings.
    /// </summary>
    public static class GameSettings
    {
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;
        public const float DefaultSensitivity = 1f;
        public const int MaxVolume = 100;
        public const int DefaultVolume = 100;

        public const string SensitivityKey = "Pesky.MouseSensitivity";
        const string LegacySpikeFilterKey = "Pesky.MouseSpikeFilter"; // round 11: removed; Load() deletes a saved value
        public const string MasterVolumeKey = "Pesky.Volume.Master";
        public const string MusicVolumeKey = "Pesky.Volume.Music";
        public const string SfxVolumeKey = "Pesky.Volume.Sfx";
        public const string VoiceVolumeKey = "Pesky.Volume.Voice";

        static bool _loaded;
        static bool _dirty;
        static float _sensitivity = DefaultSensitivity;
        static int _master = DefaultVolume;
        static int _music = DefaultVolume;
        static int _sfx = DefaultVolume;
        static int _voice = DefaultVolume;

        /// <summary>Raised after every change, so whatever uses a setting can re-read it.</summary>
        public static event Action Changed;

        /// <summary>Multiplier on OrbitCamera.lookSensitivity; 1 is the authored feel. Read by the camera every frame, so it applies live.</summary>
        public static float MouseSensitivity
        {
            get { Load(); return _sensitivity; }
            set
            {
                Load();
                float v = ClampSensitivity(value);
                if (Mathf.Approximately(v, _sensitivity)) return;
                _sensitivity = v;
                PlayerPrefs.SetFloat(SensitivityKey, _sensitivity);
                Touch();
            }
        }

        /// <summary>0 - 100. The only volume that does anything yet: AudioListener.volume (AudioSettings.Apply).</summary>
        public static int MasterVolume
        {
            get { Load(); return _master; }
            set { SetVolume(ref _master, value, MasterVolumeKey); }
        }

        /// <summary>0 - 100. Stored only: there is no music and no mixer yet.</summary>
        public static int MusicVolume
        {
            get { Load(); return _music; }
            set { SetVolume(ref _music, value, MusicVolumeKey); }
        }

        /// <summary>0 - 100. Stored only: there is no mixer yet.</summary>
        public static int SfxVolume
        {
            get { Load(); return _sfx; }
            set { SetVolume(ref _sfx, value, SfxVolumeKey); }
        }

        /// <summary>0 - 100. Stored only: voice chat is dormant and there is no mixer yet.</summary>
        public static int VoiceVolume
        {
            get { Load(); return _voice; }
            set { SetVolume(ref _voice, value, VoiceVolumeKey); }
        }

        /// <summary>The slider's own range, rounded to hundredths: the one place the limits are applied.</summary>
        public static float ClampSensitivity(float value)
        {
            return Mathf.Clamp(Mathf.Round(value * 100f) / 100f, MinSensitivity, MaxSensitivity);
        }

        public static int ClampVolume(int value)
        {
            return Mathf.Clamp(value, 0, MaxVolume);
        }

        /// <summary>Writes PlayerPrefs to disk (in a browser: its IndexedDB) if anything changed since the last flush.</summary>
        public static void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            PlayerPrefs.Save();
        }

        static void SetVolume(ref int field, int value, string key)
        {
            Load();
            int v = ClampVolume(value);
            if (v == field) return;
            field = v;
            PlayerPrefs.SetInt(key, v);
            Touch();
        }

        static void Touch()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _sensitivity = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity));
            PlayerPrefs.DeleteKey(LegacySpikeFilterKey); // the old MOUSE SPIKE FILTER switch: gone, the filter is always on
            _master = ClampVolume(PlayerPrefs.GetInt(MasterVolumeKey, DefaultVolume));
            _music = ClampVolume(PlayerPrefs.GetInt(MusicVolumeKey, DefaultVolume));
            _sfx = ClampVolume(PlayerPrefs.GetInt(SfxVolumeKey, DefaultVolume));
            _voice = ClampVolume(PlayerPrefs.GetInt(VoiceVolumeKey, DefaultVolume));
        }

        /// <summary>Play mode with domain reload off keeps statics; this makes the next run read PlayerPrefs again.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = false;
            _dirty = false;
            Changed = null;
        }
    }
}
