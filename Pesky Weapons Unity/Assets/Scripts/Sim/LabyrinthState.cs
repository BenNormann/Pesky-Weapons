using Pesky.Data;
using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>
    /// The labyrinth half of the shared world: the grid table, every player's legend, who is down and when
    /// they come back, and how the round ended.
    ///
    /// WHAT IS SHARED AND WHAT IS NOT. Everything above rides the snapshot and is public knowledge. Three
    /// things do NOT and never leave the peer they belong to: this player's own role, this player's own
    /// compass target, and the fact that a compass was bent at all. They are set from Replies addressed to
    /// one peer, they are never written to a snapshot, and no other peer's copy of them exists here. The
    /// host's table of who is a Mage lives in the host's LabyrinthRule and is not in the sim at all.
    /// </summary>
    public sealed class LabyrinthState
    {
        readonly int[] _legend = new int[Wire.MaxPlayers];
        readonly uint[] _respawnTick = new uint[Wire.MaxPlayers];
        byte _downMask;

        /// <summary>The table. Null only before the first layout, which never happens in a started session.</summary>
        public LabyrinthGrid Grid { get; private set; }

        public RoundOutcome Outcome { get; private set; }
        public uint OutcomeTick { get; private set; }

        /// <summary>One bit per slot: who reached the exit. Filled by ROUND_RESULT.</summary>
        public byte EscapedMask { get; private set; }

        /// <summary>One bit per slot: who the Mages were. Zero until ROUND_RESULT reveals them.</summary>
        public byte MageMask { get; private set; }

        /// <summary>LOCAL ONLY. This peer's own role. Never snapshotted, never broadcast, never logged.</summary>
        public LabyrinthRole LocalRole { get; private set; }

        /// <summary>LOCAL ONLY. What this peer's compass points at. GoodEnd is the truth and the default.</summary>
        public CompassTargetKind LocalCompassKind { get; private set; }

        /// <summary>LOCAL ONLY. The cell a Cell-kind compass target names.</summary>
        public int LocalCompassCell { get; private set; }

        /// <summary>Bumped whenever the grid table or the round changed, so a view can notice cheaply.</summary>
        public uint Rev { get; private set; }

        public LabyrinthState()
        {
            LocalCompassKind = CompassTargetKind.GoodEnd;
            LocalCompassCell = LabyrinthGrid.NoCell;
        }

        // ---------------------------------------------------------------- reads

        public int Legend(int slot) { return slot >= 0 && slot < _legend.Length ? _legend[slot] : 0; }

        public bool IsDown(int slot) { return slot >= 0 && slot < Wire.MaxPlayers && (_downMask & (1 << slot)) != 0; }

        public uint RespawnTick(int slot) { return slot >= 0 && slot < _respawnTick.Length ? _respawnTick[slot] : 0u; }

        public int TotalLegend()
        {
            int sum = 0;
            for (int i = 0; i < _legend.Length; i++) sum += _legend[i];
            return sum;
        }

        /// <summary>What this peer's compass should show from a cell, target and bending included.</summary>
        public CompassHint Hint(int fromCell)
        {
            if (Grid == null) return CompassHint.None;
            switch (LocalCompassKind)
            {
                case CompassTargetKind.BadEnd: return Grid.ToCell(fromCell, Grid.BadEndCell);
                case CompassTargetKind.Cell: return Grid.ToCell(fromCell, LocalCompassCell);
                default: return Grid.ToExit(fromCell);
            }
        }

        // ---------------------------------------------------------------- the round

        /// <summary>Builds the deterministic layout for a seed. Called when the sim is made and whenever SESSION_INFO reseeds it.</summary>
        public void Rebuild(LabyrinthDef def, uint seed)
        {
            Grid = LabyrinthGrid.Generate(def, seed);
            ResetRound();
        }

        public void ResetRound()
        {
            for (int i = 0; i < _legend.Length; i++) _legend[i] = 0;
            for (int i = 0; i < _respawnTick.Length; i++) _respawnTick[i] = 0;
            _downMask = 0;
            Outcome = RoundOutcome.None;
            OutcomeTick = 0;
            EscapedMask = 0;
            MageMask = 0;
            LocalRole = LabyrinthRole.Weapon;
            LocalCompassKind = CompassTargetKind.GoodEnd;
            LocalCompassCell = LabyrinthGrid.NoCell;
            Rev++;
        }

        public void Apply(in LabLayoutMsg msg)
        {
            LabyrinthGrid g = LabyrinthGrid.FromLayout(msg);
            if (g == null) return;
            DoorwayFilter filter = Grid != null ? Grid.DoorOpen : null;
            Grid = g;
            Grid.DoorOpen = filter;
            ResetRound();
        }

        public void Apply(in LabSwappedMsg msg)
        {
            if (Grid == null) return;
            if (Grid.ForceSwap(msg.cellA, msg.cellB)) Rev++;
        }

        public void Apply(in RoundResultMsg msg)
        {
            Outcome = msg.outcome;
            OutcomeTick = msg.header.tick;
            EscapedMask = msg.escapedMask;
            MageMask = msg.mageMask;
            Rev++;
        }

        public void Apply(in PlayerDownMsg msg)
        {
            if (msg.slot >= Wire.MaxPlayers) return;
            _downMask |= (byte)(1 << msg.slot);
            _respawnTick[msg.slot] = msg.respawnTick;
            Rev++;
        }

        public void Apply(in PlayerRespawnMsg msg)
        {
            if (msg.slot >= Wire.MaxPlayers) return;
            _downMask &= (byte)~(1 << msg.slot);
            _respawnTick[msg.slot] = 0;
            Rev++;
        }

        public void Apply(in LegendMsg msg)
        {
            if (msg.slot >= Wire.MaxPlayers) return;
            _legend[msg.slot] = msg.legend;
            Rev++;
        }

        /// <summary>A Reply addressed to this peer alone. Nothing here is ever forwarded to another peer.</summary>
        public void Apply(in RoleAssignMsg msg)
        {
            LocalRole = msg.role;
        }

        /// <summary>A Reply addressed to this peer alone. It does not say who set it, and neither does this.</summary>
        public void Apply(in CompassTargetsMsg msg)
        {
            LocalCompassKind = msg.target;
            LocalCompassCell = msg.target == CompassTargetKind.Cell ? msg.cell : LabyrinthGrid.NoCell;
        }

        // ---------------------------------------------------------------- snapshot

        public void Write(NetWriter w)
        {
            w.Bool(Grid != null);
            if (Grid != null) Grid.Write(w);
            for (int i = 0; i < _legend.Length; i++) w.I32(_legend[i]);
            w.U8(_downMask);
            for (int i = 0; i < _respawnTick.Length; i++) w.U32(_respawnTick[i]);
            w.U8((byte)Outcome).U32(OutcomeTick).U8(EscapedMask).U8(MageMask);
        }

        public void Read(NetReader r)
        {
            bool hasGrid = r.Bool();
            if (hasGrid)
            {
                LabyrinthGrid g = LabyrinthGrid.Read(r);
                if (g != null)
                {
                    DoorwayFilter filter = Grid != null ? Grid.DoorOpen : null;
                    Grid = g;
                    Grid.DoorOpen = filter;
                }
            }
            for (int i = 0; i < _legend.Length; i++) _legend[i] = r.I32();
            _downMask = r.U8();
            for (int i = 0; i < _respawnTick.Length; i++) _respawnTick[i] = r.U32();
            Outcome = (RoundOutcome)r.U8();
            OutcomeTick = r.U32();
            EscapedMask = r.U8();
            MageMask = r.U8();
            Rev++;
        }
    }
}
