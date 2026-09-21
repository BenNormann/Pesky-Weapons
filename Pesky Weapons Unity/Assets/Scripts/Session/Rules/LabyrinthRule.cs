using System;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// The host's labyrinth brain: role assignment at round start, the Mage's two powers, respawns and
    /// the two ways a round ends. It is both a rule (once per sim tick) and the validator for SWAP_REQ and
    /// COMPASS_BEND_REQ, because both powers need the same secret table and a validator that could be
    /// registered without the rule would be a way to leak it.
    ///
    /// THE SECRETS LIVE HERE AND NOWHERE ELSE. Who is a Mage and whose compass is bent are fields of this
    /// object, on the host, in the host's process. They are never written into the WorldSim, never put in a
    /// snapshot, never broadcast and never logged. A Mage learns its own role from a ROLE_ASSIGN addressed
    /// to it; a bent player learns only its own new compass target, with no author on it. The single
    /// message that names the Mages is ROUND_RESULT, and by then the round cannot be played any more.
    ///
    /// Every refusal is silent. A weapon that sends a SWAP_REQ to see what happens gets exactly what a
    /// Mage on cooldown gets: nothing.
    /// </summary>
    public sealed class LabyrinthRule : IHostRule, IIntentValidator
    {
        /// <summary>The round rules look at where everybody is five times a second; they are not frame-critical.</summary>
        const int ScanEveryTicks = 4;

        readonly int[] _cell = new int[Wire.MaxPlayers];
        readonly bool[] _atExit = new bool[Wire.MaxPlayers];
        readonly bool[] _mage = new bool[Wire.MaxPlayers];
        readonly bool[] _bent = new bool[Wire.MaxPlayers];
        readonly CompassTargetKind[] _bendKind = new CompassTargetKind[Wire.MaxPlayers];
        readonly int[] _bendCell = new int[Wire.MaxPlayers];
        readonly uint[] _lastSwapTick = new uint[Wire.MaxPlayers];
        readonly bool[] _swapUsed = new bool[Wire.MaxPlayers];
        readonly uint[] _lastBendTick = new uint[Wire.MaxPlayers];
        readonly bool[] _bendUsed = new bool[Wire.MaxPlayers];
        readonly int[] _shuffle = new int[Wire.MaxPlayers];

        readonly Rng _secret;
        bool _roundOpen;
        uint _nextScanTick;

        public LabyrinthRule()
        {
            // A HOST-PRIVATE generator. WorldSim.Rng is reseeded from SESSION_INFO on every peer, so a
            // client could replay any draw made from it and name the Mage. This one exists only here.
            _secret = new Rng(unchecked((uint)Guid.NewGuid().GetHashCode()));
        }

        // ---------------------------------------------------------------- the rule

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null) return;
            if (sim.Phase != SessionPhase.Playing)
            {
                if (_roundOpen) CloseRound();
                return;
            }
            LabyrinthDef def = sim.Data != null ? sim.Data.labyrinth : null;
            if (!_roundOpen)
            {
                OpenRound(sim, def, events);
                return;
            }
            if (tick < _nextScanTick) return;
            _nextScanTick = tick + ScanEveryTicks;
            Scan(sim, events);
            Respawns(sim, tick, events);
            CheckEndings(sim, def, events);
        }

        void OpenRound(WorldSim sim, LabyrinthDef def, EventSink events)
        {
            // A fresh labyrinth for every round. Clients do not derive it: LAB_LAYOUT carries the table,
            // so the host may reseed freely and the broadcast is what every peer, this one included, keeps.
            sim.Labyrinth.Rebuild(def, unchecked(sim.WorldSeed + sim.PhaseStartTick * 2654435761u + 1u));
            LabyrinthGrid grid = sim.Labyrinth.Grid;
            if (grid == null) return;

            _roundOpen = true;
            ClearTables();
            events.Emit(grid.ToLayout().Encode());
            AssignRoles(sim, def, events);
            _nextScanTick = sim.Tick + ScanEveryTicks;
        }

        void CloseRound()
        {
            _roundOpen = false;
            ClearTables();
        }

        void ClearTables()
        {
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _cell[i] = LabyrinthGrid.NoCell;
                _atExit[i] = false;
                _mage[i] = false;
                _bent[i] = false;
                _bendKind[i] = CompassTargetKind.GoodEnd;
                _bendCell[i] = LabyrinthGrid.NoCell;
                _lastSwapTick[i] = 0;
                _swapUsed[i] = false;
                _lastBendTick[i] = 0;
                _bendUsed[i] = false;
            }
        }

        /// <summary>
        /// One or two Mage fragments among the present players, drawn from the host-private generator and
        /// told to nobody else. A player who is told nothing is a weapon, which is why no ROLE_ASSIGN goes
        /// to the rest: an empty message is still a message.
        /// </summary>
        void AssignRoles(WorldSim sim, LabyrinthDef def, EventSink events)
        {
            int count = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim.Players[i];
                if (p != null && p.present) _shuffle[count++] = i;
            }
            if (count == 0) return;
            int mages = def != null ? def.MageCountFor(count) : 1;
            if (mages > count) mages = count;
            if (mages < 1) mages = 1;

            for (int i = count - 1; i > 0; i--)
            {
                int j = _secret.Range(0, i + 1);
                int t = _shuffle[i];
                _shuffle[i] = _shuffle[j];
                _shuffle[j] = t;
            }

            RoleAssignMsg msg = new RoleAssignMsg();
            msg.role = LabyrinthRole.Mage;
            byte[] payload = msg.Encode();
            for (int i = 0; i < mages; i++)
            {
                byte slot = (byte)_shuffle[i];
                _mage[slot] = true;
                events.Reply(slot, payload);
            }
        }

        /// <summary>Where everybody is, straight from the Game-side half of the host. A slot that emptied loses every secret attached to it.</summary>
        void Scan(WorldSim sim, EventSink events)
        {
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _cell[i] = LabyrinthGrid.NoCell;
                _atExit[i] = false;
                PlayerState p = sim.Players[i];
                if (p != null && p.present) continue;
                _mage[i] = false;
                _bent[i] = false;
                _swapUsed[i] = false;
                _bendUsed[i] = false;
            }
            IHostWorld world = events.World;
            if (world != null) world.CollectPlayerCells(_cell, _atExit);
        }

        void Respawns(WorldSim sim, uint tick, EventSink events)
        {
            LabyrinthState lab = sim.Labyrinth;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if (!lab.IsDown(i)) continue;
                PlayerState p = sim.Players[i];
                if (p == null || !p.present) continue;
                if (tick < lab.RespawnTick(i)) continue;
                byte anchor = Anchor(sim, i);
                PlayerRespawnMsg msg = new PlayerRespawnMsg();
                msg.header = events.Header();
                msg.slot = (byte)i;
                msg.anchorSlot = anchor;
                msg.cell = anchor != Wire.NoSlot && _cell[anchor] >= 0 ? (ushort)_cell[anchor] : (ushort)Wire.NoId;
                events.Emit(msg.Encode());
            }
        }

        /// <summary>
        /// The player to come back beside: the one standing nearest the middle of the largest group. That
        /// is "respawn with the group" without needing a party leader. NoSlot when nobody is placed yet.
        /// </summary>
        byte Anchor(WorldSim sim, int forSlot)
        {
            int bestCell = LabyrinthGrid.NoCell;
            int bestCount = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if (!Standing(sim, i, forSlot)) continue;
                int count = 0;
                for (int j = 0; j < Wire.MaxPlayers; j++)
                    if (Standing(sim, j, forSlot) && _cell[j] == _cell[i]) count++;
                if (count <= bestCount) continue;
                bestCount = count;
                bestCell = _cell[i];
            }
            if (bestCell == LabyrinthGrid.NoCell) return Wire.NoSlot;

            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if (!Standing(sim, i, forSlot) || _cell[i] != bestCell) continue;
                sum += sim.Players[i].pos;
                n++;
            }
            if (n == 0) return Wire.NoSlot;
            Vector3 middle = sum / n;

            byte best = Wire.NoSlot;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if (!Standing(sim, i, forSlot) || _cell[i] != bestCell) continue;
                float sq = (sim.Players[i].pos - middle).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = (byte)i;
            }
            return best;
        }

        bool Standing(WorldSim sim, int slot, int exceptSlot)
        {
            if (slot == exceptSlot || _cell[slot] < 0) return false;
            PlayerState p = sim.Players[slot];
            return p != null && p.present && !sim.Labyrinth.IsDown(slot);
        }

        /// <summary>The two ways a round stops: the crew at the exit doorway, or a majority in the Resurrection Room.</summary>
        void CheckEndings(WorldSim sim, LabyrinthDef def, EventSink events)
        {
            LabyrinthGrid grid = sim.Labyrinth.Grid;
            if (grid == null) return;

            int present = 0;
            int nonMage = 0;
            int atExit = 0;
            int inBad = 0;
            byte escapedMask = 0;
            byte mageMask = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim.Players[i];
                if (p == null || !p.present) continue;
                present++;
                if (_mage[i]) mageMask |= (byte)(1 << i);
                else
                {
                    nonMage++;
                    if (_atExit[i] && _cell[i] == grid.GoodEndCell)
                    {
                        atExit++;
                        escapedMask |= (byte)(1 << i);
                    }
                }
                if (_cell[i] == grid.BadEndCell) inBad++;
            }
            if (present == 0) return;

            // The Mage's instant win, checked first: it beats a simultaneous escape.
            float resurrection = def != null ? def.resurrectionFraction : 0.5f;
            if (inBad > 0 && inBad > resurrection * present)
            {
                Finish(sim, events, RoundOutcome.Resurrected, 0, mageMask, SessionEndReason.CrewLost);
                return;
            }

            if (nonMage <= 0 || atExit <= 0) return;
            float escape = def != null ? def.escapeFraction : 1f;
            int needed = (int)Math.Ceiling(escape * nonMage - 0.0001f);
            if (needed < 1) needed = 1;
            if (atExit >= needed)
                Finish(sim, events, RoundOutcome.Escaped, escapedMask, mageMask, SessionEndReason.Escaped);
        }

        void Finish(WorldSim sim, EventSink events, RoundOutcome outcome, byte escapedMask, byte mageMask,
            SessionEndReason reason)
        {
            RoundResultMsg result = new RoundResultMsg();
            result.header = events.Header();
            result.outcome = outcome;
            result.escapedMask = escapedMask;
            // The one message that ever names the Mages, and the round is already over when it goes out.
            result.mageMask = mageMask;
            events.Emit(result.Encode());

            SessionEndMsg end = new SessionEndMsg();
            end.header = events.Header();
            end.reason = reason;
            end.finalScore = sim.Labyrinth.TotalLegend();
            events.Emit(end.Encode());
            CloseRound();
        }

        // ---------------------------------------------------------------- the Mage's two powers

        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            // Silence is the answer to everything. A weapon that sends one of these to see what happens
            // gets precisely what a Mage on cooldown gets: no reply, no event, no console line.
            if (sim == null || events == null || !_roundOpen) return;
            if (fromSlot >= Wire.MaxPlayers || !_mage[fromSlot]) return;
            LabyrinthDef def = sim.Data != null ? sim.Data.labyrinth : null;
            if (MessageInfo.IdOf(payload) == MsgId.SwapReq) OnSwap(fromSlot, payload, sim, def, tick, events);
            else OnBend(fromSlot, payload, sim, def, tick, events);
        }

        void OnSwap(byte fromSlot, byte[] payload, WorldSim sim, LabyrinthDef def, uint tick, EventSink events)
        {
            SwapReqMsg req;
            if (!SwapReqMsg.TryDecode(payload, out req)) return;
            LabyrinthGrid grid = sim.Labyrinth.Grid;
            if (grid == null) return;
            // The asset owns this number and nothing else does. The 60 here is only what a session with
            // no LabyrinthDef at all would use; it matches the asset so the two can never disagree.
            uint cooldown = Ticks(def != null ? def.swapCooldownSeconds : 60f);
            if (_swapUsed[fromSlot] && tick < _lastSwapTick[fromSlot] + cooldown) return;
            bool wrap = def != null && def.swapAcrossWrap;
            // Adjacency, the three fixed rooms and "every room can still reach the Exit" all live in here.
            if (!grid.CanSwap(req.cellA, req.cellB, wrap)) return;

            _swapUsed[fromSlot] = true;
            _lastSwapTick[fromSlot] = tick;
            LabSwappedMsg msg = new LabSwappedMsg();
            msg.header = events.Header();
            msg.cellA = req.cellA;
            msg.cellB = req.cellB;
            events.Emit(msg.Encode());
        }

        void OnBend(byte fromSlot, byte[] payload, WorldSim sim, LabyrinthDef def, uint tick, EventSink events)
        {
            CompassBendReqMsg req;
            if (!CompassBendReqMsg.TryDecode(payload, out req)) return;
            LabyrinthGrid grid = sim.Labyrinth.Grid;
            if (grid == null) return;
            uint cooldown = Ticks(def != null ? def.bendCooldownSeconds : 15f);
            if (_bendUsed[fromSlot] && tick < _lastBendTick[fromSlot] + cooldown) return;
            if (req.target == CompassTargetKind.Cell && !grid.InRange(req.cell)) return;

            bool bend = req.target != CompassTargetKind.GoodEnd;
            int nonMage = 0;
            int bentAfter = 0;
            bool changes = false;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim.Players[i];
                if (p == null || !p.present || _mage[i]) continue;
                nonMage++;
                bool named = (req.slots & (1 << i)) != 0;
                bool after = named ? bend : _bent[i];
                if (after) bentAfter++;
                if (!named) continue;
                if (after != _bent[i]) changes = true;
                else if (after && (_bendKind[i] != req.target || _bendCell[i] != req.cell)) changes = true;
            }
            if (nonMage == 0 || !changes) return;

            // The minority rule: strictly fewer than half of the non-Mage players may be lied to at once,
            // counting both Mages' work together, so two of them cannot bend the whole room between them.
            float limit = def != null ? def.bendFractionLimit : 0.5f;
            if (bentAfter >= limit * nonMage) return;

            _bendUsed[fromSlot] = true;
            _lastBendTick[fromSlot] = tick;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if ((req.slots & (1 << i)) == 0) continue;
                PlayerState p = sim.Players[i];
                if (p == null || !p.present || _mage[i]) continue;
                _bent[i] = bend;
                _bendKind[i] = req.target;
                _bendCell[i] = bend ? req.cell : LabyrinthGrid.NoCell;

                CompassTargetsMsg msg = new CompassTargetsMsg();
                msg.target = req.target;
                msg.cell = bend ? req.cell : (ushort)0;
                // To that player alone, and it does not say who did it or that anybody did.
                events.Reply((byte)i, msg.Encode());
            }
        }

        // ---------------------------------------------------------------- going down

        /// <summary>Seconds to ticks, never zero.</summary>
        public static uint Ticks(float seconds)
        {
            int t = (int)(seconds * Protocol.Tick.PerSecond + 0.5f);
            return (uint)(t < 1 ? 1 : t);
        }

        /// <summary>
        /// The one place a PLAYER_DOWN is built, so the wait is the same wherever a player goes down. The
        /// respawn itself is this rule's business: it watches WorldSim for the respawn tick.
        /// </summary>
        public static byte[] DownPayload(EventHeader header, LabyrinthDef def, byte slot)
        {
            PlayerDownMsg msg = new PlayerDownMsg();
            msg.header = header;
            msg.slot = slot;
            msg.respawnTick = header.tick + Ticks(def != null ? def.respawnDelaySeconds : 10f);
            return msg.Encode();
        }

        /// <summary>Death costs all of it: the companion message to DownPayload.</summary>
        public static byte[] LegendPayload(EventHeader header, byte slot, int legend)
        {
            LegendMsg msg = new LegendMsg();
            msg.header = header;
            msg.slot = slot;
            msg.legend = legend;
            return msg.Encode();
        }
    }
}
