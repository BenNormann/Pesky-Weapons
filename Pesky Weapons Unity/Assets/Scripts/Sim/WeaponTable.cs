using System.Collections.Generic;
using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Sim
{
    /// <summary>
    /// One weapon in the level, keyed by its stable scene id. Everything here is a host fact: who holds
    /// it, its hit points, whether it is broken and when it comes back, its home slot and its modifiers.
    /// The rest pose is where a released weapon was let go, so a late joiner can put it there.
    /// </summary>
    public sealed class WeaponState
    {
        public ushort id;
        public byte defId;
        public bool broken;
        public byte ownerSlot = Wire.NoSlot;
        public float hp;
        public float maxHp;
        public uint respawnTick;
        public ushort homeSlot = Wire.NoId;
        public readonly List<byte> mods = new List<byte>(4);
        /// <summary>Damage was dealt or taken up to this tick: leaving the weapon before it breaks it.</summary>
        public uint combatUntilTick;
        public bool hasRest;
        public Vector3 restPos;
        public Quaternion restRot = Quaternion.identity;
        public Vector3 restVel;

        public void Write(NetWriter w)
        {
            w.U16(id).U8(defId).Bool(broken).U8(ownerSlot).Hp(hp).Hp(maxHp).U32(respawnTick).U16(homeSlot);
            w.U32(combatUntilTick).Bool(hasRest).Pos(restPos).Quat(restRot);
            w.U8((byte)Mathf.Min(mods.Count, 255));
            for (var i = 0; i < mods.Count && i < 255; i++) w.U8(mods[i]);
        }

        public void Read(NetReader r)
        {
            id = r.U16();
            defId = r.U8();
            broken = r.Bool();
            ownerSlot = r.U8();
            hp = r.Hp();
            maxHp = r.Hp();
            respawnTick = r.U32();
            homeSlot = r.U16();
            combatUntilTick = r.U32();
            hasRest = r.Bool();
            restPos = r.Pos();
            restRot = r.Quat();
            restVel = Vector3.zero;
            mods.Clear();
            var n = r.U8();
            for (var i = 0; i < n && !r.Failed; i++) mods.Add(r.U8());
        }
    }

    /// <summary>Every weapon row, in the order the host registered them (so every peer snapshots the same bytes).</summary>
    public sealed class WeaponTable
    {
        readonly List<WeaponState> _rows = new List<WeaponState>(32);
        readonly Dictionary<ushort, WeaponState> _byId = new Dictionary<ushort, WeaponState>(32);

        public IReadOnlyList<WeaponState> Rows => _rows;
        public int Count => _rows.Count;

        public WeaponState Get(ushort id) => _byId.TryGetValue(id, out var row) ? row : null;

        public WeaponState GetOrAdd(ushort id, out bool added)
        {
            added = false;
            if (_byId.TryGetValue(id, out var row)) return row;
            row = new WeaponState();
            row.id = id;
            _rows.Add(row);
            _byId[id] = row;
            added = true;
            return row;
        }

        /// <summary>The weapon a slot holds, or null.</summary>
        public WeaponState OwnedBy(byte slot)
        {
            if (slot == Wire.NoSlot) return null;
            for (var i = 0; i < _rows.Count; i++) if (_rows[i].ownerSlot == slot && !_rows[i].broken) return _rows[i];
            return null;
        }

        public void Clear()
        {
            _rows.Clear();
            _byId.Clear();
        }

        public void Write(NetWriter w)
        {
            w.U16((ushort)_rows.Count);
            for (var i = 0; i < _rows.Count; i++) _rows[i].Write(w);
        }

        public void Read(NetReader r)
        {
            Clear();
            var n = r.U16();
            for (var i = 0; i < n && !r.Failed; i++)
            {
                var row = new WeaponState();
                row.Read(r);
                if (r.Failed || _byId.ContainsKey(row.id)) continue;
                _rows.Add(row);
                _byId[row.id] = row;
            }
        }
    }
}
