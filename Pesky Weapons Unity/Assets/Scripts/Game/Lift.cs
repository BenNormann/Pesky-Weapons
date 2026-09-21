using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A platform that moves VERTICALLY between N authored stops. Like the ClockMover its height is a
    /// PURE FUNCTION of LevelClock.Ms (plus two host-owned values: on / off and the clock time it was
    /// switched on), never a sum of deltaTime. While off it is parked at its TOP stop. Switched on, it
    /// dwells at the top, then visits every stop down to the bottom and back up, for ever:
    ///   top, ..., 1, 0, 1, ..., top-1, (repeat)      each leg = dwellSeconds + distance / travelSpeed.
    /// Power is set through WorldAuthority.RequestSetLift (the Winch, in a later stage).
    /// Riders use the ClockMover rider fix (RiderCarry): zero-friction P_Slick surface plus ONE explicit
    /// delta per physics step. Because nothing grips on P_Slick, grounded riders are also braked
    /// horizontally (riderBrake) so a weapon that lands on the lift settles instead of sliding off; a
    /// weapon that has just launched is never braked, so a launch from the lift keeps its arc.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Lift : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] LevelClock clock;
        [SerializeField] Rigidbody body;

        [Header("Stops (authored scene objects, BOTTOM to TOP; only their height is used)")]
        [SerializeField] Transform[] stops = new Transform[0];
        [Tooltip("Seconds the platform waits at every stop.")]
        [SerializeField] float dwellSeconds = 4f;
        [Tooltip("Average speed between two stops in m/s.")]
        [SerializeField] float travelSpeed = 3f;
        [Tooltip("Ease in and out of every stop instead of a constant speed.")]
        [SerializeField] bool smooth = true;

        [Header("Power (host-owned; WorldAuthority.RequestSetLift)")]
        [Tooltip("On from the start (test scenes). In the castle the Winch switches it on.")]
        [SerializeField] bool startsOn;

        [Header("Riders")]
        [Tooltip("Local-space centre of the box that collects riders (just above the surface).")]
        [SerializeField] Vector3 riderBoxCenter = new Vector3(0f, 0.75f, 0f);
        [SerializeField] Vector3 riderBoxSize = new Vector3(3.2f, 1.4f, 3.2f);
        [Tooltip("Weapon | Enemy.")]
        [SerializeField] LayerMask riderMask = (1 << 9) | (1 << 11);
        [Tooltip("Horizontal braking of grounded riders in m/s^2 (stands in for the friction P_Slick does not have). 0 = off.")]
        [SerializeField] float riderBrake = 14f;
        [Tooltip("A rider that launched less than this many seconds ago is not braked.")]
        [SerializeField] float brakeLaunchGrace = 0.75f;

        readonly Collider[] _hits = new Collider[32];
        readonly List<Rigidbody> _riders = new List<Rigidbody>();
        Vector3 _home;
        Vector3 _last;
        bool _on;
        long _startMs;

        public int SceneId { get { return id; } }
        public LevelClock Clock { get { return clock; } }
        public bool IsOn { get { return _on; } }
        public long StartMs { get { return _startMs; } }
        public int StopCount { get { return stops != null ? stops.Length : 0; } }

        void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            _home = transform.position;
            _on = startsOn;
            _startMs = 0L;
            _last = Evaluate(clock != null ? clock.Ms : 0L);
            body.position = _last;
            transform.position = _last;
        }

        /// <summary>Authority only: the two host-owned values. startMs is the LevelClock time the cycle starts from (at the top stop).</summary>
        public void ApplyPower(bool on, long startMs)
        {
            _on = on;
            _startMs = startMs;
        }

        float StopY(int index)
        {
            Transform t = stops[index];
            return t != null ? t.position.y : _home.y;
        }

        /// <summary>The stop visited on leg k of the cycle: top, top-1, ..., 0, 1, ..., top-1.</summary>
        int StopOfLeg(int leg, int n)
        {
            int top = n - 1;
            return leg <= top ? top - leg : leg - top;
        }

        float TravelSeconds(int from, int to)
        {
            return Mathf.Abs(StopY(to) - StopY(from)) / Mathf.Max(0.01f, travelSpeed);
        }

        /// <summary>Pure: where the platform is at a given clock time (given the host-owned power state).</summary>
        public Vector3 Evaluate(long ms)
        {
            int stop;
            return Evaluate(ms, out stop);
        }

        /// <summary>Pure. <paramref name="dwellingAt"/> is the stop index while the platform waits at one, or -1 while it travels.</summary>
        public Vector3 Evaluate(long ms, out int dwellingAt)
        {
            Vector3 p = _home;
            int n = StopCount;
            dwellingAt = -1;
            if (n == 0) return p;
            if (!_on || n == 1)
            {
                dwellingAt = n - 1;
                p.y = StopY(n - 1);
                return p;
            }

            int legs = 2 * (n - 1);
            double period = 0.0;
            for (int k = 0; k < legs; k++)
                period += dwellSeconds + TravelSeconds(StopOfLeg(k, n), StopOfLeg((k + 1) % legs, n));
            if (period < 0.01)
            {
                dwellingAt = n - 1;
                p.y = StopY(n - 1);
                return p;
            }

            double t = ((ms - _startMs) * 0.001) % period;
            if (t < 0.0) t += period;
            for (int k = 0; k < legs; k++)
            {
                int from = StopOfLeg(k, n);
                int to = StopOfLeg((k + 1) % legs, n);
                if (t < dwellSeconds)
                {
                    dwellingAt = from;
                    p.y = StopY(from);
                    return p;
                }
                t -= dwellSeconds;
                float travel = TravelSeconds(from, to);
                if (t < travel)
                {
                    float u = travel > 0.0001f ? (float)(t / travel) : 1f;
                    if (smooth) u = Mathf.SmoothStep(0f, 1f, u);
                    p.y = Mathf.Lerp(StopY(from), StopY(to), u);
                    return p;
                }
                t -= travel;
            }
            dwellingAt = n - 1;
            p.y = StopY(n - 1);
            return p;
        }

        /// <summary>The stop the platform is waiting at right now, or -1 while it travels.</summary>
        public int DwellingAt
        {
            get
            {
                int stop;
                Evaluate(clock != null ? clock.Ms : 0L, out stop);
                return stop;
            }
        }

        void FixedUpdate()
        {
            if (clock == null) return;

            Vector3 target = Evaluate(clock.Ms);
            Vector3 delta = target - _last;
            _last = target;

            RiderCarry.Collect(transform, riderBoxCenter, riderBoxSize, riderMask, body, _hits, _riders);
            // A step of more than a metre is a snap (power cut, clock re-base), not travel: riders are not dragged along.
            if (delta.sqrMagnitude > 1e-10f && delta.sqrMagnitude < 1f) RiderCarry.Move(_riders, delta);
            if (riderBrake > 0f) BrakeRiders();
            body.MovePosition(target);
        }

        void BrakeRiders()
        {
            float drop = riderBrake * Time.fixedDeltaTime;
            for (int i = 0; i < _riders.Count; i++)
            {
                Rigidbody rb = _riders[i];
                WeaponBody weapon = rb.GetComponent<WeaponBody>();
                if (weapon == null || !weapon.IsGrounded || weapon.SecondsSinceLaunch < brakeLaunchGrace) continue;
                Vector3 v = rb.linearVelocity;
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                float speed = flat.magnitude;
                if (speed < 0.001f) continue;
                flat = speed <= drop ? Vector3.zero : flat * (1f - drop / speed);
                rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.47f, 0.59f, 0.9f);
            if (stops != null)
            {
                for (int i = 0; i < stops.Length; i++)
                {
                    if (stops[i] == null) continue;
                    Vector3 s = new Vector3(transform.position.x, stops[i].position.y, transform.position.z);
                    Gizmos.DrawWireCube(s, new Vector3(3f, 0.05f, 3f));
                    if (i > 0 && stops[i - 1] != null)
                        Gizmos.DrawLine(new Vector3(s.x, stops[i - 1].position.y, s.z), s);
                }
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(riderBoxCenter, riderBoxSize);
        }
    }
}
