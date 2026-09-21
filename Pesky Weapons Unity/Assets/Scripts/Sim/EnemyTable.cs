using System.Collections.Generic;
using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Sim
{
    /// <summary>
    /// One host simulated enemy, keyed by its stable scene id. The NavMesh brain lives on the host only;
    /// every peer (the host included) keeps this row from ENEMY_STATE and ENEMY_HEALTH, and a client's
    /// enemy view is drawn from it.
    /// </summary>
    public sealed class EnemyState
    {
        public ushort id;
        public byte kind;
        public Vector3 pos;
        public float yaw;
        public float hp;
        public float shieldHp;
        public byte state;
        public EnemyFlags flags;
        public ushort targetWeaponId = Wire.NoId;
        /// <summary>Counts the ENEMY_STATE rows taken, so a view can tell a fresh sample from a repeat. Local only.</summary>
        public uint samples;

        public bool Alive => (flags & EnemyFlags.Alive) != 0;
        public bool Hostile => (flags & EnemyFlags.Hostile) != 0;

        public void Write(NetWriter w)
        {
            w.U16(id).U8(kind).Pos(pos).Yaw(yaw).Hp(hp).Hp(shieldHp).U8(state).U8((byte)flags).U16(targetWeaponId);
        }

        public void Read(NetReader r)
        {
            id = r.U16();
            kind = r.U8();
            pos = r.Pos();
            yaw = r.Yaw();
            hp = r.Hp();
            shieldHp = r.Hp();
            state = r.U8();
            flags = (EnemyFlags)r.U8();
            targetWeaponId = r.U16();
        }
    }

    public sealed class EnemyTable
    {
        readonly List<EnemyState> _rows = new List<EnemyState>(32);
        readonly Dictionary<ushort, EnemyState> _byId = new Dictionary<ushort, EnemyState>(32);

        public IReadOnlyList<EnemyState> Rows => _rows;
        public int Count => _rows.Count;

        public EnemyState Get(ushort id) => _byId.TryGetValue(id, out var row) ? row : null;

        public EnemyState GetOrAdd(ushort id)
        {
            if (_byId.TryGetValue(id, out var row)) return row;
            row = new EnemyState();
            row.id = id;
            _rows.Add(row);
            _byId[id] = row;
            return row;
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
                var row = new EnemyState();
                row.Read(r);
                if (r.Failed || _byId.ContainsKey(row.id)) continue;
                row.samples = 1;
                _rows.Add(row);
                _byId[row.id] = row;
            }
        }
    }
}
