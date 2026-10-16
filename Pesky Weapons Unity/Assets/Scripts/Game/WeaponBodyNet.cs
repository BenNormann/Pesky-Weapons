using System.Collections.Generic;
using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The netcode half of WeaponBody. Three things change once a level runs inside a NetSession:
    /// * NET DRIVEN: hit points, breaking and respawning are host facts. The WorldAuthority writes them in
    ///   from the session's events; the weapon no longer runs its own respawn timer.
    /// * REMOTE DRIVEN: while another player holds the weapon it is a kinematic body moved from that
    ///   player's streamed POSE, with its colliders on so local weapons bounce off it and can bat it. Its
    ///   ANIMATE state is the bit the owner streams, which is what the host's goblins perceive.
    /// * BATTING: a locally possessed weapon that strikes a remote driven one reports it (friend ballistics).
    /// </summary>
    public sealed partial class WeaponBody
    {
        WorldAuthority _net;
        bool _netDriven;
        bool _remoteDriven;
        bool _remoteAnimate;
        bool _remoteBraced;

        /// <summary>Standing up for a bat (Shift held on the ground): my own stance, or the streamed flag of a remote body.</summary>
        public bool IsBraced { get { return _remoteDriven ? _remoteBraced : _braced; } }
        Vector3 _remoteVelocity;
        Vector3 _preStepVelocity;

        public bool IsNetDriven { get { return _netDriven; } }
        /// <summary>Another player holds this weapon: it is kinematic and follows their POSE.</summary>
        public bool IsRemoteDriven { get { return _remoteDriven; } }
        /// <summary>Held by the player at this machine (not by a remote player's stand-in).</summary>
        public bool IsLocallyPossessed { get { return _possessor is PlayerSoul; } }
        /// <summary>The velocity the remote owner streamed (a kinematic body has none of its own).</summary>
        public Vector3 RemoteVelocity { get { return _remoteVelocity; } }
        /// <summary>The linear velocity going into the current physics step, i.e. before a collision changed it.</summary>
        public Vector3 PreStepVelocity { get { return _preStepVelocity; } }

        /// <summary>Called by the WorldAuthority for every weapon in its registry when the level runs in a session.</summary>
        public void SetNetDriven(WorldAuthority authority)
        {
            _net = authority;
            _netDriven = authority != null;
        }

        /// <summary>Authority only: hand the body to (or take it back from) a remote player's pose stream.</summary>
        public void SetRemoteDriven(bool on)
        {
            if (_remoteDriven == on) return;
            _remoteAnimate = false;
            _remoteBraced = false;
            _remoteVelocity = Vector3.zero;
            if (body == null || _broken)
            {
                _remoteDriven = on;
                return;
            }
            if (on)
            {
                ReleaseHold();
                Unstick();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                _remoteDriven = true;
            }
            else
            {
                _remoteDriven = false;
                body.isKinematic = false;
                body.WakeUp();
            }
        }

        /// <summary>The remote view feeds these from each POSE it takes.</summary>
        public void SetRemoteState(bool animate, bool braced, Vector3 velocity)
        {
            _remoteAnimate = animate;
            _remoteBraced = braced;
            _remoteVelocity = velocity;
            if (animate) MarkAnimated();
        }

        /// <summary>The remote view moves the kinematic body. snap = a teleport: no sweep, no interpolation streak.</summary>
        public void RemoteMove(Vector3 position, Quaternion rotation, bool snap)
        {
            if (!_remoteDriven || _broken || body == null) return;
            if (snap)
            {
                body.position = position;
                body.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                body.MovePosition(position);
                body.MoveRotation(rotation);
            }
        }

        /// <summary>Authority only: the host's hit point fact. Never breaks the weapon itself; WEAPON_BROKEN does that.</summary>
        public void SetHpFromNet(float hp)
        {
            if (_broken) return;
            hp = Mathf.Clamp(hp, 0f, MaxHp);
            if (Mathf.Approximately(hp, _hp)) return;
            bool dropped = hp < _hp;
            _hp = hp;
            if (dropped) NotifyCombat();
            if (HpChanged != null) HpChanged(this);
        }

        /// <summary>Authority only: replace the modifier list (a snapshot or a resync).</summary>
        public void SetModifiersFromNet(List<ModifierDef> list)
        {
            bool same = list.Count == modifiers.Count;
            for (int i = 0; same && i < list.Count; i++) same = list[i] == modifiers[i];
            if (same) return;
            modifiers.Clear();
            modifiers.AddRange(list);
            if (ModifiersChanged != null) ModifiersChanged(this);
        }

        /// <summary>Authority only: WEAPON_RESPAWNED.</summary>
        public void ApplyRespawn()
        {
            if (_broken) Respawn();
        }

        /// <summary>Authority only: a player on another machine let this weapon go here. Physics takes it from this pose.</summary>
        public void PlaceAtRest(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (_broken || _remoteDriven || body == null) return;
            Warp(position, rotation, velocity, Vector3.zero);
        }

        // ---- following the host (LOOSE_WEAPONS) ----

        // Closer than this, in metres and degrees, the local copy is left to its own physics.
        const float FollowDeadZone = 0.06f;
        const float FollowDeadAngle = 3f;
        // Each physics step removes this fraction of the remaining error (about 100 ms to settle at 50 Hz).
        const float FollowBlend = 0.3f;
        const float FollowVelocityBlend = 0.1f;
        // A weapon the host has lying still is put exactly there once it is further off than this.
        const float FollowRestDistance = 0.05f;
        const float FollowRestAngle = 3f;

        /// <summary>
        /// Client only: pulls this UNHELD weapon toward where the host's copy is (the host simulates loose weapons;
        /// every client simulates its own for feel and is held to the host's by this). Far off: snap. Lying still on
        /// the host: put exactly there, so the final resting poses agree. Otherwise blend, so a bounce does not
        /// stutter. Stuck in wood on the host: stick too, when there is wood where the host's blade is.
        /// target is already carried forward by the age of the host's row.
        /// </summary>
        public void FollowHost(Vector3 position, Quaternion rotation, Vector3 velocity, bool asleep, bool stuck, float snapDistance)
        {
            if (_broken || _remoteDriven || _carried || body == null || _possessor != null) return;
            // The host's copy is loose, so it has left its rack: this one has too.
            if (_held) ReleaseHold();
            if (_stuck && !stuck) Unstick();
            if (stuck)
            {
                if (_stuck) return;
                if (StickAtPose(position, rotation)) return;
            }
            if (body.isKinematic) return;   // anything else kinematic here (a carry, a frozen body) is not ours to move

            float distance = (position - body.position).magnitude;
            float angle = Quaternion.Angle(rotation, body.rotation);
            if (distance > snapDistance)
            {
                Warp(position, rotation, velocity, Vector3.zero);
                return;
            }
            if (asleep)
            {
                if (distance > FollowRestDistance || angle > FollowRestAngle) Warp(position, rotation, Vector3.zero, Vector3.zero);
                return;
            }
            if (distance <= FollowDeadZone && angle <= FollowDeadAngle) return;
            Vector3 p = Vector3.Lerp(body.position, position, FollowBlend);
            Quaternion q = Quaternion.Slerp(body.rotation, rotation, FollowBlend);
            body.position = p;
            body.rotation = q;
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, velocity, FollowVelocityBlend);
            body.WakeUp();
        }

        /// <summary>Puts the body at the host's stuck pose and sticks it into whatever wood its blade is in. False when there is none.</summary>
        bool StickAtPose(Vector3 position, Quaternion rotation)
        {
            if (def == null || !def.bladed) return false;
            Warp(position, rotation, Vector3.zero, Vector3.zero);
            Collider[] own = GetComponentsInChildren<Collider>();
            if (own.Length == 0) return false;
            Bounds box = own[0].bounds;
            for (int i = 1; i < own.Length; i++) box.Encapsulate(own[i].bounds);
            Collider[] hits = Physics.OverlapBox(box.center, box.extents + Vector3.one * 0.15f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c.attachedRigidbody == body) continue;
                WoodSurface wood = c.GetComponentInParent<WoodSurface>();
                if (wood == null || !wood.Stickable) continue;
                MirrorStick(wood, c, -TipDirection);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Holds the weapon in the wood where it already is (the host's copy is stuck here, with the tip already sunk):
        /// the same state a fresh hit sets in Stick, without the sink. Kept beside FollowHost rather than shared with
        /// Stick so the two cannot be edited into each other's way; if Stick gains state, add it here too.
        /// </summary>
        void MirrorStick(WoodSurface wood, Collider on, Vector3 normal)
        {
            Rigidbody hostBody = on.attachedRigidbody;
            Transform host = hostBody != null ? hostBody.transform : on.transform;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            _stuck = true;
            _stuckTo = host;
            _stuckFollows = hostBody != null;
            _stuckLocalPos = host.InverseTransformPoint(body.position);
            _stuckLocalRot = Quaternion.Inverse(host.rotation) * body.rotation;
            _stuckNormal = normal;
            _lastGroundTime = Time.fixedTime;
            if (Stuck != null) Stuck(this, wood);
        }

        /// <summary>Runs first in FixedUpdate. True = a remote driven body: nothing else in FixedUpdate applies to it.</summary>
        bool NetFixedUpdate()
        {
            if (_remoteDriven) return true;
            if (body != null && !body.isKinematic) _preStepVelocity = body.linearVelocity;
            return false;
        }

        /// <summary>My player's weapon struck another player's weapon: ask the authority to bat them.</summary>
        void NoteBat(Collision collision)
        {
            if (_net == null || _broken || !IsLocallyPossessed) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody other = rb.GetComponent<WeaponBody>();
            if (other == null || other._broken) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : body.worldCenterOfMass;
            if (other._remoteDriven)
            {
                _net.RequestBat(this, other, _preStepVelocity - other._remoteVelocity, point);
                return;
            }
            // A loose weapon standing braced by itself (autoBrace, the Bat Room test's bot): the host bats it directly.
            if (other.IsBraced && other._possessor == null && other.body != null)
                _net.BatLoose(this, other, _preStepVelocity - other.body.linearVelocity, point);
        }
    }
}
