using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// The host's weapon bookkeeping, once per tick: a weapon whose hit points reached zero breaks
    /// (WEAPON_BROKEN), a broken weapon whose respawn tick has come returns (WEAPON_RESPAWNED), and a
    /// weapon whose owner's slot emptied is set free (WEAPON_OWNER with no owner). Reads the sim and
    /// emits; it never writes a row itself.
    /// </summary>
    public sealed class WeaponRule : IHostRule
    {
        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            var rows = sim.Weapons.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                var w = rows[i];
                if (w.broken)
                {
                    if (tick >= w.respawnTick)
                    {
                        var back = new WeaponRespawnedMsg();
                        back.header = events.Header();
                        back.weaponId = w.id;
                        events.Emit(back.Encode());
                    }
                    continue;
                }
                if (w.maxHp > 0f && w.hp <= 0f)
                {
                    events.Emit(Broken(events, w.id, BreakCause.Damage));
                    continue;
                }
                if (w.ownerSlot == Wire.NoSlot) continue;
                var owner = sim.Players[w.ownerSlot];
                if (owner != null && owner.present) continue;
                var free = new WeaponOwnerMsg();
                free.header = events.Header();
                free.weaponId = w.id;
                free.ownerSlot = Wire.NoSlot;
                free.hasPose = false;
                events.Emit(free.Encode());
            }
        }

        /// <summary>The one place a WEAPON_BROKEN is built, so the respawn delay is the same whoever breaks it.</summary>
        public static byte[] Broken(EventSink events, ushort weaponId, BreakCause cause)
        {
            var msg = new WeaponBrokenMsg();
            msg.header = events.Header();
            msg.weaponId = weaponId;
            msg.cause = cause;
            msg.respawnTick = msg.header.tick + WorldSim.RespawnDelayTicks;
            return msg.Encode();
        }
    }
}
