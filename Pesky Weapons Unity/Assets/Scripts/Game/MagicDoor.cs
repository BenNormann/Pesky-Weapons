using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A doorway whose glowing plane leads to a twin doorway somewhere else. Anything that matters - a
    /// possessed weapon, a loose weapon, a free soul - that crosses the plane FROM THE FRONT is moved to
    /// the twin, and its motion is turned by the rotation between the two frames, so "forward into A"
    /// becomes "forward out of B" at the same speed: a launch carries straight through. Two-way.
    /// Goblins are never moved (they are not travellers, and the alcove behind the plane is solid).
    ///
    /// It is a sensor only: it watches the travellers the WorldAuthority knows (no trigger collider, so
    /// the layer matrix does not matter and nothing can tunnel past it), and REQUESTS the traversal; the
    /// authority validates it (gate open, twin present, re-entry cooldown), applies it and raises
    /// MagicDoorTraversed, which is what the camera and the HUD react to.
    /// Local space: origin on the floor in the middle of the opening, +Z faces OUT of the doorway.
    /// Keep the root upright and at scale 1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicDoor : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [Tooltip("Both doors of a pair carry the same link id. It is the stable name of the link: when the twin lives in another scene the serialized reference below is empty and the pair is matched by this id.")]
        [SerializeField] int linkId;
        [SerializeField] WorldAuthority authority;
        [Tooltip("The other end. Empty only when the twin is in another scene (matched by link id).")]
        [OptionalRef][SerializeField] MagicDoor twin;
        [Tooltip("The Door whose DoorCondition gates this doorway: closed = solid panel, plane hidden, no travel. Empty = always passable.")]
        [OptionalRef][SerializeField] Door gate;
        [Tooltip("The glowing plane. Hidden while the gate is closed.")]
        [SerializeField] GameObject planeVisual;

        [Header("Opening (local: x across, y up from the floor)")]
        [SerializeField] float width = 2.9f;
        [SerializeField] float height = 3.45f;

        [Header("Travel")]
        [Tooltip("A traveller comes out with its centre at least this far in front of the twin's plane.")]
        [SerializeField] float exitClearance = 0.6f;
        [Tooltip("Seconds during which a traveller that just came through cannot go through any magic door again.")]
        [SerializeField] float reentryCooldown = 0.5f;
        [Tooltip("A step longer than this in one physics tick is a teleport or a respawn, never a crossing.")]
        [SerializeField] float maxStep = 2f;

        readonly Dictionary<Rigidbody, Vector3> _previous = new Dictionary<Rigidbody, Vector3>();
        bool _planeShown = true;

        public int SceneId { get { return id; } }
        public int LinkId { get { return linkId; } }
        public Door Gate { get { return gate; } }
        public float ReentryCooldown { get { return reentryCooldown; } }
        public float ExitClearance { get { return exitClearance; } }

        /// <summary>The other end: the serialized twin, or the registered door that shares this link id.</summary>
        public MagicDoor Twin
        {
            get
            {
                if (twin != null) return twin;
                return authority != null ? authority.FindLinkedMagicDoor(this) : null;
            }
        }

        public bool GateOpen { get { return gate == null || gate.IsOpen; } }
        /// <summary>Open and linked: a crossing will be accepted.</summary>
        public bool IsPassable { get { return GateOpen && Twin != null; } }

        void OnEnable()
        {
            _previous.Clear();
            ShowPlane(GateOpen);
        }

        void Update()
        {
            bool open = GateOpen;
            if (open != _planeShown) ShowPlane(open);
        }

        void ShowPlane(bool on)
        {
            _planeShown = on;
            if (planeVisual != null && planeVisual.activeSelf != on) planeVisual.SetActive(on);
        }

        // ---------------------------------------------------------------- geometry (pure)

        Vector3 ToLocal(Vector3 world)
        {
            return Quaternion.Inverse(transform.rotation) * (world - transform.position);
        }

        bool CrossedFromFront(Vector3 before, Vector3 now, out Vector3 localHit)
        {
            localHit = now;
            if (before.z <= 0f || now.z > 0f) return false;
            if ((now - before).sqrMagnitude > maxStep * maxStep) return false;
            float t = before.z / (before.z - now.z);
            localHit = Vector3.Lerp(before, now, t);
            return Mathf.Abs(localHit.x) <= width * 0.5f && localHit.y >= -0.25f && localHit.y <= height;
        }

        /// <summary>Pure: does the segment a to b go through the open plane from the front? (The trajectory preview ends there.)</summary>
        public bool SegmentCrosses(Vector3 a, Vector3 b, out Vector3 point)
        {
            Vector3 hit;
            bool crossed = CrossedFromFront(ToLocal(a), ToLocal(b), out hit);
            point = crossed ? transform.position + transform.rotation * hit : b;
            return crossed;
        }

        /// <summary>Pure: the rotation that turns "into this door" into "out of the exit door".</summary>
        public Quaternion TurnTo(MagicDoor exit)
        {
            return exit.transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(transform.rotation);
        }

        /// <summary>
        /// Pure: where a body at <paramref name="position"/> (whose centre is <paramref name="centre"/>) comes out.
        /// The offset from this door is carried over to the exit door, then pushed forward so the centre clears the plane.
        /// </summary>
        public Vector3 MapPosition(MagicDoor exit, Vector3 position, Vector3 centre)
        {
            Quaternion turn = TurnTo(exit);
            Vector3 exitPos = exit.transform.position;
            Vector3 mapped = exitPos + turn * (position - transform.position);
            Vector3 mappedCentre = exitPos + turn * (centre - transform.position);
            Vector3 forward = exit.transform.forward;
            float ahead = Vector3.Dot(mappedCentre - exitPos, forward);
            if (ahead < exit.exitClearance) mapped += forward * (exit.exitClearance - ahead);
            return mapped;
        }

        // ---------------------------------------------------------------- sensing

        void FixedUpdate()
        {
            if (authority == null) return;
            bool passable = IsPassable;

            IReadOnlyList<WeaponBody> weapons = authority.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponBody w = weapons[i];
                if (w == null || w.Body == null) continue;
                if (w.IsBroken) { _previous.Remove(w.Body); continue; }
                if (Watch(w.Body, w.Body.worldCenterOfMass, passable)) authority.RequestMagicDoorTraverse(this, w);
            }

            IReadOnlyList<PlayerSoul> souls = authority.Souls;
            for (int i = 0; i < souls.Count; i++)
            {
                PlayerSoul s = souls[i];
                if (s == null || s.Body == null) continue;
                if (s.IsPossessing) { _previous.Remove(s.Body); continue; }
                if (Watch(s.Body, s.Body.position, passable)) authority.RequestMagicDoorTraverse(this, s);
            }
        }

        bool Watch(Rigidbody rb, Vector3 world, bool passable)
        {
            Vector3 now = ToLocal(world);
            Vector3 before;
            bool had = _previous.TryGetValue(rb, out before);
            _previous[rb] = now;
            if (!had || !passable || rb.isKinematic) return false;
            Vector3 hit;
            return CrossedFromFront(before, now, out hit);
        }

        /// <summary>Authority only: a traveller was just put in front of this door; start tracking it from there.</summary>
        public void NoteArrival(Rigidbody rb, Vector3 world)
        {
            if (rb != null) _previous[rb] = ToLocal(world);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(0.66f, 0.27f, 0.95f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.02f));
            Gizmos.DrawLine(new Vector3(0f, height * 0.5f, 0f), new Vector3(0f, height * 0.5f, exitClearance + 0.6f));
            Gizmos.matrix = Matrix4x4.identity;
            if (twin != null) Gizmos.DrawLine(transform.position + Vector3.up * (height * 0.5f), twin.transform.position + Vector3.up * (twin.height * 0.5f));
        }
    }
}
