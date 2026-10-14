using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One-shot sound effects in the world: a small pool of 3D AudioSources on a hidden object, so a kit piece
    /// can say "this clip, here, this loud" without owning an AudioSource of its own. A piece plays its sound
    /// from the state change every peer applies (ApplyLatched, ApplyOn, ApplyClosed), so everybody hears it
    /// where it happened. A little pitch jitter keeps a repeated clip from sounding like a sample. Master
    /// volume is the listener's (AudioSettings); no mixer yet.
    /// </summary>
    public static class Sfx
    {
        const int PoolSize = 12;
        const float MinDistance = 2f;
        const float MaxDistance = 45f;

        static AudioSource[] _pool;
        static int _next;

        /// <summary>Plays a clip at a world position. volume 0..1; jitter is the +/- pitch spread (0.05 = five percent).</summary>
        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float jitter = 0.05f)
        {
            if (clip == null || volume <= 0f) return;
            AudioSource s = Take();
            if (s == null) return;
            s.transform.position = position;
            s.pitch = 1f + Random.Range(-jitter, jitter);
            s.volume = Mathf.Clamp01(volume);
            s.clip = clip;
            s.Play();
        }

        static AudioSource Take()
        {
            if (_pool == null || _pool[0] == null) Build();
            if (_pool == null) return null;
            // The next free source; failing that, the oldest one (it is cut off).
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource s = _pool[(_next + i) % PoolSize];
                if (s != null && !s.isPlaying) { _next = (_next + i + 1) % PoolSize; return s; }
            }
            AudioSource oldest = _pool[_next];
            _next = (_next + 1) % PoolSize;
            return oldest;
        }

        static void Build()
        {
            GameObject root = new GameObject("Sfx");
            root.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(root);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new GameObject("Voice_" + i);
                go.transform.SetParent(root.transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = MinDistance;
                s.maxDistance = MaxDistance;
                s.dopplerLevel = 0f;
                _pool[i] = s;
            }
            _next = 0;
        }
    }
}
