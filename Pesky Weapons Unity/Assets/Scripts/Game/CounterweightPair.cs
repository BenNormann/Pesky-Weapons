using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Two pans on one rope over a pulley. The heavier pan sinks by `travel` and the lighter one rises by
    /// the same amount; inside `deadzone` they level out. Park the Hammer on one pan and the other pan
    /// carries a weapon up to a ledge you could not reach - then fly over as a soul and possess it.
    ///
    /// The host owns ONE number: the target offset (-1 level -1... in practice -1, 0 or +1). The pans'
    /// heights are then a pure function of (startOffset, targetOffset, startMs) and LevelClock.Ms, so
    /// every client draws the same movement from the same three values. Riders are carried by MassPan.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CounterweightPair : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [SerializeField] LevelClock clock;

        [Header("Pans")]
        [SerializeField] MassPan panA;
        [SerializeField] MassPan panB;

        [Header("Rule")]
        [Tooltip("Metres each pan moves from its authored height to its extreme. A goes DOWN when A is heavier.")]
        [SerializeField] float travel = 3f;
        [Tooltip("Mass difference below which the pans return to level.")]
        [SerializeField] float deadzone = 0.5f;
        [Tooltip("Metres per second the pans move while settling.")]
        [SerializeField] float easeSpeed = 1.5f;
        [Tooltip("Ease in and out of the new height instead of a constant rate.")]
        [SerializeField] bool smooth = true;

        float _startOffset;
        float _targetOffset;
        long _startMs;

        public int SceneId { get { return id; } }
        public MassPan PanA { get { return panA; } }
        public MassPan PanB { get { return panB; } }
        public float Travel { get { return travel; } }
        /// <summary>Host-owned: -1 (A up, B down), 0 (level) or +1 (A down, B up).</summary>
        public float TargetOffset { get { return _targetOffset; } }
        public float MassA { get { return panA != null ? panA.Mass : 0f; } }
        public float MassB { get { return panB != null ? panB.Mass : 0f; } }

        /// <summary>Pure: what the host would want given these two masses.</summary>
        public float WantedOffset(float massA, float massB)
        {
            float diff = massA - massB;
            if (Mathf.Abs(diff) < deadzone) return 0f;
            return diff > 0f ? 1f : -1f;
        }

        /// <summary>Pure: the offset at a clock time, given the host-owned target.</summary>
        public float OffsetAt(long ms)
        {
            float span = Mathf.Abs(_targetOffset - _startOffset) * travel;
            float duration = easeSpeed > 0.001f ? span / easeSpeed : 0f;
            if (duration <= 0.001f) return _targetOffset;
            float k = Mathf.Clamp01((float)((ms - _startMs) * 0.001 / duration));
            if (smooth) k = Mathf.SmoothStep(0f, 1f, k);
            return Mathf.Lerp(_startOffset, _targetOffset, k);
        }

        /// <summary>Authority only: the new target, and the clock time the move starts from.</summary>
        public void ApplyTarget(float target, long ms)
        {
            _startOffset = OffsetAt(ms);
            _targetOffset = target;
            _startMs = ms;
        }

        void FixedUpdate()
        {
            if (panA == null || panB == null) return;
            long ms = clock != null ? clock.Ms : 0L;
            float a = panA.Measure();
            float b = panB.Measure();
            if (authority != null) authority.ReportCounterweight(id, a, b, ms);

            float offset = OffsetAt(ms);
            panA.MoveTo(panA.BaseY - offset * travel);
            panB.MoveTo(panB.BaseY + offset * travel);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.72f, 0.94f, 0.9f);
            if (panA != null)
            {
                Gizmos.DrawLine(panA.transform.position + Vector3.up * travel, panA.transform.position - Vector3.up * travel);
                Gizmos.DrawWireCube(panA.transform.position, new Vector3(2.4f, 0.05f, 2.4f));
            }
            if (panB != null)
            {
                Gizmos.DrawLine(panB.transform.position + Vector3.up * travel, panB.transform.position - Vector3.up * travel);
                Gizmos.DrawWireCube(panB.transform.position, new Vector3(2.4f, 0.05f, 2.4f));
            }
            if (panA != null && panB != null) Gizmos.DrawLine(panA.transform.position, panB.transform.position);
        }
    }
}
