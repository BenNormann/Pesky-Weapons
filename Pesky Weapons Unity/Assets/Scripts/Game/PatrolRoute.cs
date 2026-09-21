using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// An authored list of waypoints a goblin walks. Nothing but data and a gizmo: the goblin owns the
    /// index it is on. Loop returns to point 0 after the last one; PingPong walks back down the list,
    /// which is what makes "move only when nobody looks" readable - you can see it coming back.
    /// Put one under &lt;Room&gt;/Gameplay with a child Transform per waypoint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatrolRoute : MonoBehaviour
    {
        public enum Mode { Loop = 0, PingPong = 1 }

        [Tooltip("Waypoints in order. Child transforms of this object.")]
        [SerializeField] Transform[] points = new Transform[0];
        [SerializeField] Mode mode = Mode.Loop;
        [Tooltip("Seconds the walker stands at each waypoint. This is the window you sneak through.")]
        [SerializeField] float pauseSeconds = 1f;

        public int Count { get { return points != null ? points.Length : 0; } }
        public float PauseSeconds { get { return pauseSeconds; } }
        public Mode RouteMode { get { return mode; } }

        public Vector3 Point(int index)
        {
            if (points == null || points.Length == 0) return transform.position;
            index = Mathf.Clamp(index, 0, points.Length - 1);
            Transform t = points[index];
            return t != null ? t.position : transform.position;
        }

        /// <summary>The next index along the route. <paramref name="direction"/> is the walker's own +1 / -1 state.</summary>
        public int Next(int index, ref int direction)
        {
            int n = Count;
            if (n <= 1) return 0;
            if (mode == Mode.Loop) return (index + 1) % n;

            if (direction == 0) direction = 1;
            int next = index + direction;
            if (next >= n) { direction = -1; next = n - 2; }
            else if (next < 0) { direction = 1; next = 1; }
            return Mathf.Clamp(next, 0, n - 1);
        }

        void OnDrawGizmos()
        {
            if (points == null || points.Length == 0) return;
            Gizmos.color = new Color(0.98f, 0.62f, 0.20f, 0.9f);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                Gizmos.DrawWireSphere(points[i].position, 0.35f);
                int j = i + 1;
                if (j >= points.Length) { if (mode != Mode.Loop) break; j = 0; }
                if (points[j] != null) Gizmos.DrawLine(points[i].position, points[j].position);
            }
        }
    }
}
