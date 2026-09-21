using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One authored room of the labyrinth. It carries its ROOM ID (its index in LabyrinthDef.rooms), the
    /// four doorways in Heading order (0 north, 1 east, 2 south, 3 west), the box that says what counts as
    /// being inside it, and the spot a player is put when they start or respawn here.
    ///
    /// A room never moves. What moves is the grid table: which CELL this room currently sits in, which the
    /// LabyrinthDirector looks up. Rooms are placed far apart in world space and are joined only by their
    /// doorways.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabyrinthRoom : MonoBehaviour
    {
        [Tooltip("This room's index in LabyrinthDef.rooms. Unique in the scene; it travels on the wire.")]
        [SerializeField] int roomId;

        [Tooltip("The four doorways, in Heading order: 0 north, 1 east, 2 south, 3 west.")]
        [SerializeField] MagicDoor[] doorways = new MagicDoor[4];

        [Tooltip("The room's footprint: a point inside this box is inside this room. No collider behaviour is used, so leave it a trigger or disabled.")]
        [SerializeField] BoxCollider footprint;

        [Tooltip("Where a player who starts or respawns in this room is put. Empty = the room root.")]
        [SerializeField] Transform anchor;

        public int RoomId { get { return roomId; } }

        public Transform Anchor { get { return anchor != null ? anchor : transform; } }

        /// <summary>One of the four doorways, or null when that side was left unbuilt.</summary>
        public MagicDoor Doorway(int dir)
        {
            if (doorways == null || dir < 0 || dir >= doorways.Length) return null;
            return doorways[dir];
        }

        /// <summary>Is this point inside the room? False when no footprint was set, which keeps a half-built room out of every query.</summary>
        public bool Contains(Vector3 world)
        {
            if (footprint == null) return false;
            Vector3 local = footprint.transform.InverseTransformPoint(world) - footprint.center;
            Vector3 half = footprint.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void OnDrawGizmosSelected()
        {
            if (footprint == null) return;
            Gizmos.matrix = footprint.transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(footprint.center, footprint.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
