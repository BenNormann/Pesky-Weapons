using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A goblin-only gate. A solid panel that blocks weapons and souls, and slides open while one of its
    /// porters is close enough AND carrying something. That is the whole Carry Room puzzle: you cannot
    /// open it, so you play dead, let the porter pick you up, and it opens the gate for itself.
    ///
    /// It does NOT latch - it shuts again behind the porter. The open flag is host-owned
    /// (WorldAuthority.RequestSetPorterGate); the panel slide is a view.
    /// Goblins walk with a NavMeshAgent and are not stopped by the panel, so other goblins can pass a
    /// shut gate. Keep a porter gate on the porter's route and nowhere a fight can reach.
    /// The panel must NOT be marked static.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PorterGate : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;

        [Header("Rule")]
        [Tooltip("The porters this gate opens for.")]
        [SerializeField] GoblinBrain[] porters = new GoblinBrain[0];
        [Tooltip("A porter this close opens it.")]
        [SerializeField] float openRange = 4f;
        [Tooltip("Open only for a porter that is CARRYING something. An empty-handed porter walks around.")]
        [SerializeField] bool requireCarrying = true;

        [Header("Panel")]
        [Tooltip("The solid panel. Slides by openOffset while the gate is open.")]
        [SerializeField] Transform panel;
        [SerializeField] Vector3 openOffset = new Vector3(0f, 3.6f, 0f);
        [SerializeField] float slideSeconds = 0.6f;

        Vector3 _shutLocalPosition;
        bool _open;
        float _shown;

        public int SceneId { get { return id; } }
        public bool IsOpen { get { return _open; } }
        public float OpenRange { get { return openRange; } }
        public int PorterCount { get { return porters != null ? porters.Length : 0; } }
        void Awake()
        {
            if (panel != null) _shutLocalPosition = panel.localPosition;
        }

        /// <summary>Pure: should the gate be open right now?</summary>
        public bool WantsOpen()
        {
            float r2 = openRange * openRange;
            for (int i = 0; i < porters.Length; i++)
            {
                GoblinBrain g = porters[i];
                if (g == null || g.IsDead) continue;
                if (requireCarrying && !g.IsCarrying) continue;
                if ((g.transform.position - transform.position).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        void FixedUpdate()
        {
            if (authority == null) return;
            bool want = WantsOpen();
            if (want != _open) authority.RequestSetPorterGate(id, want);
        }

        /// <summary>Authority only.</summary>
        public void ApplyOpen(bool open)
        {
            _open = open;
        }

        void Update()
        {
            if (panel == null) return;
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_shown, target)) return;
            float step = slideSeconds > 0.001f ? Time.deltaTime / slideSeconds : 1f;
            _shown = Mathf.MoveTowards(_shown, target, step);
            panel.localPosition = _shutLocalPosition + openOffset * Mathf.SmoothStep(0f, 1f, _shown);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.98f, 0.62f, 0.20f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, openRange);
        }
    }
}
