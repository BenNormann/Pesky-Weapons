using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Session.Validators
{
    /// <summary>
    /// HIT_CLAIM to ENEMY_HEALTH. The claim says which enemy, which weapon, how fast and where; the host
    /// checks who may claim for that weapon, the per weapon per enemy rate, and the range between the
    /// claimant's streamed pose and the enemy row, then works the damage out itself from the WeaponDef
    /// (speed curve, modifiers, the shield boss's shield and its bladed bonus). The knockback the enemy
    /// takes is part of the result so every peer shows the same shove.
    /// </summary>
    public sealed class HitValidator : IIntentValidator
    {
        /// <summary>Claimant pose to enemy feet, metres. Generous: the row is up to 100 ms old and the pose up to 50 ms.</summary>
        public const float MaxRange = 6f;

        /// <summary>No weapon in this game gets near this; a faster claim is clamped, not refused.</summary>
        public const float MaxClaimSpeed = 60f;

        readonly Dictionary<uint, uint> _lastHitTick = new Dictionary<uint, uint>(64);

        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (!HitClaimMsg.TryDecode(payload, out var claim)) return;
            var data = sim.Data;
            if (data == null) return;
            var e = sim.Enemies.Get(claim.enemyId);
            if (e == null || !e.Alive) return;
            var w = sim.Weapons.Get(claim.weaponId);
            if (w == null || w.broken) return;
            var claimant = sim.Players[fromSlot];
            if (claimant == null || !claimant.present) return;

            // Who may claim: the player holding the weapon, or the host for a loose weapon (the host simulates those).
            var holds = w.ownerSlot == fromSlot;
            if (!holds && !(w.ownerSlot == Wire.NoSlot && claimant.isHost)) return;

            // Rate.
            var key = ((uint)w.id << 16) | e.id;
            var cooldown = (uint)Mathf.CeilToInt(data.hitCooldownSeconds * Protocol.Tick.PerSecond);
            if (_lastHitTick.TryGetValue(key, out var last) && tick - last < cooldown) return;

            // Range, against the pose the claimant streams (its body is the weapon while it holds one).
            if (holds && claimant.poseCount > 0 && (claimant.pos - e.pos).sqrMagnitude > MaxRange * MaxRange) return;

            // Damage, computed here and nowhere else.
            var def = data.Weapon(w.defId);
            if (def == null) return;
            var speed = Mathf.Min(claim.relativeSpeed, MaxClaimSpeed);
            var raw = def.damage * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(data.impactSpeedMin, data.impactSpeedMax, speed));
            for (var i = 0; i < w.mods.Count; i++)
            {
                var mod = data.Modifier(w.mods[i]);
                if (mod != null) raw *= mod.damageMultiplier;
            }
            if (raw <= 0f) return;

            var enemyDef = data.Enemy(e.kind);
            var msg = new EnemyHealthMsg();
            msg.header = events.Header();
            msg.enemyId = e.id;
            msg.attackerSlot = holds ? fromSlot : Wire.NoSlot;
            msg.weaponId = w.id;
            msg.damage = raw;
            msg.newHp = e.hp;
            msg.newShieldHp = e.shieldHp;
            msg.point = claim.point;

            if (e.shieldHp > 0f)
            {
                // While the shield holds only a heavy weapon gets through to it at all.
                msg.flags |= HitFlags.HitShield;
                var need = enemyDef != null ? enemyDef.shieldMinMass : 8f;
                if (def.mass < need)
                {
                    msg.damage = 0f;
                    msg.flags |= HitFlags.Blocked;
                }
                else
                {
                    msg.newShieldHp = Mathf.Max(0f, e.shieldHp - raw);
                    if (msg.newShieldHp <= 0f) msg.flags |= HitFlags.ShieldBroke;
                }
            }
            else
            {
                if (def.bladed && enemyDef != null && enemyDef.bladedDamageBonus > 0f) msg.damage = raw * enemyDef.bladedDamageBonus;
                msg.newHp = Mathf.Max(0f, e.hp - msg.damage);
                if (msg.newHp <= 0f) msg.flags |= HitFlags.Died;
            }

            var height = enemyDef != null ? enemyDef.height : 1.4f;
            var dir = e.pos + Vector3.up * (height * 0.5f) - claim.point;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = e.pos - claimant.pos;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
            var perDamage = enemyDef != null ? enemyDef.knockbackPerDamage : 0.25f;
            var maxKnock = enemyDef != null ? enemyDef.maxKnockbackSpeed : 9f;
            msg.knock = dir * Mathf.Min(msg.damage * perDamage, maxKnock);

            _lastHitTick[key] = tick;
            events.Emit(msg.Encode());
        }
    }
}
