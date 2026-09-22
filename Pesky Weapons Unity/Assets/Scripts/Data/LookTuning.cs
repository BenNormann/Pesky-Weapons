using UnityEngine;

namespace Pesky.Data
{
    /// <summary>What the look filter does with a mouse delta that cannot be a hand movement.</summary>
    public enum LookSpikeMode : byte
    {
        /// <summary>The frame's delta is thrown away. A dropped frame is invisible where a 180-degree jerk is not.</summary>
        Drop = 0,
        /// <summary>The delta keeps its direction and is shrunk to scaleToPixels, a regular-sized movement.</summary>
        Scale = 1
    }

    /// <summary>
    /// Tunables of the mouse-look spike filter (Assets/Data/LookTuning.asset, referenced by OrbitCamera).
    /// Chrome under pointer lock occasionally reports a huge single-frame mouse delta for no reason at
    /// all; the filter drops or shrinks it. Ported from ATCK's LookFilter, with the threshold and the
    /// mode on data instead of constants.
    /// </summary>
    [CreateAssetMenu(menuName = "Pesky/Look Tuning", fileName = "LookTuning")]
    public sealed class LookTuning : ScriptableObject
    {
        [Header("Spike filter")]
        [Tooltip("Master switch. Off = every delta is applied as the browser reports it.")]
        public bool filterSpikes = true;

        [Tooltip("Drop the whole frame's delta, or scale it down to a regular movement in the same direction.")]
        public LookSpikeMode spikeMode = LookSpikeMode.Drop;

        [Tooltip("A single frame's mouse delta longer than this many pixels is a spike. At 60 fps 300 px is 18,000 px/s, faster than any hand; at 20 fps a hard flick reaches about 150. Raise it if slow frames make real flicks trip the filter.")]
        [Min(1f)] public float spikePixels = 300f;

        [Tooltip("Scale mode only: the length a spike is shrunk to, pixels.")]
        [Min(0f)] public float scaleToPixels = 40f;

        [Header("Lock and focus")]
        [Tooltip("Discard the first non-zero delta after the pointer lock is acquired, after the window regains focus, and after the Tab overlay closes: that delta carries the cursor's jump to the centre and the deltas the Input System accumulated while nobody was reading them.")]
        public bool dropFirstDeltaAfterLock = true;

        [Tooltip("Print every dropped or scaled delta to the console. Only prints where DebugGate is open (the Editor, development builds, ?debug=1 pages).")]
        public bool logDrops = true;
    }
}
