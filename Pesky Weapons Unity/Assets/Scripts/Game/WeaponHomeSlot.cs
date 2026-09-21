using UnityEngine;

namespace Pesky.Game
{
    /// <summary>Where a weapon lives: it is authored here and it respawns here after breaking.</summary>
    public sealed class WeaponHomeSlot : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;

        public int Id { get { return id; } }
        public int SceneId { get { return id; } }
        public Vector3 Position { get { return transform.position; } }
        public Quaternion Rotation { get { return transform.rotation; } }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.24f, 0.86f, 0.94f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.5f, 0.1f, 1f));
        }
    }
}
