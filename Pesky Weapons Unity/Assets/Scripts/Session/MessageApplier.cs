using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session
{
    /// <summary>
    /// The one switch from a payload to the matching WorldSim.Apply: every
    /// Event and State message the sim keeps, plus any stream it keeps (none
    /// yet). Used by the client router and by
    /// EventSink.Emit on the host, so both sides apply through one path.
    /// Also reads the effect tick out of an event without a full decode.
    /// </summary>
    public static class MessageApplier
    {
        /// <summary>Decodes and applies; false for an unknown id, a bad payload, or a message the sim does not keep.</summary>
        public static bool Apply(WorldSim sim, byte[] payload)
        {
            if (sim == null || payload == null || payload.Length == 0) return false;
            switch (payload[0])
            {
                case MsgId.SessionInfo: { if (!SessionInfoMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.TimeSync: { if (!TimeSyncMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.SessionPhase: { if (!SessionPhaseMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.PeerSlots: { if (!PeerSlotsMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.SessionEnd: { if (!SessionEndMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.WorldReset: { if (!WorldResetMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.WeaponState: { if (!WeaponStateMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.WeaponOwner: { if (!WeaponOwnerMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.WeaponBroken: { if (!WeaponBrokenMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.WeaponRespawned: { if (!WeaponRespawnedMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.WeaponDamaged: { if (!WeaponDamagedMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.BatEvent: { if (!BatEventMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.EnemyState: { if (!EnemyStateMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.EnemyHealth: { if (!EnemyHealthMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.KitState: { if (!KitStateMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.LabLayout: { if (!LabLayoutMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.LabSwapped: { if (!LabSwappedMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.RoundResult: { if (!RoundResultMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.PlayerDown: { if (!PlayerDownMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.PlayerRespawn: { if (!PlayerRespawnMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.Legend: { if (!LegendMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }                case MsgId.PadStroke: { if (!PadStrokeMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.PadClear: { if (!PadClearMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }

                
// One case per Pesky Weapons message the sim keeps goes here as
                // its stage lands: the player, enemy, door and pickup domains.
            }
            return false;
        }

        /// <summary>
        /// Decodes and applies a REPLY the host addressed to this peer alone: its own role, its own
        /// compass target. Kept apart from Apply because these are secrets. They are applied locally and
        /// are never re-sent, never snapshotted and never logged.
        /// </summary>
        public static bool ApplyReply(WorldSim sim, byte[] payload)
        {
            if (sim == null || payload == null || payload.Length == 0) return false;
            switch (payload[0])
            {
                case MsgId.RoleAssign: { if (!RoleAssignMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
                case MsgId.CompassTargets: { if (!CompassTargetsMsg.TryDecode(payload, out var m)) return false; sim.Apply(m); return true; }
            }
            return false;
        }

        /// <summary>
        /// The tick an Event payload takes effect, read straight out of its
        /// header without a full decode. Every Event's first field after the
        /// type byte is the EventHeader's u32 tick, so the offset is always 1.
        /// A message family with a wider header would break this.
        /// </summary>
        public static bool TryEventTick(byte[] payload, out uint tick)
        {
            tick = 0;
            if (payload == null || payload.Length < 5) return false;
            tick = (uint)(payload[1] | (payload[2] << 8) | (payload[3] << 16) | (payload[4] << 24));
            return true;
        }
    }
}
