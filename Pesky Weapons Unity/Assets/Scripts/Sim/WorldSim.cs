using Pesky.Data;
using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Sim
{
    /// <summary>
    /// The shared world state every peer runs. Clock free by contract: AdvanceTo takes the tick as a
    /// parameter and the sim never reads a clock, which is what makes it testable in EditMode.
    ///
    /// Four tables ride the snapshot: players (slots, owner streamed pose, the weapon a slot holds),
    /// weapons (owner, hp, broken, respawn, home slot, modifiers, rest pose), enemies (host simulated
    /// rows) and kit (the last KIT_STATE of every piece that changed). Every change arrives through an
    /// Apply overload, reached through MessageApplier on every peer, the host included. The sim never
    /// decides anything: host rules and validators decide, then emit.
    /// </summary>
    public sealed class WorldSim
    {
        /// <summary>A broken weapon returns to its home slot after this long (10 s).</summary>
        public const uint RespawnDelayTicks = 10 * Protocol.Tick.PerSecond;

        /// <summary>Damage dealt or taken keeps a weapon "in combat" for this long (5 s).</summary>
        public const uint CombatTicks = 5 * Protocol.Tick.PerSecond;

        public GameData Data { get; }

        public uint Tick { get; private set; }
        public uint WorldSeed { get; private set; }
        public SessionPhase Phase { get; private set; }
        public SessionEndReason EndReason { get; private set; }
        public int FinalScore { get; private set; }

        /// <summary>Which floor of the labyrinth the run is on, 1 based.</summary>
        public byte FloorId { get; private set; }

        /// <summary>The tick the current phase began.</summary>
        public uint PhaseStartTick { get; private set; }

        /// <summary>The last host room milliseconds TIME_SYNC carried; the clock's coarse check owns the real estimate.</summary>
        public uint HostRoomMs { get; private set; }

        /// <summary>The level the host last loaded (WORLD_RESET). 0 until a level registers.</summary>
        public uint SceneHash { get; private set; }

        /// <summary>
        /// Bumped whenever the tables were replaced wholesale (a snapshot, a WORLD_RESET, a weapon
        /// registration). Game compares it each frame and rebuilds its views from the rows when it moved.
        /// Local only, never snapshotted.
        /// </summary>
        public uint Rev { get; private set; }

        /// <summary>The host's world RNG, reseeded on every peer from SESSION_INFO so cosmetic draws match.</summary>
        public Rng Rng { get; }

        public PlayerTable Players { get; }
        public WeaponTable Weapons { get; }
        public EnemyTable Enemies { get; }
        public KitTable Kit { get; }

        /// <summary>The labyrinth: the cell to room table, legend, who is down, and how the round ended.</summary>
        public LabyrinthState Labyrinth { get; }
        /// <summary>The team's shared scratch pad: the drawing everybody can see and add to. Public, never a secret.</summary>
        public ScratchPadState Pad { get; }


        public WorldSim(GameData data, uint seed)
        {
            Data = data;
            WorldSeed = seed;
            Rng = new Rng(seed);
            Players = new PlayerTable();
            Weapons = new WeaponTable();
            Enemies = new EnemyTable();
            Kit = new KitTable();
            Labyrinth = new LabyrinthState();
            Labyrinth.Rebuild(data != null ? data.labyrinth : null, seed);
            Pad = new ScratchPadState();
            Pad.Configure(data != null ? data.labyrinth : null);

            Phase = SessionPhase.Lobby;
            EndReason = SessionEndReason.HostEnded;
            FloorId = 1;
        }

        /// <summary>Steps the world to a tick. Time is a parameter; the sim never reads a clock. Nothing in the tables moves on its own: enemies are host rows and weapons are physics.</summary>
        public void AdvanceTo(uint tick)
        {
            if (tick <= Tick) return;
            Tick = tick;
        }

        // ---- queries ----

        /// <summary>True while leaving this weapon would break it: it dealt or took damage lately, or a hostile enemy is after it.</summary>
        public bool InCombat(ushort weaponId)
        {
            var w = Weapons.Get(weaponId);
            if (w == null) return false;
            if (Tick < w.combatUntilTick) return true;
            var rows = Enemies.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                var e = rows[i];
                if (e.Alive && e.Hostile && e.targetWeaponId == weaponId) return true;
            }
            return false;
        }

        // ---- session messages ----

        public void Apply(in SessionInfoMsg msg)
        {
            if (msg.worldSeed != 0 && msg.worldSeed != WorldSeed)
            {
                WorldSeed = msg.worldSeed;
                Rng.State = msg.worldSeed;
                // The layout is a function of the seed, so a client that learns the real seed generates
                // the same labyrinth the host did. LAB_LAYOUT then confirms it cell for cell.
                Labyrinth.Rebuild(Data != null ? Data.labyrinth : null, msg.worldSeed);
            }
            if (msg.floorId != 0) FloorId = msg.floorId;
            SetPhase(msg.phase, msg.phaseStartTick);
        }

        public void Apply(in TimeSyncMsg msg)
        {
            HostRoomMs = msg.hostRoomMs;
        }

        public void Apply(in SessionPhaseMsg msg)
        {
            if (msg.floorId != 0) FloorId = msg.floorId;
            SetPhase(msg.phase, msg.header.tick);
        }

        public void Apply(in PeerSlotsMsg msg)
        {
            Players.Apply(msg);
        }

        public void Apply(in SessionEndMsg msg)
        {
            EndReason = msg.reason;
            FinalScore = msg.finalScore;
            SetPhase(SessionPhase.Ended, msg.header.tick);
        }

        /// <summary>POSE (0x01), owner authoritative: the slot's body, which is its weapon or, when free, its soul.</summary>
        public void SetPlayerPose(byte slot, PoseFlags flags, Vector3 pos, Quaternion rot, Vector3 vel, ushort seq)
        {
            Players.SetPose(slot, flags, pos, rot, vel, seq);
        }

        // ---- weapons ----

        public void Apply(in WorldResetMsg msg)
        {
            Weapons.Clear();
            Enemies.Clear();
            Kit.Clear();
            for (var i = 0; i < Players.Count; i++) Players[i].weaponId = Wire.NoId;
            SceneHash = msg.sceneHash;
            Rev++;
        }

        public void Apply(in WeaponStateMsg msg)
        {
            if (msg.rows == null) return;
            for (var i = 0; i < msg.rows.Count; i++)
            {
                var row = msg.rows[i];
                var w = Weapons.GetOrAdd(row.weaponId, out var added);
                w.hp = row.hp;
                w.maxHp = row.maxHp;
                w.mods.Clear();
                if (row.mods != null) for (var m = 0; m < row.mods.Length; m++) w.mods.Add(row.mods[m]);
                if (!added) continue;
                // Ownership and breaking belong to their events; a row only carries them when it is new.
                w.defId = row.defId;
                w.broken = (row.flags & WeaponFlags.Broken) != 0;
                w.ownerSlot = row.ownerSlot;
                w.respawnTick = row.respawnTick;
                w.homeSlot = row.homeSlot;
                var owner = Players[w.ownerSlot];
                if (owner != null) owner.weaponId = w.id;
            }
            Rev++;
        }

        public void Apply(in WeaponOwnerMsg msg)
        {
            var w = Weapons.Get(msg.weaponId);
            if (w == null) return;
            var before = Players[w.ownerSlot];
            if (before != null && before.weaponId == w.id) before.weaponId = Wire.NoId;

            if (msg.ownerSlot == Wire.NoSlot)
            {
                w.ownerSlot = Wire.NoSlot;
                w.hasRest = msg.hasPose;
                if (msg.hasPose)
                {
                    w.restPos = msg.pos;
                    w.restRot = msg.rot;
                    w.restVel = msg.vel;
                }
                return;
            }

            // One weapon per player: whatever the new owner held before is let go.
            var held = Weapons.OwnedBy(msg.ownerSlot);
            if (held != null && held != w) held.ownerSlot = Wire.NoSlot;
            w.ownerSlot = msg.ownerSlot;
            w.hasRest = false;
            var after = Players[msg.ownerSlot];
            if (after != null) after.weaponId = w.id;
        }

        public void Apply(in WeaponBrokenMsg msg)
        {
            var w = Weapons.Get(msg.weaponId);
            if (w == null) return;
            var owner = Players[w.ownerSlot];
            if (owner != null && owner.weaponId == w.id) owner.weaponId = Wire.NoId;
            w.ownerSlot = Wire.NoSlot;
            w.broken = true;
            w.hp = 0f;
            w.respawnTick = msg.respawnTick;
            w.mods.Clear();
            w.hasRest = false;
            w.combatUntilTick = 0;
        }

        public void Apply(in WeaponRespawnedMsg msg)
        {
            var w = Weapons.Get(msg.weaponId);
            if (w == null) return;
            w.broken = false;
            w.hp = w.maxHp;
            w.hasRest = false;
            w.combatUntilTick = 0;
        }

        public void Apply(in WeaponDamagedMsg msg)
        {
            var w = Weapons.Get(msg.weaponId);
            if (w == null || w.broken) return;
            w.hp = Mathf.Max(0f, msg.newHp);
            w.combatUntilTick = msg.header.tick + CombatTicks;
        }

        /// <summary>The sim keeps nothing from a bat; it only has to accept it so the event reaches Game on every peer.</summary>
        public void Apply(in BatEventMsg msg) { }

        // ---- enemies ----

        public void Apply(in EnemyStateMsg msg)
        {
            if (msg.rows == null) return;
            for (var i = 0; i < msg.rows.Count; i++)
            {
                var row = msg.rows[i];
                var e = Enemies.GetOrAdd(row.enemyId);
                e.kind = row.kind;
                e.pos = row.pos;
                e.yaw = row.yaw;
                e.hp = row.hp;
                e.shieldHp = row.shieldHp;
                e.state = row.state;
                e.flags = row.flags;
                e.targetWeaponId = row.targetWeaponId;
                e.samples++;
            }
        }

        public void Apply(in EnemyHealthMsg msg)
        {
            var e = Enemies.Get(msg.enemyId);
            if (e != null)
            {
                e.hp = msg.newHp;
                e.shieldHp = msg.newShieldHp;
                if ((msg.flags & HitFlags.Died) != 0)
                {
                    e.flags &= ~(EnemyFlags.Alive | EnemyFlags.Hostile);
                    e.targetWeaponId = Wire.NoId;
                }
            }
            var w = Weapons.Get(msg.weaponId);
            if (w != null && !w.broken) w.combatUntilTick = msg.header.tick + CombatTicks;
        }

        // ---- kit ----

        public void Apply(in KitStateMsg msg)
        {
            Kit.Set(msg.kind, msg.pieceId, msg.state, msg.actor, msg.value, msg.clockMs);
            var w = Weapons.Get(msg.actor);
            if (w == null || w.broken) return;
            // Two kit pieces change the weapon that used them, so the change rides the same ordered event.
            if (msg.kind == KitKind.Anvil) w.hp = w.maxHp;
            else if (msg.kind == KitKind.Rune && msg.state != 0) w.mods.Add((byte)msg.value);
        }

        // ---- the labyrinth ----
        // The labyrinth deliberately does NOT bump WorldSim.Rev: Rev means "the weapon and kit tables
        // were replaced, rebuild the scene from them", and a room swap every twenty seconds must not put
        // every loose weapon back on its rest pose. Views watch LabyrinthState.Rev instead.
        public void Apply(in LabLayoutMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        public void Apply(in LabSwappedMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        public void Apply(in RoundResultMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        public void Apply(in PlayerDownMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        public void Apply(in PlayerRespawnMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        public void Apply(in LegendMsg msg)
        {
            Labyrinth.Apply(msg);
        }
        // ---- the scratch pad ----
        // Like the labyrinth, the pad does NOT bump WorldSim.Rev: a stroke every few seconds must not make
        // every client rebuild its weapons from the rows. Views watch ScratchPadState.Rev instead.
        public void Apply(in PadStrokeMsg msg)
        {
            Pad.Apply(msg);
        }

        public void Apply(in PadClearMsg msg)
        {
            Pad.Apply(msg);
        }


        /// <summary>A Reply meant for this peer alone: its own secret role. It is never sent on again.</summary>
        public void Apply(in RoleAssignMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        /// <summary>A Reply meant for this peer alone: what its own compass points at, never who bent it.</summary>
        public void Apply(in CompassTargetsMsg msg)
        {
            Labyrinth.Apply(msg);
        }

        // ---- snapshot ----

        /// <summary>Writes one table; false for a kind this sim does not carry.</summary>
        public bool WritePart(SnapshotPartKind kind, NetWriter w)
        {
            switch (kind)
            {
                case SnapshotPartKind.World:
                    w.U32(Tick).U32(WorldSeed).U32(Rng.State);
                    w.U8((byte)Phase).U32(PhaseStartTick).U8(FloorId);
                    w.U8((byte)EndReason).I32(FinalScore).U32(SceneHash);
                    return true;
                case SnapshotPartKind.Players:
                    Players.Write(w);
                    return true;
                case SnapshotPartKind.Weapons:
                    Weapons.Write(w);
                    return true;
                case SnapshotPartKind.Enemies:
                    Enemies.Write(w);
                    return true;
                case SnapshotPartKind.Kit:
                    Kit.Write(w);
                    return true;
                case SnapshotPartKind.Labyrinth:
                    Labyrinth.Write(w);
                    return true;
                case SnapshotPartKind.Pad:
                    Pad.Write(w);
                    return true;

            }
            return false;
        }

        /// <summary>Reads one table back; false for a kind this sim does not carry or a reader that ran off the end.</summary>
        public bool ReadPart(SnapshotPartKind kind, NetReader r)
        {
            Rev++;
            switch (kind)
            {
                case SnapshotPartKind.World:
                    Tick = r.U32();
                    WorldSeed = r.U32();
                    Rng.State = r.U32();
                    Phase = (SessionPhase)r.U8();
                    PhaseStartTick = r.U32();
                    FloorId = r.U8();
                    EndReason = (SessionEndReason)r.U8();
                    FinalScore = r.I32();
                    SceneHash = r.U32();
                    return !r.Failed;
                case SnapshotPartKind.Players:
                    Players.Read(r);
                    return !r.Failed;
                case SnapshotPartKind.Weapons:
                    Weapons.Read(r);
                    return !r.Failed;
                case SnapshotPartKind.Enemies:
                    Enemies.Read(r);
                    return !r.Failed;
                case SnapshotPartKind.Kit:
                    Kit.Read(r);
                    return !r.Failed;
                case SnapshotPartKind.Labyrinth:
                    Labyrinth.Read(r);
                    return !r.Failed;
                case SnapshotPartKind.Pad:
                    Pad.Read(r);
                    return !r.Failed;

            }
            return false;
        }

        /// <summary>FNV-1a over every snapshot table: the desync check the EditMode tests compare across peers.</summary>
        public ulong Hash()
        {
            var w = new NetWriter(MsgId.Snapshot, 2048);
            WritePart(SnapshotPartKind.World, w);
            WritePart(SnapshotPartKind.Players, w);
            WritePart(SnapshotPartKind.Weapons, w);
            WritePart(SnapshotPartKind.Enemies, w);
            WritePart(SnapshotPartKind.Kit, w);
            WritePart(SnapshotPartKind.Labyrinth, w);
            WritePart(SnapshotPartKind.Pad, w);

            var bytes = w.ToArray();
            return SimHash.Fnv1a64(bytes, 1, bytes.Length - 1);
        }

        void SetPhase(SessionPhase phase, uint tick)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseStartTick = tick;
        }
    }
}
