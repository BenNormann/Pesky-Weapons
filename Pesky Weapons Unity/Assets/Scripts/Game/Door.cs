using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A door panel on the World layer, so it blocks weapons AND the free soul (the Soul layer only
    /// collides with World). It never opens itself: the WorldAuthority validates its DoorCondition and
    /// raises DoorOpened, and this view reacts to that event by sliding the panel sideways into the wall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Door : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [SerializeField] DoorCondition condition;
        [Tooltip("The sliding panel. It starts at its closed local position.")]
        [SerializeField] Transform panel;
        [Tooltip("Local offset the panel slides to when the door opens (into the wall beside the doorway).")]
        [SerializeField] Vector3 openOffset = new Vector3(3.1f, 0f, 0f);
        [SerializeField] float slideSeconds = 0.8f;

        Vector3 _closedLocal;
        bool _open;
        float _t;

        public int SceneId { get { return id; } }
        public DoorCondition Condition { get { return condition; } }
        public bool IsOpen { get { return _open; } }
        /// <summary>1 = fully slid away.</summary>
        public float Openness { get { return _t; } }

        void Awake()
        {
            if (panel != null) _closedLocal = panel.localPosition;
        }

        void OnEnable()
        {
            if (authority != null) authority.DoorOpened += OnDoorOpened;
        }

        void OnDisable()
        {
            if (authority != null) authority.DoorOpened -= OnDoorOpened;
        }

        public bool ConditionSatisfied(WorldAuthority asker)
        {
            return condition != null && condition.IsSatisfied(asker);
        }

        /// <summary>Authority only: flips the shared state. The slide happens on the DoorOpened event.</summary>
        public void ApplyOpen()
        {
            _open = true;
        }

        void OnDoorOpened(Door door)
        {
            if (door == this) _open = true;
        }

        void Update()
        {
            if (panel == null) return;
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            float step = slideSeconds > 0.001f ? Time.deltaTime / slideSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);
            panel.localPosition = _closedLocal + openOffset * _t;
        }

        void OnDrawGizmosSelected()
        {
            if (panel == null) return;
            Gizmos.color = new Color(0.59f, 0.43f, 0.27f, 0.9f);
            Gizmos.DrawLine(panel.position, panel.parent != null
                ? panel.parent.TransformPoint(panel.localPosition + openOffset)
                : panel.position + openOffset);
        }
    }
}
