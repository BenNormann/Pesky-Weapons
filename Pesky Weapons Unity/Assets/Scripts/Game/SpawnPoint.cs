using UnityEngine;

namespace Pesky.Game
{
    /// <summary>A named, identified place to spawn something (souls, enemies). Pure authoring data.</summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour, ISceneId
    {
        public enum Kind { Soul = 0, Enemy = 1, Weapon = 2, Probe = 3 }

        [SerializeField] int id;
        [SerializeField] Kind kind = Kind.Soul;

        public int SceneId { get { return id; } }
        public Kind SpawnKind { get { return kind; } }
        public Vector3 Position { get { return transform.position; } }
        public Quaternion Rotation { get { return transform.rotation; } }
        public float Yaw { get { return transform.eulerAngles.y; } }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Soul
                ? new Color(0.24f, 0.86f, 0.94f, 0.9f)
                : new Color(0.24f, 0.63f, 0.27f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
