using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Session.Validators
{
    /// <summary>
    /// BAT_CLAIM to BAT_EVENT: friend ballistics v0. A player's weapon struck another player's remote
    /// view and claims a shove on them. The host checks both players hold a weapon, that their streamed
    /// poses are close enough to have touched, the rate per pair, and clamps the shove; the target's own
    /// peer applies it.
    /// </summary>
    public sealed class BatValidator : IIntentValidator
    {
        public const float MaxRange = 5f;
        public const int CooldownTicks = Protocol.Tick.PerSecond / 2;

        readonly Dictionary<int, uint> _lastBatTick = new Dictionary<int, uint>(16);

        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (!BatClaimMsg.TryDecode(payload, out var claim)) return;
            if (claim.targetSlot == fromSlot) return;
            var from = sim.Players[fromSlot];
            var target = sim.Players[claim.targetSlot];
            if (from == null || target == null || !from.present || !target.present) return;
            if (from.weaponId == Wire.NoId || target.weaponId == Wire.NoId) return;
            if ((from.pos - target.pos).sqrMagnitude > MaxRange * MaxRange) return;

            var key = (fromSlot << 8) | claim.targetSlot;
            if (_lastBatTick.TryGetValue(key, out var last) && tick - last < CooldownTicks) return;

            var max = sim.Data != null ? sim.Data.batMaxSpeed : 18f;
            var dv = claim.velocityChange;
            if (float.IsNaN(dv.x) || float.IsNaN(dv.y) || float.IsNaN(dv.z)) return;
            dv = Vector3.ClampMagnitude(dv, max);
            if (dv.sqrMagnitude < 0.01f) return;

            _lastBatTick[key] = tick;
            var msg = new BatEventMsg();
            msg.header = events.Header();
            msg.fromSlot = fromSlot;
            msg.targetSlot = claim.targetSlot;
            msg.velocityChange = dv;
            msg.point = claim.point;
            events.Emit(msg.Encode());
        }
    }
}
