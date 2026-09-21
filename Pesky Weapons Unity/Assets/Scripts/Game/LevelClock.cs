using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The one shared level clock, in milliseconds. Moving things are a pure function of <see cref="Ms"/>,
    /// never of summed deltaTime, so a network port only has to agree on this single number.
    /// Referenced through serialized fields; there is no static instance.
    /// </summary>
    public sealed class LevelClock : MonoBehaviour
    {
        double _startTime;
        long _offsetMs;

        void Awake()
        {
            _startTime = Time.timeAsDouble;
        }

        /// <summary>Milliseconds since the level started (fixed time inside FixedUpdate).</summary>
        public long Ms
        {
            get { return (long)((Time.timeAsDouble - _startTime) * 1000.0) + _offsetMs; }
        }

        public double Seconds
        {
            get { return Ms * 0.001; }
        }

        /// <summary>Re-bases the clock (used later by the netcode port to follow the host).</summary>
        public void SetMs(long ms)
        {
            _offsetMs += ms - Ms;
        }
    }
}
