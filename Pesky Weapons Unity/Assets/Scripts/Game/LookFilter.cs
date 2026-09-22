using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Filters the mouse-look deltas that are not a hand movement. Two cases, both from ATCK's LookFilter:
    /// (1) a single frame whose delta is longer than LookTuning.spikePixels - Chrome under pointer lock
    /// occasionally reports a huge jump for no reason - is dropped, or scaled down to a regular movement
    /// when the tuning says so; (2) the first non-zero delta after the pointer lock is acquired, after the
    /// window regains focus, or after the caller says an overlay closed, is always dropped: it carries the
    /// cursor's jump to the centre and whatever the Input System accumulated while nobody read it.
    /// Plain C#: the camera calls Track once a frame and Filter per delta. Mouse deltas are already
    /// per-frame and are never multiplied by deltaTime anywhere.
    /// </summary>
    public sealed class LookFilter
    {
        public int DroppedCount { get; private set; }
        public int ScaledCount { get; private set; }

        /// <summary>True while the next non-zero delta is to be discarded (a lock, focus or overlay change just happened).</summary>
        public bool SkippingNext { get { return _skipNext; } }

        bool _wasLocked;
        bool _wasFocused = true;
        bool _skipNext;

        /// <summary>Call once per frame, before reading the look delta, with the pointer lock and window focus state.</summary>
        public void Track(bool locked, bool focused)
        {
            if (locked && !_wasLocked) _skipNext = true;
            if (focused && !_wasFocused) _skipNext = true;
            _wasLocked = locked;
            _wasFocused = focused;
        }

        /// <summary>An overlay closed or input was re-enabled: the next delta is the accumulated junk, not a movement.</summary>
        public void SkipNext()
        {
            _skipNext = true;
        }

        /// <summary>The delta to apply this frame: the input itself, a scaled copy, or zero when it was dropped.</summary>
        public Vector2 Filter(Vector2 delta, LookTuning tuning)
        {
            if (delta.sqrMagnitude <= 0f) return Vector2.zero;

            bool dropFirst = tuning == null || tuning.dropFirstDeltaAfterLock;
            if (_skipNext)
            {
                _skipNext = false;
                if (dropFirst)
                {
                    DroppedCount++;
                    Log("dropped the first delta after a lock, focus or overlay change", delta, tuning);
                    return Vector2.zero;
                }
            }

            if (tuning == null || !tuning.filterSpikes) return delta;
            float threshold = Mathf.Max(1f, tuning.spikePixels);
            if (delta.sqrMagnitude <= threshold * threshold) return delta;

            if (tuning.spikeMode == LookSpikeMode.Scale)
            {
                ScaledCount++;
                Vector2 scaled = delta.normalized * Mathf.Max(0f, tuning.scaleToPixels);
                Log("scaled a spike to " + scaled.magnitude.ToString("F0") + " px", delta, tuning);
                return scaled;
            }
            DroppedCount++;
            Log("dropped a spike", delta, tuning);
            return Vector2.zero;
        }

        static void Log(string what, Vector2 delta, LookTuning tuning)
        {
            if (tuning != null && !tuning.logDrops) return;
            DebugGate.Log("look: " + what + " (" + delta.x.ToString("F0") + ", " + delta.y.ToString("F0") + " px, frame " + Time.frameCount + ", dt " + (Time.unscaledDeltaTime * 1000f).ToString("F0") + " ms)");
        }
    }
}
