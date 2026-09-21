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
        public void SetRemoteState(bool animate, Vector3 velocity)
        {
            _remoteAnimate = animate;
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
            if (other == null || !other._remoteDriven || other._broken) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : body.worldCenterOfMass;
            _net.RequestBat(this, other, _preStepVelocity - other._remoteVelocity, point);
        }
    }
}
