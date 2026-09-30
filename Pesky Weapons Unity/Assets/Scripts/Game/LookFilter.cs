using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Filters the mouse-look deltas that are not a hand movement. Two cases, both from ATCK's LookFilter:
    /// (1) a single frame whose delta would turn the camera more than LookTuning.spikeDegrees - Chrome under pointer lock
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
/// <summary>
        /// The turn to apply this frame, in DEGREES: the input times degreesPerUnit, a scaled copy, or zero when
        /// it was dropped. The threshold is in degrees so it holds whatever scale processor the Look binding
        /// carries (Pesky's mouse binding is scaled by 0.06, which made a pixel threshold 16x too lax).
        /// </summary>
        public Vector2 Filter(Vector2 delta, float degreesPerUnit, LookTuning tuning)
        {
            if (delta.sqrMagnitude <= 0f) return Vector2.zero;
            Vector2 turn = delta * degreesPerUnit;

            bool dropFirst = tuning == null || tuning.dropFirstDeltaAfterLock;
            if (_skipNext)
            {
                _skipNext = false;
                if (dropFirst)
                {
                    DroppedCount++;
                    Log("dropped the first delta after a lock, focus or overlay change", turn, tuning);
                    return Vector2.zero;
                }
            }

            if (tuning == null || !tuning.filterSpikes) return turn;
            float threshold = Mathf.Max(0.1f, tuning.spikeDegrees);
            if (turn.sqrMagnitude <= threshold * threshold) return turn;

            if (tuning.spikeMode == LookSpikeMode.Scale)
            {
                ScaledCount++;
                Vector2 scaled = turn.normalized * Mathf.Max(0f, tuning.scaleToDegrees);
                Log("scaled a spike to " + scaled.magnitude.ToString("F1") + " deg", turn, tuning);
                return scaled;
            }
            DroppedCount++;
            Log("dropped a spike", turn, tuning);
            return Vector2.zero;
        }

static void Log(string what, Vector2 turn, LookTuning tuning)
        {
            if (tuning != null && !tuning.logDrops) return;
            DebugGate.Log("look: " + what + " (" + turn.x.ToString("F1") + ", " + turn.y.ToString("F1") + " deg, frame " + Time.frameCount + ", dt " + (Time.unscaledDeltaTime * 1000f).ToString("F0") + " ms)");
        }
    }
}
