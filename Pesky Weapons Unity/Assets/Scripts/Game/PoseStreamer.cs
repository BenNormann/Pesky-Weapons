using Pesky.Protocol;
using Pesky.Session;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Streams the local player's BODY as POSE (0x01) on the raw SendNow path, 20 Hz. The body is the
    /// possessed weapon or, when free, the soul (PoseFlags.Soul). A pose only goes out when the body moved,
    /// turned or changed speed past an epsilon, or the one second keepalive is due. PoseFlags.Animate
    /// carries WeaponBody.IsAnimate, which is what the host's goblins perceive for a remote player.
    /// PoseFlags.Teleport rides the next three poses after a MagicDoor trip, a body swap (soul to weapon
    /// and back) or any jump too far to be motion, so remote views snap instead of interpolating.
    /// Owner authoritative: nobody ever corrects this body from the wire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoseStreamer : MonoBehaviour
    {
        const float SendHz = 20f;
        const float KeepaliveSeconds = 1f;
        const float MoveEpsilon = 0.01f;
        const float RotEpsilonDeg = 1f;
        const float VelEpsilon = 0.25f;
        const int TeleportRepeats = 3;

        [SerializeField] SessionRunner session;
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("Listened to for magic-door traversals of the local player.")]
        [SerializeField] WorldAuthority authority;

        static ushort s_seq;

        float _nextSend;
        float _lastSent = -999f;
        Vector3 _lastPos;
        Quaternion _lastRot = Quaternion.identity;
        Vector3 _lastVel;
        PoseFlags _lastFlags;
        bool _sentOnce;
        int _teleportLeft;

        void OnEnable()
        {
            if (authority != null) authority.MagicDoorTraversed += OnTraversed;
        }

        void OnDisable()
        {
            if (authority != null) authority.MagicDoorTraversed -= OnTraversed;
        }

        void OnTraversed(MagicDoorTraversal trip)
        {
            if (spawner != null && trip.soul != null && trip.soul == spawner.LocalSoul) Invalidate(true);
        }

        /// <summary>Forces the next frame to send. teleport = remote views must snap to it.</summary>
        public void Invalidate(bool teleport)
        {
            _nextSend = 0f;
            _sentOnce = false;
            if (teleport) _teleportLeft = TeleportRepeats;
        }

        void LateUpdate()
        {
            if (session == null || spawner == null || !session.HasSlot) return;
            PlayerSoul soul = spawner.LocalSoul;
            if (soul == null) return;
            float now = Time.unscaledTime;
            if (now < _nextSend) return;

            PoseFlags flags = PoseFlags.None;
            Vector3 pos;
            Quaternion rot;
            Vector3 vel;
            WeaponBody weapon = soul.Weapon;
            if (weapon != null && weapon.Body != null)
            {
                Rigidbody rb = weapon.Body;
                pos = rb.position;
                rot = rb.rotation;
                vel = rb.isKinematic ? Vector3.zero : rb.linearVelocity;
                if (weapon.IsAnimate) flags |= PoseFlags.Animate;
            }
            else
            {
                Rigidbody rb = soul.Body;
                pos = rb != null ? rb.position : soul.transform.position;
                rot = Quaternion.identity;
                vel = rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
                flags |= PoseFlags.Soul;
            }

            if (_sentOnce)
            {
                // A body swap, or a jump no launch could make, is a teleport whatever caused it.
                bool swapped = ((flags ^ _lastFlags) & PoseFlags.Soul) != 0;
                float jump = (pos - _lastPos).magnitude;
                float plausible = 3f + (vel.magnitude + _lastVel.magnitude) * (now - _lastSent);
                if (swapped || jump > plausible) _teleportLeft = TeleportRepeats;
            }

            bool changed = !_sentOnce
                || _teleportLeft > 0
                || flags != _lastFlags
                || (pos - _lastPos).sqrMagnitude > MoveEpsilon * MoveEpsilon
                || Quaternion.Angle(rot, _lastRot) > RotEpsilonDeg
                || (vel - _lastVel).sqrMagnitude > VelEpsilon * VelEpsilon;
            if (!changed && now - _lastSent < KeepaliveSeconds) return;

            if (_teleportLeft > 0)
            {
                flags |= PoseFlags.Teleport;
                _teleportLeft--;
            }

            _nextSend = now + 1f / SendHz;
            _lastSent = now;
            _lastPos = pos;
            _lastRot = rot;
            _lastVel = vel;
            _lastFlags = flags & ~PoseFlags.Teleport;
            _sentOnce = true;
            s_seq++;
            session.Session.SendNow(M0Messages.EncodePose(flags, pos, rot, vel, s_seq));
        }
    }
}
