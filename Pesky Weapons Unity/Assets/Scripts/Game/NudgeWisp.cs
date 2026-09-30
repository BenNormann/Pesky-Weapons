using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The faint trace a Mage's nudge leaves: a thin pale streak along the push, shown for about a second
    /// and only on a machine whose player is a free SOUL at that moment (MageNudge decides). It says that
    /// something happened to that body, never who did it. No particles, no collider, no light.
    /// The streak is the child mesh, laid along this object's +Z; this object is scaled (width, width,
    /// length) over its life.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NudgeWisp : MonoBehaviour
    {
        [SerializeField] float lifetime = 1f;
        [Tooltip("Full length of the streak, metres (the child capsule is 2 units long at scale 1).")]
        [SerializeField] float length = 1.6f;
        [SerializeField] float width = 0.14f;

        float _startTime;

        public bool Playing { get { return gameObject.activeSelf; } }

        /// <summary>Show it at a body, laid along the push.</summary>
        public void Play(Vector3 at, Vector3 push)
        {
            Vector3 dir = push.sqrMagnitude > 1e-6f ? push.normalized : Vector3.up;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir, up));
            _startTime = Time.time;
            gameObject.SetActive(true);
            Apply(0f);
        }

        void Update()
        {
            float k = lifetime > 0.01f ? (Time.time - _startTime) / lifetime : 1f;
            if (k >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            Apply(k);
        }

        /// <summary>It stretches out along the push and thins away to nothing.</summary>
        void Apply(float k)
        {
            float w = width * (1f - k);
            float l = 0.5f * length * (0.35f + 0.65f * Mathf.Sin(k * Mathf.PI * 0.5f));
            transform.localScale = new Vector3(w, w, l);
        }
    }
}
