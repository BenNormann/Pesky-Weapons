using System.Collections.Generic;
using NUnit.Framework;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Tests
{
    /// <summary>LOOSE_WEAPONS (0x2F): the wire layout, the kind table, and what the sim keeps from a row.</summary>
    public sealed class LooseWeaponsTests
    {
        static WorldSim SimWithWeapon(ushort id, byte ownerSlot = Wire.NoSlot, bool broken = false, uint respawnTick = 0)
        {
            WorldSim sim = new WorldSim(null, 1u);
            WeaponState w = sim.Weapons.GetOrAdd(id, out _);
            w.ownerSlot = ownerSlot;
            w.broken = broken;
            w.respawnTick = respawnTick;
            return sim;
        }

        static LooseWeaponsMsg Msg(uint tick, ushort id, Vector3 pos, LooseFlags flags = LooseFlags.None)
        {
            LooseWeaponsMsg m = new LooseWeaponsMsg();
            m.tick = tick;
            m.rows = new List<LooseWeaponsMsg.Row>
            {
                new LooseWeaponsMsg.Row { weaponId = id, flags = flags, pos = pos, rot = Quaternion.Euler(0f, 90f, 0f), vel = new Vector3(1f, 2f, -3f) },
            };
            return m;
        }

        [Test]
        public void RoundTripsAndKeepsTheDocumentedSize()
        {
            LooseWeaponsMsg m = Msg(1234u, 7, new Vector3(1.5f, 2f, -3.25f), LooseFlags.Asleep | LooseFlags.Stuck);
            byte[] bytes = m.Encode();
            Assert.AreEqual(1 + 4 + 1 + LooseWeaponsMsg.RowSize, bytes.Length);
            Assert.AreEqual(MsgId.LooseWeapons, bytes[0]);

            LooseWeaponsMsg back;
            Assert.IsTrue(LooseWeaponsMsg.TryDecode(bytes, out back));
            Assert.AreEqual(1234u, back.tick);
            Assert.AreEqual(1, back.rows.Count);
            Assert.AreEqual((ushort)7, back.rows[0].weaponId);
            Assert.AreEqual(LooseFlags.Asleep | LooseFlags.Stuck, back.rows[0].flags);
            Assert.AreEqual(1.5f, back.rows[0].pos.x, 1e-4f);
            Assert.AreEqual(-3.25f, back.rows[0].pos.z, 1e-4f);
            Assert.AreEqual(2f, back.rows[0].vel.y, 0.01f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), back.rows[0].rot), 0.5f);
        }

        [Test]
        public void IsAStateMessageTheRouterKnows()
        {
            Assert.AreEqual(MsgKind.State, MessageInfo.KindOf(MsgId.LooseWeapons));
            Assert.IsTrue(MessageInfo.IsKnown(MsgId.LooseWeapons));
        }

        [Test]
        public void ARowSetsTheRestPoseOfAnUnheldWeaponWithoutBumpingRev()
        {
            WorldSim sim = SimWithWeapon(7);
            uint rev = sim.Rev;
            Assert.IsTrue(MessageApplier.Apply(sim, Msg(100u, 7, new Vector3(4f, 0.5f, 9f), LooseFlags.Asleep).Encode()));

            WeaponState w = sim.Weapons.Get(7);
            Assert.IsTrue(w.hasRest);
            Assert.AreEqual(4f, w.restPos.x, 1e-4f);
            Assert.AreEqual(100u, w.restTick);
            Assert.IsTrue(w.restAsleep);
            Assert.IsFalse(w.restStuck);
            Assert.AreEqual(rev, sim.Rev, "ten rows a second must not make every client rebuild its weapons");
        }

        [Test]
        public void ARowForAHeldOrBrokenWeaponIsDropped()
        {
            WorldSim held = SimWithWeapon(7, ownerSlot: 2);
            MessageApplier.Apply(held, Msg(100u, 7, Vector3.one).Encode());
            Assert.IsFalse(held.Weapons.Get(7).hasRest, "an owner event may have overtaken the row");

            WorldSim broken = SimWithWeapon(7, broken: true);
            MessageApplier.Apply(broken, Msg(100u, 7, Vector3.one).Encode());
            Assert.IsFalse(broken.Weapons.Get(7).hasRest);
        }

        [Test]
        public void ARowSampledBeforeTheWeaponLastBrokeIsDropped()
        {
            WorldSim sim = SimWithWeapon(7, respawnTick: 400u);
            MessageApplier.Apply(sim, Msg(300u, 7, Vector3.one).Encode());
            Assert.IsFalse(sim.Weapons.Get(7).hasRest, "a stale in-flight row must not pull a respawned weapon off its rack");

            MessageApplier.Apply(sim, Msg(401u, 7, Vector3.one).Encode());
            Assert.IsTrue(sim.Weapons.Get(7).hasRest);
        }

        [Test]
        public void AnOlderRowArrivingAfterANewerOneIsIgnored()
        {
            WorldSim sim = SimWithWeapon(7);
            MessageApplier.Apply(sim, Msg(200u, 7, new Vector3(5f, 0f, 0f)).Encode());
            MessageApplier.Apply(sim, Msg(150u, 7, new Vector3(-5f, 0f, 0f)).Encode());
            Assert.AreEqual(5f, sim.Weapons.Get(7).restPos.x, 1e-4f);
        }

        [Test]
        public void AnUnknownWeaponIsIgnored()
        {
            WorldSim sim = new WorldSim(null, 1u);
            Assert.DoesNotThrow(() => MessageApplier.Apply(sim, Msg(100u, 99, Vector3.one).Encode()));
            Assert.IsNull(sim.Weapons.Get(99));
        }
    }
}
