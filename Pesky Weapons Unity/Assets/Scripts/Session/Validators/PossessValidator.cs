using Pesky.Protocol;
using Pesky.Session.Rules;
using Pesky.Sim;

namespace Pesky.Session.Validators
{
    /// <summary>
    /// POSSESS_REQ and RELEASE_REQ. Two players can never hold one weapon because the host answers the
    /// requests one at a time against the weapon row: the first WEAPON_OWNER is applied to the host's sim
    /// before the second request is read, so the second finds the weapon taken and is dropped.
    /// A release while the weapon is in combat breaks it instead of dropping it.
    /// </summary>
    public sealed class PossessValidator : IIntentValidator
    {
        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (MessageInfo.IdOf(payload) == MsgId.PossessReq) OnPossess(fromSlot, payload, sim, events);
            else OnRelease(fromSlot, payload, sim, events);
        }

        static void OnPossess(byte fromSlot, byte[] payload, WorldSim sim, EventSink events)
        {
            if (!PossessReqMsg.TryDecode(payload, out var req)) return;
            var player = sim.Players[fromSlot];
            if (player == null || !player.present || player.weaponId != Wire.NoId) return;
            var w = sim.Weapons.Get(req.weaponId);
            if (w == null || w.broken || w.ownerSlot != Wire.NoSlot) return;

            var msg = new WeaponOwnerMsg();
            msg.header = events.Header();
            msg.weaponId = w.id;
            msg.ownerSlot = fromSlot;
            msg.hasPose = false;
            msg.rot = UnityEngine.Quaternion.identity;
            events.Emit(msg.Encode());
        }

        static void OnRelease(byte fromSlot, byte[] payload, WorldSim sim, EventSink events)
        {
            if (!ReleaseReqMsg.TryDecode(payload, out var req)) return;
            var w = sim.Weapons.Get(req.weaponId);
            if (w == null || w.broken || w.ownerSlot != fromSlot) return;

            if (sim.InCombat(w.id))
            {
                events.Emit(WeaponRule.Broken(events, w.id, BreakCause.ReleasedInCombat));
                return;
            }

            var msg = new WeaponOwnerMsg();
            msg.header = events.Header();
            msg.weaponId = w.id;
            msg.ownerSlot = Wire.NoSlot;
            msg.hasPose = true;
            msg.pos = req.pos;
            msg.rot = req.rot;
            msg.vel = req.vel;
            events.Emit(msg.Encode());
        }
    }
}
