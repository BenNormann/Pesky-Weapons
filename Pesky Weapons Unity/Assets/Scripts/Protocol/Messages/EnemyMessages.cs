using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Protocol
{
    /// <summary>
    /// 0x40 ENEMY_STATE, state: host simulated enemy rows, 10 Hz plus at once on any state change.
    /// type u8 | count u8 | rows. Row (25 bytes): enemyId u16 | kind u8 | pos f32 x3 | yaw u16 |
    /// hp u16 tenths | shieldHp u16 tenths | state u8 | flags u8 | targetWeaponId u16 (0xFFFF = none).
    /// A row is only sent when that enemy moved or changed, with a one second keepalive.
    /// </summary>
    public struct EnemyStateMsg
    {
        public const byte Id = MsgId.EnemyState;
        public const MsgKind Kind = MsgKind.State;
        public const int RowSize = 25;

        public struct Row
        {
            public ushort enemyId;
            public byte kind;
            public Vector3 pos;
            public float yaw;
            public float hp;
            public float shieldHp;
            public byte state;
            public EnemyFlags flags;
            public ushort targetWeaponId;
        }

        public List<Row> rows;

        public byte[] Encode()
        {
            var count = Wire.BatchCount(rows);
            var w = new NetWriter(Id, 2 + count * RowSize);
            w.U8(count);
            for (var i = 0; i < count; i++)
            {
                var row = rows[i];
                w.U16(row.enemyId).U8(row.kind).Pos(row.pos).Yaw(row.yaw).Hp(row.hp).Hp(row.shieldHp);
                w.U8(row.state).U8((byte)row.flags).U16(row.targetWeaponId);
            }
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out EnemyStateMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            var count = r.U8();
            msg.rows = new List<Row>(count);
            for (var i = 0; i < count && !r.Failed; i++)
            {
                var row = new Row();
                row.enemyId = r.U16();
                row.kind = r.U8();
                row.pos = r.Pos();
                row.yaw = r.Yaw();
                row.hp = r.Hp();
                row.shieldHp = r.Hp();
                row.state = r.U8();
                row.flags = (EnemyFlags)r.U8();
                row.targetWeaponId = r.U16();
                msg.rows.Add(row);
            }
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x41 HIT_CLAIM, intent: "the weapon I simulate struck this enemy".
    /// 19 bytes: type u8 | enemyId u16 | weaponId u16 | relativeSpeed u16 (1/100 m/s) | point f32 x3.
    /// The host checks the rate, the range against the claimant's streamed pose, and works the damage out
    /// itself from the WeaponDef; the claim never carries a damage number.
    /// </summary>
    public struct HitClaimMsg
    {
        public const byte Id = MsgId.HitClaim;
        public const MsgKind Kind = MsgKind.Intent;

        public ushort enemyId;
        public ushort weaponId;
        public float relativeSpeed;
        public Vector3 point;

        public byte[] Encode() => new NetWriter(Id, 19).U16(enemyId).U16(weaponId).Speed(relativeSpeed).Pos(point).ToArray();

        public static bool TryDecode(byte[] payload, out HitClaimMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.enemyId = r.U16();
            msg.weaponId = r.U16();
            msg.relativeSpeed = r.Speed();
            msg.point = r.Pos();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x42 ENEMY_HEALTH, event: the validated result of one hit.
    /// 35 bytes: type u8 | tick u32 | enemyId u16 | attackerSlot u8 | weaponId u16 | damage u16 tenths |
    /// newHp u16 tenths | newShieldHp u16 tenths | flags u8 (HitFlags) | knock i16 x3 | point f32 x3.
    /// </summary>
    public struct EnemyHealthMsg
    {
        public const byte Id = MsgId.EnemyHealth;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort enemyId;
        public byte attackerSlot;
        public ushort weaponId;
        public float damage;
        public float newHp;
        public float newShieldHp;
        public HitFlags flags;
        public Vector3 knock;
        public Vector3 point;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 35);
            header.Write(w);
            w.U16(enemyId).U8(attackerSlot).U16(weaponId).Hp(damage).Hp(newHp).Hp(newShieldHp);
            w.U8((byte)flags).Vel3(knock).Pos(point);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out EnemyHealthMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.enemyId = r.U16();
            msg.attackerSlot = r.U8();
            msg.weaponId = r.U16();
            msg.damage = r.Hp();
            msg.newHp = r.Hp();
            msg.newShieldHp = r.Hp();
            msg.flags = (HitFlags)r.U8();
            msg.knock = r.Vel3();
            msg.point = r.Pos();
            return !r.Failed;
        }
    }
}
