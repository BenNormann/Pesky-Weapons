using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Validators
{
    /// <summary>
    /// KIT_REQ to KIT_STATE. A client's own weapon did something to a kit piece. The plain checks happen
    /// here (the claimant really holds the weapon it names); whether the claim is true of the scene is
    /// asked of the Game-side IHostWorld, which also fills in the value and the clock millisecond.
    /// </summary>
    public sealed class KitValidator : IIntentValidator
    {
        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (!KitReqMsg.TryDecode(payload, out var req)) return;
            if (events.World == null) return;
            var player = sim.Players[fromSlot];
            if (player == null || !player.present) return;
            if (req.actor != Wire.NoId)
            {
                var w = sim.Weapons.Get(req.actor);
                if (w == null || w.broken || w.ownerSlot != fromSlot) return;
            }

            var msg = new KitStateMsg();
            msg.header = events.Header();
            msg.kind = req.kind;
            msg.pieceId = req.pieceId;
            msg.state = req.state;
            msg.actor = req.actor;
            if (!events.World.ValidateKit(fromSlot, req.speed, ref msg)) return;
            events.Emit(msg.Encode());
        }
    }
}
