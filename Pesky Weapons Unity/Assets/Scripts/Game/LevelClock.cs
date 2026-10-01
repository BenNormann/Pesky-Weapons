using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The one shared level clock, in milliseconds. Moving things are a pure function of <see cref="Ms"/>,
    /// never of summed deltaTime, so a network port only has to agree on this single number.
    /// Referenced through serialized fields; there is no static instance.
    ///
    /// It follows SessionRunner.LevelMs, which is GAME time: room time since the round went to Playing with
    /// every host pause taken out (docs/VOTING.md). So while the host has the game frozen this clock stands
    /// still, and everything scheduled on it (ClockMover, lifts, ropes, the lightning, the practice dummy's
    /// hops, the curse expiries) stands still with it, on every peer alike.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class LevelClock : MonoBehaviour
    {
        double _startTime;
        long _offsetMs;

        [Tooltip("The scene's SessionRunner. The level clock follows the session's room clock, so every peer's scheduled movers agree.")]
        [SerializeField] SessionRunner session;
        [Tooltip("Re-base when this far from the room clock. Between re-bases it runs on Unity time, so FixedUpdate still sees fixed steps.")]
        [SerializeField] int followToleranceMs = 50;

        /// <summary>LevelClock reads the RoomClock: milliseconds since the run went to Playing, pauses excluded, the same on every peer.</summary>
        void Update()
        {
            Follow();
        }

        void FixedUpdate()
        {
            // Paused: a snap here too, so a mover's FixedUpdate never sees the clock creep a frame ahead.
            if (session != null && session.IsPaused) Follow();
        }

        void Follow()
        {
            if (session == null || !session.HasLevelClock) return;
            long want = session.LevelMs;
            long off = want - Ms;
            // Frozen: hold exactly where the room clock says, without the tolerance (no sawtooth while nothing moves).
            if (session.IsPaused)
            {
                if (off != 0) SetMs(want);
                return;
            }
            if (off > followToleranceMs || off < -followToleranceMs) SetMs(want);
        }

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
