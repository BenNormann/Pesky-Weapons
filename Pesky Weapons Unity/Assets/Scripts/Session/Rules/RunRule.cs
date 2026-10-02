using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// The host's brain for the simplified RUN (docs/RUN.md): picks the run's rooms from the pool, starts
    /// the timer when the first player leaves the start room, ends the round (the crew in the Exit room, or
    /// the deadline), and validates the Mage's five CURSES. It runs only when GameData.mode is Run; in the
    /// labyrinth it sits idle.
    ///
    /// It has no secret table of its own. Who is a Mage is LabyrinthRule's (the same ROLE_ASSIGN path as
    /// the labyrinth); this rule reads it on the host and never copies it anywhere. A curse is validated
    /// here because "is the asker a Mage" is that table, and CURSE_EVENT leaves with no author.
    ///
    /// THE POOL IS THE SCENE. The room ids a run may use are authored (RunDirector.pool), so the rule
    /// waits until the host's own scene answers CollectRunPool, then draws roomsPerRun distinct rooms from
    /// the seed and broadcasts RUN_LAYOUT. Until then no door leads anywhere, which is a second or so.
    /// </summary>
    public sealed class RunRule : IHostRule, IIntentValidator
    {
        /// <summary>Where everybody is, five times a second. Not frame critical.</summary>
        const int ScanEveryTicks = 4;

        readonly LabyrinthRule _roles;
        readonly RunPlace[] _place = new RunPlace[Wire.MaxPlayers];
        readonly List<ushort> _pool = new List<ushort>(32);
        readonly uint[] _lastCurseTick = new uint[Wire.MaxPlayers];
        readonly bool[] _curseUsed = new bool[Wire.MaxPlayers];

        bool _roundOpen;
        bool _layoutSent;
        bool _finished;
        uint _nextScanTick;

        public RunRule(LabyrinthRule roles)
        {
            _roles = roles;
        }

        // ---------------------------------------------------------------- the rule

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null || !LabyrinthRule.IsRunMode(sim)) return;
            if (sim.Phase != SessionPhase.Playing)
            {
                if (_roundOpen) CloseRound();
                return;
            }
            if (!_roundOpen)
            {
                OpenRound(sim);
                return;
            }
            if (_finished || tick < _nextScanTick) return;
            _nextScanTick = tick + ScanEveryTicks;

            RunDef def = sim.Data != null ? sim.Data.run : null;
            IHostWorld world = events.World;
            if (!_layoutSent)
            {
                // The host's scene is not up yet on the first Playing ticks (it is still loading). Ask again
                // on every scan until it answers with its pool.
                if (world == null || !world.CollectRunPool(_pool) || _pool.Count == 0) return;
                SendLayout(sim, def, events);
                return;
            }

            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _place[i] = RunPlace.Unknown;
                PlayerState p = sim.Players[i];
                if (p == null || !p.present) _curseUsed[i] = false;
            }
            if (world == null || !world.CollectRunPlaces(_place)) return;

            RunState run = sim.Run;
            if (!run.Started)
            {
                if (AnyoneLeftTheStart(sim)) StartTimer(sim, def, tick, events);
                return;
            }
            CheckEndings(sim, tick, events);
        }

        void OpenRound(WorldSim sim)
        {
            _roundOpen = true;
            _layoutSent = false;
            _finished = false;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _lastCurseTick[i] = 0;
                _curseUsed[i] = false;
                _place[i] = RunPlace.Unknown;
            }
            _nextScanTick = sim.Tick + ScanEveryTicks;
        }

        void CloseRound()
        {
            _roundOpen = false;
            _layoutSent = false;
            _finished = false;
        }

        /// <summary>
        /// The configured guaranteed rooms first, in authored order, followed by enough distinct random
        /// pool rooms to reach roomsPerRun. Clients never derive it: RUN_LAYOUT carries the sequence, and
        /// the snapshot carries it to a late joiner.
        /// </summary>
        void SendLayout(WorldSim sim, RunDef def, EventSink events)
        {
            int want = def != null ? def.roomsPerRun : 5;
            uint seed = unchecked(sim.WorldSeed ^ (sim.PhaseStartTick * 2654435761u) ^ 0x52554Eu);
            ushort[] roomIds = RunLayoutPicker.Pick(_pool, def != null ? def.guaranteedRoomIds : null, want, seed);
            if (roomIds.Length == 0) return;
            RunLayoutMsg msg = new RunLayoutMsg();
            msg.roomIds = roomIds;
            events.Emit(msg.Encode());
            _layoutSent = true;
            NetDebug.Log("host: run layout " + Join(msg.roomIds) + " (pool of " + _pool.Count + ")");
        }

        static string Join(ushort[] ids)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(32);
            for (int i = 0; i < ids.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(ids[i]); }
            return sb.ToString();
        }

        bool AnyoneLeftTheStart(WorldSim sim)
        {
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim.Players[i];
                if (p == null || !p.present || sim.Vote.IsBanished(i)) continue;
                if (_place[i] == RunPlace.Rooms || _place[i] == RunPlace.Exit) return true;
            }
            return false;
        }

        /// <summary>The first player left the start room: everybody's clock starts from the same tick. GAME ticks (docs/VOTING.md): the deadline stands still through a pause.</summary>
        void StartTimer(WorldSim sim, RunDef def, uint tick, EventSink events)
        {
            RunStartMsg msg = new RunStartMsg();
            msg.header = events.Header();
            msg.startTick = sim.Pause.GameTick(msg.header.tick);
            msg.deadlineTick = msg.startTick + LabyrinthRule.Ticks(def != null ? def.timerSeconds : 300f);
            events.Emit(msg.Encode());
            NetDebug.Log("host: run timer started at game tick " + msg.startTick + ", deadline " + msg.deadlineTick);
        }

        /// <summary>
        /// Nothing ends while the game is paused. Then the deadline first (game ticks): the Mage wins the instant
        /// it passes, and it beats a simultaneous gathering. Then the crew: every present NON-Mage, NON-banished
        /// player inside the Exit room's volume at once (the Mage may be anywhere). A non-Mage with no pose yet
        /// counts as not there. If a banishment left no non-Mage standing, the Mage wins at once (WeaponsGone).
        /// </summary>
        void CheckEndings(WorldSim sim, uint tick, EventSink events)
        {
            if (sim.Pause.Paused) return;
            byte mageMask = _roles != null ? _roles.MageMask(sim) : (byte)0;
            if (sim.Pause.GameTick(tick) >= sim.Run.DeadlineTick)
            {
                Finish(sim, events, RoundOutcome.TimedOut, 0, mageMask, SessionEndReason.CrewLost);
                return;
            }

            int nonMage = 0;
            int banishedNonMage = 0;
            int inExit = 0;
            byte escapedMask = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = sim.Players[i];
                if (p == null || !p.present) continue;
                if ((mageMask & (1 << i)) != 0) continue;
                if (sim.Vote.IsBanished(i)) { banishedNonMage++; continue; }
                nonMage++;
                if (_place[i] != RunPlace.Exit) continue;
                inExit++;
                escapedMask |= (byte)(1 << i);
            }
            if (nonMage <= 0)
            {
                // Only when a vote emptied the crew: a round with no weapon at all (a solo Mage) just runs on.
                if (banishedNonMage > 0) Finish(sim, events, RoundOutcome.WeaponsGone, 0, mageMask, SessionEndReason.CrewLost);
                return;
            }
            if (inExit < nonMage) return;
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
            _finished = true;
            NetDebug.Log("host: run over: " + outcome);
        }

        // ---------------------------------------------------------------- the Mage's curses

        /// <summary>
        /// CURSE_REQ. Silent unless the asker is a Mage in an open round (like every Mage intent). Then, in
        /// order: a real curse id, his shared cooldown, a target that is another player holding a weapon
        /// (not a soul, not himself; or the tutorial dummy), within curseRange of his own streamed body.
        /// Accepted: CURSE_EVENT to everybody with the target, the curse and the duration, and NO author.
        /// Refused: CURSE_REFUSED to him alone.
        /// </summary>
        /// <summary>
        /// CURSE_REQ. Silent unless the asker is a Mage in an open round (like every Mage intent), not banished
        /// and not paused. Then, in order: a real curse id, his shared cooldown (game ticks), a target that is
        /// another player holding a weapon (not a soul, not himself; or the tutorial dummy), within curseRange
        /// of his own streamed body. Accepted: CURSE_EVENT to everybody with the target, the curse and the
        /// duration, and NO author. Refused: CURSE_REFUSED to him alone.
        /// </summary>
        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null || !_roundOpen || !LabyrinthRule.IsRunMode(sim) || _roles == null || !_roles.RoundOpen)
            {
                NetDebug.Log("host: curse from slot " + fromSlot + " refused: no round open");
                return;
            }
            if (fromSlot >= Wire.MaxPlayers || !_roles.IsMage(fromSlot))
            {
                NetDebug.Log("host: curse from slot " + fromSlot + " refused: not a Mage");
                return;
            }
            // A banished Mage has no powers; nothing is cast while the game is paused (docs/VOTING.md).
            if (sim.Vote.IsBanished(fromSlot) || sim.Pause.Paused)
            {
                NetDebug.Log("host: curse from slot " + fromSlot + " refused: banished or paused");
                return;
            }
            CurseReqMsg req;
            if (!CurseReqMsg.TryDecode(payload, out req)) return;
            PlayerState from = sim.Players[fromSlot];
            if (from == null || !from.present) return;
            RunDef def = sim.Data != null ? sim.Data.run : null;
            uint game = sim.Pause.GameTick(tick);
            bool practice = req.targetSlot == NudgeReqMsg.PracticeTarget;
            string what = "curse " + req.curse + " on " + (practice ? "the practice dummy" : "slot " + req.targetSlot) + " from slot " + fromSlot;

            if (req.curse < CurseKind.Magnetic || req.curse > CurseKind.Heavy)
            {
                Refuse(events, fromSlot, req.targetSlot, CurseRefusal.NoSuchCurse, 0, what);
                return;
            }

            uint cooldown = LabyrinthRule.Ticks(def != null ? def.curseCooldown : 30f);
            if (_curseUsed[fromSlot] && game < _lastCurseTick[fromSlot] + cooldown)
            {
                Refuse(events, fromSlot, req.targetSlot, CurseRefusal.Cooldown, _lastCurseTick[fromSlot] + cooldown - game, what);
                return;
            }

            Vector3 targetPos;
            if (practice)
            {
                bool airborne;
                IHostWorld world = events.World;
                if (world == null || !world.TryGetPracticeTarget(out targetPos, out airborne))
                {
                    Refuse(events, fromSlot, req.targetSlot, CurseRefusal.NoTarget, 0, what);
                    return;
                }
            }
            else
            {
                PlayerState target = req.targetSlot < Wire.MaxPlayers ? sim.Players[req.targetSlot] : null;
                // Never yourself, never an empty slot, never a free soul: only a body a player is driving.
                if (req.targetSlot == fromSlot || target == null || !target.present || target.weaponId == Wire.NoId
                    || (target.poseFlags & PoseFlags.Soul) != 0 || target.poseCount == 0)
                {
                    Refuse(events, fromSlot, req.targetSlot, CurseRefusal.NoTarget, 0, what);
                    return;
                }
                targetPos = target.pos;
            }

            float range = def != null ? def.curseRange : 15f;
            if (from.poseCount == 0 || (targetPos - from.pos).sqrMagnitude > range * range)
            {
                Refuse(events, fromSlot, req.targetSlot, CurseRefusal.OutOfRange, 0, what);
                return;
            }

            _curseUsed[fromSlot] = true;
            _lastCurseTick[fromSlot] = game;

            float seconds = def != null ? def.curseDuration : 20f;
            uint tenths = (uint)Mathf.RoundToInt(seconds * 10f);
            CurseEventMsg msg = new CurseEventMsg();
            msg.header = events.Header();
            msg.targetSlot = req.targetSlot;
            msg.curse = req.curse;
            msg.durationTenths = (ushort)(tenths > 65535u ? 65535u : tenths);
            // No author on it, anywhere: the one thing it must never say is who did it.
            events.Emit(msg.Encode());
            NetDebug.Log("host: " + what + " accepted: CURSE_EVENT for " + seconds.ToString("0.0") + " s");
        }

        static void Refuse(EventSink events, byte fromSlot, byte targetSlot, CurseRefusal reason, uint waitTicks, string what)
        {
            CurseRefusedMsg msg = new CurseRefusedMsg();
            msg.reason = reason;
            msg.targetSlot = targetSlot;
            uint tenths = (waitTicks * 10u + (uint)Protocol.Tick.PerSecond - 1u) / (uint)Protocol.Tick.PerSecond;
            msg.waitTenths = (ushort)(tenths > 65535u ? 65535u : tenths);
            events.Reply(fromSlot, msg.Encode());
            NetDebug.Log("host: " + what + " refused: " + reason + (waitTicks > 0 ? ", " + waitTicks + " ticks left" : ""));
        }
    }
}
