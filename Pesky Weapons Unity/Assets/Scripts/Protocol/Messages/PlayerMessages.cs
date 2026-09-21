using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Protocol
{
    /// <summary>0x20 POSSESS_REQ, intent: "let me have this weapon". 3 bytes: type u8 | weaponId u16.</summary>
    public struct PossessReqMsg
    {
        public const byte Id = MsgId.PossessReq;
        public const MsgKind Kind = MsgKind.Intent;

        public ushort weaponId;

        public byte[] Encode() => new NetWriter(Id, 3).U16(weaponId).ToArray();

        public static bool TryDecode(byte[] payload, out PossessReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.weaponId = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x21 RELEASE_REQ, intent: "I am leaving this weapon, here is where it is".
    /// 25 bytes: type u8 | weaponId u16 | pos f32 x3 | rot u32 | vel i16 x3.
    /// The host decides whether it drops or breaks (in combat it breaks).
    /// </summary>
    public struct ReleaseReqMsg
    {
        public const byte Id = MsgId.ReleaseReq;
        public const MsgKind Kind = MsgKind.Intent;

        public ushort weaponId;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 vel;

        public byte[] Encode() => new NetWriter(Id, 25).U16(weaponId).Pos(pos).Quat(rot).Vel3(vel).ToArray();

        public static bool TryDecode(byte[] payload, out ReleaseReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.weaponId = r.U16();
            msg.pos = r.Pos();
            msg.rot = r.Quat();
            msg.vel = r.Vel3();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x22 WEAPON_OWNER, event: who holds a weapon from this tick on.
    /// 31 bytes: type u8 | tick u32 | weaponId u16 | ownerSlot u8 (0xFF = free) | hasPose u8 |
    /// pos f32 x3 | rot u32 | vel i16 x3. The pose is the loose weapon's rest pose and only means
    /// something when hasPose is 1 (a release).
    /// </summary>
    public struct WeaponOwnerMsg
    {
        public const byte Id = MsgId.WeaponOwner;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort weaponId;
        public byte ownerSlot;
        public bool hasPose;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 vel;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 31);
            header.Write(w);
            w.U16(weaponId).U8(ownerSlot).Bool(hasPose).Pos(pos).Quat(rot).Vel3(vel);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out WeaponOwnerMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.weaponId = r.U16();
            msg.ownerSlot = r.U8();
            msg.hasPose = r.Bool();
            msg.pos = r.Pos();
            msg.rot = r.Quat();
            msg.vel = r.Vel3();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x23 WEAPON_STATE, state: absolute weapon rows, sent when the host registers a level's weapons.
    /// type u8 | count u8 | rows. Row (16 + n bytes): weaponId u16 | defId u8 | flags u8 | ownerSlot u8 |
    /// hp u16 tenths | maxHp u16 tenths | respawnTick u32 | homeSlot u16 | modCount u8 | modifierId u8 x n.
    /// On a row the sim already has, only hp, maxHp and the modifiers are taken: ownership and breaking
    /// belong to their events.
    /// </summary>
    public struct WeaponStateMsg
    {
        public const byte Id = MsgId.WeaponState;
        public const MsgKind Kind = MsgKind.State;

        public struct Row
        {
            public ushort weaponId;
            public byte defId;
            public WeaponFlags flags;
            public byte ownerSlot;
            public float hp;
            public float maxHp;
            public uint respawnTick;
            public ushort homeSlot;
            public byte[] mods;
        }

        public List<Row> rows;

        public byte[] Encode()
        {
            var count = Wire.BatchCount(rows);
            var w = new NetWriter(Id, 2 + count * 18);
            w.U8(count);
            for (var i = 0; i < count; i++)
            {
                var row = rows[i];
                var n = row.mods != null ? Mathf.Min(row.mods.Length, 255) : 0;
                w.U16(row.weaponId).U8(row.defId).U8((byte)row.flags).U8(row.ownerSlot);
                w.Hp(row.hp).Hp(row.maxHp).U32(row.respawnTick).U16(row.homeSlot).U8((byte)n);
                for (var m = 0; m < n; m++) w.U8(row.mods[m]);
            }
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out WeaponStateMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            var count = r.U8();
            msg.rows = new List<Row>(count);
            for (var i = 0; i < count && !r.Failed; i++)
            {
                var row = new Row();
                row.weaponId = r.U16();
                row.defId = r.U8();
                row.flags = (WeaponFlags)r.U8();
                row.ownerSlot = r.U8();
                row.hp = r.Hp();
                row.maxHp = r.Hp();
                row.respawnTick = r.U32();
                row.homeSlot = r.U16();
                row.mods = r.Bytes(r.U8());
                msg.rows.Add(row);
            }
            return !r.Failed;
        }
    }

    /// <summary>0x24 WEAPON_BROKEN, event. 12 bytes: type u8 | tick u32 | weaponId u16 | cause u8 | respawnTick u32.</summary>
    public struct WeaponBrokenMsg
    {
        public const byte Id = MsgId.WeaponBroken;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort weaponId;
        public BreakCause cause;
        public uint respawnTick;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 12);
            header.Write(w);
            w.U16(weaponId).U8((byte)cause).U32(respawnTick);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out WeaponBrokenMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.weaponId = r.U16();
            msg.cause = (BreakCause)r.U8();
            msg.respawnTick = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>0x25 WEAPON_RESPAWNED, event. 7 bytes: type u8 | tick u32 | weaponId u16.</summary>
    public struct WeaponRespawnedMsg
    {
        public const byte Id = MsgId.WeaponRespawned;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort weaponId;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 7);
            header.Write(w);
            w.U16(weaponId);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out WeaponRespawnedMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.weaponId = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x26 WEAPON_DAMAGED, event: an enemy attack (or a hazard) landed on a weapon.
    /// 19 bytes: type u8 | tick u32 | weaponId u16 | enemyId u16 (0xFFFF = hazard) | damage u16 tenths |
    /// newHp u16 tenths | knock i16 x3 (1/256 m/s, a velocity change). Whoever simulates the weapon
    /// (its owner, or the host while it is loose) applies the knock locally.
    /// </summary>
    public struct WeaponDamagedMsg
    {
        public const byte Id = MsgId.WeaponDamaged;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort weaponId;
        public ushort enemyId;
        public float damage;
        public float newHp;
        public Vector3 knock;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 19);
            header.Write(w);
            w.U16(weaponId).U16(enemyId).Hp(damage).Hp(newHp).Vel3(knock);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out WeaponDamagedMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.weaponId = r.U16();
            msg.enemyId = r.U16();
            msg.damage = r.Hp();
            msg.newHp = r.Hp();
            msg.knock = r.Vel3();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x27 BAT_CLAIM, intent: "my weapon just struck that player's weapon, shove them like this".
    /// 20 bytes: type u8 | targetSlot u8 | velocityChange i16 x3 (1/256 m/s) | point f32 x3.
    /// </summary>
    public struct BatClaimMsg
    {
        public const byte Id = MsgId.BatClaim;
        public const MsgKind Kind = MsgKind.Intent;

        public byte targetSlot;
        public Vector3 velocityChange;
        public Vector3 point;

        public byte[] Encode() => new NetWriter(Id, 20).U8(targetSlot).Vel3(velocityChange).Pos(point).ToArray();

        public static bool TryDecode(byte[] payload, out BatClaimMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.targetSlot = r.U8();
            msg.velocityChange = r.Vel3();
            msg.point = r.Pos();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x28 BAT_EVENT, event: the validated bat. The target's owner applies the velocity change.
    /// 25 bytes: type u8 | tick u32 | fromSlot u8 | targetSlot u8 | velocityChange i16 x3 | point f32 x3.
    /// </summary>
    public struct BatEventMsg
    {
        public const byte Id = MsgId.BatEvent;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte fromSlot;
        public byte targetSlot;
        public Vector3 velocityChange;
        public Vector3 point;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 25);
            header.Write(w);
            w.U8(fromSlot).U8(targetSlot).Vel3(velocityChange).Pos(point);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out BatEventMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.fromSlot = r.U8();
            msg.targetSlot = r.U8();
            msg.velocityChange = r.Vel3();
            msg.point = r.Pos();
            return !r.Failed;
        }
    }
}
