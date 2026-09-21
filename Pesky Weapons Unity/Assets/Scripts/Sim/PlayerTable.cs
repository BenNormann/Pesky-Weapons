using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Sim
{
    /// <summary>
    /// The Wire.MaxPlayers player slots and the messages that touch them.
    /// Slots exist from the start; PEER_SLOTS marks which are present. Stage 1
    /// carries only the slot table and the owner pose; weapons, runes and
    /// health rows arrive with their own messages in later stages.
    /// </summary>
    public sealed class PlayerTable
    {
        readonly PlayerState[] _slots = new PlayerState[Wire.MaxPlayers];

        public PlayerTable()
        {
            for (var i = 0; i < _slots.Length; i++) _slots[i] = new PlayerState((byte)i);
        }

        public int Count => _slots.Length;

        /// <summary>The row for a slot, or null when the slot is out of range.</summary>
        public PlayerState this[int slot] => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        public PlayerState Get(byte slot) => this[slot];

        public int PresentCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _slots.Length; i++) if (_slots[i].present) n++;
                return n;
            }
        }

        /// <summary>M0 TRANSFORM: the owner's position and yaw, latest wins.</summary>
public void SetPose(byte slot, PoseFlags flags, Vector3 pos, Quaternion rot, Vector3 vel, ushort seq)
        {
            var p = this[slot];
            if (p == null) return;
            p.poseFlags = flags;
            p.pos = pos;
            p.rot = rot;
            p.vel = vel;
            p.poseSeq = seq;
            p.poseCount++;
        }

        /// <summary>The whole slot table, absolute: rows not listed are cleared.</summary>
        public void Apply(in PeerSlotsMsg msg)
        {
            var listed = 0;
            if (msg.rows != null)
            {
                for (var i = 0; i < msg.rows.Count; i++)
                {
                    var row = msg.rows[i];
                    var p = this[row.slot];
                    if (p == null) continue;
                    listed |= 1 << row.slot;
                    if (!p.present || p.peerId != row.peerId) p.Reset();
                    p.present = true;
                    p.peerId = row.peerId ?? "";
                    p.name = row.name ?? "";
                    p.isHost = row.isHost;
                }
            }
            for (var i = 0; i < _slots.Length; i++)
            {
                if ((listed & (1 << i)) == 0 && _slots[i].present) _slots[i].Clear();
            }
        }

        public void Write(NetWriter w)
        {
            for (var i = 0; i < _slots.Length; i++) _slots[i].Write(w);
        }

        public void Read(NetReader r)
        {
            for (var i = 0; i < _slots.Length && !r.Failed; i++) _slots[i].Read(r);
        }
    }
}
