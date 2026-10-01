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
        // This round's ROLE_ASSIGN went to this slot (Mage or Weapon). Cleared with the round and when the slot empties.
        readonly bool[] _told = new bool[Wire.MaxPlayers];
        readonly uint[] _lastNudgeTick = new uint[Wire.MaxPlayers];
        readonly bool[] _nudgeUsed = new bool[Wire.MaxPlayers];
        // Per TARGET (the eight slots, then the tutorial dummy at index MaxPlayers): the last accepted nudge
        // on it and which fragment made it, for the two-fragment co-sign.
        readonly uint[] _nudgedAtTick = new uint[Wire.MaxPlayers + 1];
        readonly byte[] _nudgedBy = new byte[Wire.MaxPlayers + 1];

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
            // RUN MODE (docs/RUN.md): this rule only keeps the secret Mage table and answers the nudge. The
            // grid, the respawns and the two labyrinth endings are set aside; RunRule ends the round.
            if (IsRunMode(sim)) return;
            Respawns(sim, tick, events);
            CheckEndings(sim, def, events);
        }

        void OpenRound(WorldSim sim, LabyrinthDef def, EventSink events)
        {
            if (!IsRunMode(sim))
            {
                // A fresh labyrinth for every round. Clients do not derive it: LAB_LAYOUT carries the table,
                // so the host may reseed freely and the broadcast is what every peer, this one included, keeps.
                sim.Labyrinth.Rebuild(def, unchecked(sim.WorldSeed + sim.PhaseStartTick * 2654435761u + 1u));
                LabyrinthGrid grid = sim.Labyrinth.Grid;
                if (grid == null) return;
                _roundOpen = true;
                ClearTables();
                events.Emit(grid.ToLayout().Encode());
            }
            else
            {
                // The run has no grid to send; the room sequence is RunRule's (it waits for the host's scene).
                _roundOpen = true;
                ClearTables();
            }
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
                _told[i] = false;
                _bent[i] = false;
                _bendKind[i] = CompassTargetKind.GoodEnd;
                _bendCell[i] = LabyrinthGrid.NoCell;
                _lastSwapTick[i] = 0;
                _swapUsed[i] = false;
                _lastBendTick[i] = 0;
                _bendUsed[i] = false;
                _lastNudgeTick[i] = 0;
                _nudgeUsed[i] = false;
            }
            for (int i = 0; i <= Wire.MaxPlayers; i++)
            {
                _nudgedAtTick[i] = 0;
                _nudgedBy[i] = Wire.NoSlot;
            }
        }

        /// <summary>
        /// One or two Mage fragments among the present players, drawn from the host-private generator and
        /// told to nobody else. A player who is told nothing is a weapon, which is why no ROLE_ASSIGN goes
        /// to the rest: an empty message is still a message.
        /// </summary>
        /// <summary>
        /// One or two Mage fragments among the present players, drawn from the host-private generator. EVERY
        /// present player is told its role for THIS round: ROLE_ASSIGN(Mage) to the fragments and
        /// ROLE_ASSIGN(Weapon) to everybody else, the same two bytes each, so nobody can tell the roles apart
        /// by who got a packet. Each peer resets its own role when the round starts (WorldSim.SetPhase), and
        /// before this every weapon was told nothing - so a Mage of the last round stayed one (round 10).
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

            for (int i = 0; i < count; i++)
            {
                byte slot = (byte)_shuffle[i];
                bool mage = i < mages;
                _mage[slot] = mage;
                TellRole(events, slot, mage);
            }
        }

        /// <summary>ROLE_ASSIGN to one slot alone (EventSink.Reply; the host's own slot gets it through LocalReply).</summary>
        void TellRole(EventSink events, byte slot, bool mage)
        {
            RoleAssignMsg msg = new RoleAssignMsg();
            msg.role = mage ? LabyrinthRole.Mage : LabyrinthRole.Weapon;
            events.Reply(slot, msg.Encode());
            _told[slot] = true;
        }

        /// <summary>Where everybody is, straight from the Game-side half of the host. A slot that emptied loses every secret attached to it.</summary>
        void Scan(WorldSim sim, EventSink events)
        {
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _cell[i] = LabyrinthGrid.NoCell;
                _atExit[i] = false;
                PlayerState p = sim.Players[i];
                if (p != null && p.present)
                {
                    // A late joiner (or anybody the draw at round open missed) is a weapon, and is told so.
                    if (!_told[i]) TellRole(events, (byte)i, false);
                    continue;
                }
                _told[i] = false;
                _mage[i] = false;
                _bent[i] = false;
                _swapUsed[i] = false;
                _bendUsed[i] = false;
                _nudgeUsed[i] = false;
            }
            if (IsRunMode(sim)) return;
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
            // ON THE WIRE. The host's own debug console (NetDebug: the Editor, development builds, ?debug=1 pages) does say why.
            if (sim == null || events == null || !_roundOpen)
            {
                NetDebug.Log("host: intent 0x" + MessageInfo.IdOf(payload).ToString("X2") + " from slot " + fromSlot + " refused: no round open");
                return;
            }
            if (fromSlot >= Wire.MaxPlayers || !_mage[fromSlot])
            {
                NetDebug.Log("host: intent 0x" + MessageInfo.IdOf(payload).ToString("X2") + " from slot " + fromSlot + " refused: not a Mage");
                return;
            }
            LabyrinthDef def = sim.Data != null ? sim.Data.labyrinth : null;
            byte id = MessageInfo.IdOf(payload);
            if (id == MsgId.NudgeReq) { OnNudge(fromSlot, payload, sim, def, tick, events); return; }
            if (IsRunMode(sim))
            {
                // The map powers are set aside with the labyrinth: there is no grid to swap and no compass to bend.
                NetDebug.Log("host: intent 0x" + id.ToString("X2") + " from slot " + fromSlot + " refused: not a run-mode power");
                return;
            }
            if (id == MsgId.SwapReq) OnSwap(fromSlot, payload, sim, def, tick, events);
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
            if (_swapUsed[fromSlot] && tick < _lastSwapTick[fromSlot] + cooldown)
            {
                NetDebug.Log("host: swap " + req.cellA + "<->" + req.cellB + " from slot " + fromSlot + " refused: cooldown, " + (_lastSwapTick[fromSlot] + cooldown - tick) + " ticks left");
                return;
            }
            bool wrap = def != null && def.swapAcrossWrap;
            // Adjacency, the three fixed rooms and "every room can still reach the Exit" all live in here.
            if (!grid.CanSwap(req.cellA, req.cellB, wrap))
            {
                NetDebug.Log("host: swap " + req.cellA + "<->" + req.cellB + " from slot " + fromSlot + " refused: not swappable (adjacency, a fixed room, or the Exit would become unreachable)");
                return;
            }

            _swapUsed[fromSlot] = true;
            _lastSwapTick[fromSlot] = tick;
            LabSwappedMsg msg = new LabSwappedMsg();
            msg.header = events.Header();
            msg.cellA = req.cellA;
            msg.cellB = req.cellB;
            events.Emit(msg.Encode());
            NetDebug.Log("host: swap " + req.cellA + "<->" + req.cellB + " from slot " + fromSlot + " accepted, LAB_SWAPPED at tick " + msg.header.tick);
        }

        void OnBend(byte fromSlot, byte[] payload, WorldSim sim, LabyrinthDef def, uint tick, EventSink events)
        {
            CompassBendReqMsg req;
            if (!CompassBendReqMsg.TryDecode(payload, out req)) return;
            LabyrinthGrid grid = sim.Labyrinth.Grid;
            if (grid == null) return;
            uint cooldown = Ticks(def != null ? def.bendCooldownSeconds : 15f);
            string what = "bend mask=" + req.slots + " target=" + req.target + " cell=" + req.cell + " from slot " + fromSlot;
            if (_bendUsed[fromSlot] && tick < _lastBendTick[fromSlot] + cooldown)
            {
                NetDebug.Log("host: " + what + " refused: cooldown, " + (_lastBendTick[fromSlot] + cooldown - tick) + " ticks left");
                return;
            }
            if (req.target == CompassTargetKind.Cell && !grid.InRange(req.cell))
            {
                NetDebug.Log("host: " + what + " refused: cell out of range");
                return;
            }

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
            if (nonMage == 0 || !changes)
            {
                NetDebug.Log("host: " + what + " refused: " + (nonMage == 0 ? "no non-Mage players present" : "nothing would change (a named slot is absent, a Mage, or already set that way)"));
                return;
            }

            // The minority rule: strictly fewer than half of the non-Mage players may be lied to at once,
            // counting both Mages' work together, so two of them cannot bend the whole room between them.
            // BUT a strict minority of 1 or 2 non-Mages is ZERO, which used to make every bend in the
            // tutorial and in any small test silently impossible; LabyrinthDef.minBendTargets is the floor.
            int maxBent = def != null ? def.MaxBentFor(nonMage) : (nonMage > 0 ? 1 : 0);
            if (bentAfter > maxBent)
            {
                NetDebug.Log("host: " + what + " refused: " + bentAfter + " bent would exceed the limit of " + maxBent + " for " + nonMage + " non-Mages");
                return;
            }

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
                NetDebug.Log("host: " + what + " accepted: COMPASS_TARGETS(" + req.target + (bend && req.target == CompassTargetKind.Cell ? " " + req.cell : "") + ") sent to slot " + i + " (" + bentAfter + "/" + maxBent + " bent)");
            }
        }

        // ---------------------------------------------------------------- the Mage's third power: nudge / pull

        /// <summary>
        /// NUDGE_REQ. The asker is already known to be a Mage in an open round (Handle). Checked here: his
        /// cooldown, a real target (another player holding a weapon, or the tutorial dummy), that it is in
        /// the air, in range of his own streamed body, and in his line of sight on this host's scene. Then
        /// NUDGE_EVENT goes to everybody WITHOUT the asker's slot, and the target's owner applies it. A
        /// refusal goes back to the asker alone, with the reason, so his screen can say it.
        /// Two fragments: a nudge lands at twoFragmentScale unless the OTHER fragment nudged the same target
        /// within coSignWindow, which makes the second one land at full strength.
        /// </summary>
        void OnNudge(byte fromSlot, byte[] payload, WorldSim sim, LabyrinthDef def, uint tick, EventSink events)
        {
            NudgeReqMsg req;
            if (!NudgeReqMsg.TryDecode(payload, out req)) return;
            PlayerState from = sim.Players[fromSlot];
            if (from == null || !from.present) return;
            // A banished player has no powers, and nothing moves while the game is paused (docs/VOTING.md).
            if (sim.Vote.IsBanished(fromSlot) || sim.Pause.Paused)
            {
                NetDebug.Log("host: nudge from slot " + fromSlot + " refused: banished or paused");
                return;
            }
            uint game = sim.Pause.GameTick(tick);
            bool practice = req.targetSlot == NudgeReqMsg.PracticeTarget;
            string what = "nudge " + req.mode + " on " + (practice ? "the practice dummy" : "slot " + req.targetSlot) + " from slot " + fromSlot;

            uint cooldown = Ticks(def != null ? def.nudgeCooldown : 8f);
            if (_nudgeUsed[fromSlot] && game < _lastNudgeTick[fromSlot] + cooldown)
            {
                RefuseNudge(events, fromSlot, req.targetSlot, NudgeRefusal.Cooldown, _lastNudgeTick[fromSlot] + cooldown - game, what);
                return;
            }

            IHostWorld world = events.World;
            Vector3 targetPos;
            bool airborne;
            int targetIndex;
            if (practice)
            {
                if (world == null || !world.TryGetPracticeTarget(out targetPos, out airborne))
                {
                    RefuseNudge(events, fromSlot, req.targetSlot, NudgeRefusal.NoTarget, 0, what);
                    return;
                }
                targetIndex = Wire.MaxPlayers;
            }
            else
            {
                PlayerState target = req.targetSlot < Wire.MaxPlayers ? sim.Players[req.targetSlot] : null;
                // Never yourself, never an empty slot, never a soul: only a body a player is driving.
                if (req.targetSlot == fromSlot || target == null || !target.present || target.weaponId == Wire.NoId
                    || (target.poseFlags & PoseFlags.Soul) != 0 || target.poseCount == 0)
                {
                    RefuseNudge(events, fromSlot, req.targetSlot, NudgeRefusal.NoTarget, 0, what);
                    return;
                }
                targetPos = target.pos;
                airborne = (target.poseFlags & PoseFlags.Airborne) != 0;
                targetIndex = req.targetSlot;
            }

            bool needAir = def == null || def.requireAirborne;
            if (needAir && !airborne)
            {
                RefuseNudge(events, fromSlot, req.targetSlot, NudgeRefusal.NotAirborne, 0, what);
                return;
            }

            float range = def != null ? def.nudgeRange : 12f;
            Vector3 toTarget = targetPos - from.pos;
            if (from.poseCount == 0 || toTarget.sqrMagnitude > range * range)
            {
                RefuseNudge(events, fromSlot, req.targetSlot, NudgeRefusal.OutOfRange, 0, what);
                return;
            }

            // Lifted a little at both ends, so a body resting on a floor does not start its line inside it.
            Vector3 lift = new Vector3(0f, 0.3f, 0f);
            if (world != null && !world.HasLineOfSight(from.pos + lift, targetPos + lift))
            {
                RefuseNudge(events, fromSlot, req.targetSlot, NudgeRefusal.NoLineOfSight, 0, what);
                return;
            }

            // NUDGE pushes along the Mage's view (away from where he looks from); PULL draws toward his body.
            // A view that does not point at the target at all is replaced by the line to it.
            Vector3 dir;
            float strength;
            if (req.mode == NudgeMode.Pull)
            {
                dir = -toTarget;
                strength = def != null ? def.pullImpulse : 4f;
            }
            else
            {
                dir = req.viewDir;
                if (dir.sqrMagnitude < 0.01f || Vector3.Dot(dir, toTarget) <= 0f) dir = toTarget;
                strength = def != null ? def.nudgeImpulse : 4f;
            }
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
            dir.Normalize();

            // The two-fragment rule, in its simple form: reduced, unless both fragments pick the same target.
            int mages = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim.Players[i];
                if (_mage[i] && p != null && p.present) mages++;
            }
            float scale = 1f;
            bool coSigned = false;
            if (mages >= 2)
            {
                uint window = (uint)Mathf.RoundToInt((def != null ? def.coSignWindow : 1f) * Protocol.Tick.PerSecond);
                byte by = _nudgedBy[targetIndex];
                coSigned = by != Wire.NoSlot && by != fromSlot && by < Wire.MaxPlayers && _mage[by]
                    && tick - _nudgedAtTick[targetIndex] <= window;
                scale = coSigned ? 1f : (def != null ? def.twoFragmentScale : 0.6f);
            }

            _nudgeUsed[fromSlot] = true;
            _lastNudgeTick[fromSlot] = game;
            // A co-signed pair is spent: a later nudge starts a new pair.
            _nudgedBy[targetIndex] = coSigned ? Wire.NoSlot : fromSlot;
            _nudgedAtTick[targetIndex] = tick;

            NudgeEventMsg msg = new NudgeEventMsg();
            msg.header = events.Header();
            msg.targetSlot = req.targetSlot;
            msg.mode = req.mode;
            // No author on it, anywhere: the one thing it must never say is who did it.
            msg.velocityChange = dir * (strength * scale);
            events.Emit(msg.Encode());
            NetDebug.Log("host: " + what + " accepted: NUDGE_EVENT dv=" + msg.velocityChange.magnitude.ToString("0.0") + " m/s"
                + (mages >= 2 ? (coSigned ? " (co-signed, full strength)" : " (one fragment, x" + scale.ToString("0.00") + ")") : ""));
        }

        /// <summary>To the asking Mage alone: why not. NetDebug says it on the host's own console too.</summary>
        static void RefuseNudge(EventSink events, byte fromSlot, byte targetSlot, NudgeRefusal reason, uint waitTicks, string what)
        {
            NudgeRefusedMsg msg = new NudgeRefusedMsg();
            msg.reason = reason;
            msg.targetSlot = targetSlot;
            uint tenths = (waitTicks * 10u + (uint)Protocol.Tick.PerSecond - 1u) / (uint)Protocol.Tick.PerSecond;
            msg.waitTenths = (ushort)(tenths > 65535u ? 65535u : tenths);
            events.Reply(fromSlot, msg.Encode());
            NetDebug.Log("host: " + what + " refused: " + reason + (waitTicks > 0 ? ", " + waitTicks + " ticks left" : ""));
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
    

        /// <summary>True when the session's data plays the simplified run rather than the labyrinth grid.</summary>
        public static bool IsRunMode(WorldSim sim)
        {
            return sim != null && sim.Data != null && sim.Data.mode == GameMode.Run;
        }

        /// <summary>A round is open: roles are drawn and the Mage's powers answer. Read by RunRule on the same host.</summary>
        public bool RoundOpen { get { return _roundOpen; } }

        /// <summary>HOST ONLY: is this slot a Mage fragment. Never leaves this process; RunRule reads it to validate a curse and to name the Mages on ROUND_RESULT.</summary>
        public bool IsMage(int slot)
        {
            return slot >= 0 && slot < Wire.MaxPlayers && _mage[slot];
        }

        /// <summary>HOST ONLY: one bit per present Mage slot, for the one message that ever names them.</summary>
        public byte MageMask(WorldSim sim)
        {
            byte mask = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim != null ? sim.Players[i] : null;
                if (_mage[i] && p != null && p.present) mask |= (byte)(1 << i);
            }
            return mask;
        }
}
}
