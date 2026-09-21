using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Sim
{
    /// <summary>
    /// One player slot: a possessed weapon. Identity comes from PEER_SLOTS,
    /// the pose is owner streamed (TRANSFORM), and hp, alive, death and
    /// respawn are host facts. Views take alive or dead from here, never from
    /// the stream.
    ///
    /// Stage 1 (netcode port) keeps ATCK's position + yaw pose so the ported
    /// router and SendNow path compile unchanged. Stage 2 respecifies
    /// TRANSFORM as position + quaternion + velocity and this row follows.
    /// </summary>
    public sealed class PlayerState
    {
        public readonly byte slot;

        // ---- identity (PEER_SLOTS) ----
        public bool present;
        public string name = "";
        public string peerId = "";
        public bool isHost;

        // ---- owner stream (POSE 0x01) ----
        public Vector3 pos;
        public Quaternion rot = Quaternion.identity;
        public Vector3 vel;
        public PoseFlags poseFlags;
        public ushort poseSeq;
        /// <summary>Counts accepted poses so a view can tell a new sample from a repeat. Local only, never snapshotted.</summary>
        public uint poseCount;

        // ---- host fact (WEAPON_OWNER) ----
        /// <summary>The weapon this slot holds, or Wire.NoId while the player is a free soul.</summary>
        public ushort weaponId = Wire.NoId;

        public PlayerState(byte slot)
        {
            this.slot = slot;
        }

        /// <summary>A fresh occupant: alive, full hp, nothing else.</summary>
        public void Reset()
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            vel = Vector3.zero;
            poseFlags = PoseFlags.Soul;
            poseSeq = 0;
            poseCount = 0;
            weaponId = Wire.NoId;
        }

        public void Clear()
        {
            present = false;
            name = "";
            peerId = "";
            isHost = false;
            Reset();
        }

        public void Write(NetWriter w)
        {
            w.Bool(present).Str(name).Str(peerId).Bool(isHost);
            w.Pos(pos).Quat(rot).Vel3(vel).U8((byte)poseFlags).U16(weaponId);
        }

        public void Read(NetReader r)
        {
            present = r.Bool();
            name = r.Str();
            peerId = r.Str();
            isHost = r.Bool();
            pos = r.Pos();
            rot = r.Quat();
            vel = r.Vel3();
            poseFlags = (PoseFlags)r.U8();
            weaponId = r.U16();
        }
    }
}
