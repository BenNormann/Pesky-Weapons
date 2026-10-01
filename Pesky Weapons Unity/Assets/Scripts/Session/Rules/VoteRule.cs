using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// The host's VOTE (docs/VOTING.md). A VOTE_CALL_REQ is checked (present, not banished, a call left, the
    /// group cooldown, the run timer running for voteNoVoteBeforeSeconds, nothing else paused), then the game
    /// freezes (PAUSE_BEGIN, reason Vote) and the meeting opens (VOTE_START) at the same tick. Each eligible
    /// voter casts ONE final VOTE_CAST_REQ, broadcast as a VOTE_TALLY (votes are public). At the deadline, or
    /// as soon as every eligible voter present has voted, the meeting resolves: the plurality of the votes
    /// for players wins; a tie, or SKIP with at least as many votes as the top player, banishes nobody.
    /// VOTE_END names the banished player and reveals that player's role (read from LabyrinthRule's secret
    /// table on this host, never copied), the banished body breaks (WEAPON_BROKEN, HostForced) and
    /// voteResultSeconds later PAUSE_END unfreezes everybody. The tutorial's practice vote
    /// (VoteCallReqMsg.candidate = VoteTarget.Dummy) skips the limits and the timing and has the dummy as
    /// its only candidate. Runs only in run mode.
    /// </summary>
    public sealed class VoteRule : IHostRule, IIntentValidator
    {
        readonly LabyrinthRule _roles;
        // One count per candidate slot, plus the dummy at index MaxPlayers.
        readonly int[] _count = new int[Wire.MaxPlayers + 1];

        bool _roundOpen;
        /// <summary>The sim tick PAUSE_END goes out at, once a meeting has resolved; 0 = none pending.</summary>
        uint _unpauseTick;

        public VoteRule(LabyrinthRule roles)
        {
            _roles = roles;
        }

        // ---------------------------------------------------------------- the rule

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null || !LabyrinthRule.IsRunMode(sim)) return;
            if (sim.Phase != SessionPhase.Playing)
            {
                _roundOpen = false;
                _unpauseTick = 0;
                return;
            }
            _roundOpen = true;
            VoteState vote = sim.Vote;
            if (vote.MeetingOpen)
            {
                if (tick >= vote.DeadlineTick || vote.AllVoted(sim.Players)) Resolve(sim, tick, events);
                return;
            }
            if (_unpauseTick != 0 && tick >= _unpauseTick)
            {
                _unpauseTick = 0;
                if (sim.Pause.Paused && sim.Pause.Reason == PauseReason.Vote)
                    events.Emit(PauseState.EndPayload(events.Header(), PauseReason.Vote));
                NetDebug.Log("host: vote result shown, game unpaused");
            }
        }

        // ---------------------------------------------------------------- the two intents

        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null) return;
            byte id = MessageInfo.IdOf(payload);
            if (id == MsgId.VoteCallReq) OnCall(fromSlot, payload, sim, tick, events);
            else if (id == MsgId.VoteCastReq) OnCast(fromSlot, payload, sim, tick, events);
        }

        void OnCall(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            VoteCallReqMsg req;
            if (!VoteCallReqMsg.TryDecode(payload, out req)) return;
            bool practice = req.candidate == VoteTarget.Dummy;
            string what = "vote call from slot " + fromSlot + (practice ? " (practice)" : "");
            PlayerState from = fromSlot < Wire.MaxPlayers ? sim.Players[fromSlot] : null;
            if (!_roundOpen || !LabyrinthRule.IsRunMode(sim) || from == null || !from.present)
            {
                Refuse(events, fromSlot, VoteRefusal.NoRound, 0, what);
                return;
            }
            VoteState vote = sim.Vote;
            PauseState pause = sim.Pause;
            RunDef def = sim.Data != null ? sim.Data.run : null;
            if (vote.IsBanished(fromSlot))
            {
                Refuse(events, fromSlot, VoteRefusal.Banished, 0, what);
                return;
            }
            if (pause.Paused || vote.MeetingOpen || _unpauseTick != 0)
            {
                Refuse(events, fromSlot, VoteRefusal.AlreadyPaused, 0, what);
                return;
            }
            uint game = pause.GameTick(tick);
            if (practice)
            {
                Vector3 at;
                bool airborne;
                IHostWorld world = events.World;
                if (world == null || !world.TryGetPracticeTarget(out at, out airborne))
                {
                    Refuse(events, fromSlot, VoteRefusal.NoTarget, 0, what);
                    return;
                }
            }
            else
            {
                int calls = def != null ? def.voteCallsPerPlayer : 1;
                if (vote.CallsUsed(fromSlot) >= calls)
                {
                    Refuse(events, fromSlot, VoteRefusal.NoCallsLeft, 0, what);
                    return;
                }
                if (vote.HasResult)
                {
                    uint ready = vote.LastEndGameTick + LabyrinthRule.Ticks(def != null ? def.voteGroupCooldown : 45f);
                    if (game < ready)
                    {
                        Refuse(events, fromSlot, VoteRefusal.GroupCooldown, ready - game, what);
                        return;
                    }
                }
                RunState run = sim.Run;
                if (!run.Started)
                {
                    Refuse(events, fromSlot, VoteRefusal.TooEarly, 0, what);
                    return;
                }
                uint early = run.StartTick + LabyrinthRule.Ticks(def != null ? def.voteNoVoteBeforeSeconds : 30f);
                if (game < early)
                {
                    Refuse(events, fromSlot, VoteRefusal.TooEarly, early - game, what);
                    return;
                }
            }

            // Freeze first, then open the meeting: both take effect at the same tick, in this order, everywhere.
            events.Emit(PauseState.BeginPayload(events.Header(), PauseReason.Vote));

            VoteStartMsg start = new VoteStartMsg();
            start.header = events.Header();
            start.caller = fromSlot;
            start.deadlineTick = start.header.tick + LabyrinthRule.Ticks(def != null ? def.voteMeetingSeconds : 25f);
            byte eligible = 0;
            byte candidates = 0;
            if (practice)
            {
                eligible = (byte)(1 << fromSlot);
                start.flags = VoteFlags.Practice;
            }
            else
            {
                for (int i = 0; i < Wire.MaxPlayers; i++)
                {
                    PlayerState p = sim.Players[i];
                    if (p == null || !p.present || vote.IsBanished(i)) continue;
                    eligible |= (byte)(1 << i);
                    candidates |= (byte)(1 << i);
                }
            }
            start.eligibleMask = eligible;
            start.candidateMask = candidates;
            events.Emit(start.Encode());
            NetDebug.Log("host: " + what + " accepted: PAUSE_BEGIN + VOTE_START, deadline tick " + start.deadlineTick
                + ", voters 0x" + eligible.ToString("X2") + ", candidates 0x" + candidates.ToString("X2"));
        }

        void OnCast(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            VoteCastReqMsg req;
            if (!VoteCastReqMsg.TryDecode(payload, out req)) return;
            VoteState vote = sim.Vote;
            string what = "vote cast from slot " + fromSlot + " for " + TargetName(req.target);
            if (!vote.MeetingOpen)
            {
                Refuse(events, fromSlot, VoteRefusal.NoMeeting, 0, what);
                return;
            }
            if (vote.IsBanished(fromSlot))
            {
                Refuse(events, fromSlot, VoteRefusal.Banished, 0, what);
                return;
            }
            if (!vote.IsEligible(fromSlot))
            {
                Refuse(events, fromSlot, VoteRefusal.NotEligible, 0, what);
                return;
            }
            if (vote.HasVoted(fromSlot))
            {
                Refuse(events, fromSlot, VoteRefusal.AlreadyVoted, 0, what);
                return;
            }
            byte t = req.target;
            bool ok = t == VoteTarget.Skip
                || (vote.Practice ? t == VoteTarget.Dummy : (VoteTarget.IsPlayer(t) && vote.IsCandidate(t)));
            if (!ok)
            {
                Refuse(events, fromSlot, VoteRefusal.BadTarget, 0, what);
                return;
            }

            VoteTallyMsg tally = new VoteTallyMsg();
            tally.header = events.Header();
            tally.voter = fromSlot;
            tally.target = t;
            events.Emit(tally.Encode());
            NetDebug.Log("host: " + what + " accepted: VOTE_TALLY");

            // Everybody is in: resolve now rather than on the next tick.
            if (vote.AllVoted(sim.Players)) Resolve(sim, tick, events);
        }

        // ---------------------------------------------------------------- the result

        /// <summary>
        /// Plurality among the votes for candidates; a tie between the top candidates, or SKIP with at least
        /// as many votes as the top candidate, banishes nobody. Players who did not vote count for nothing.
        /// </summary>
        void Resolve(WorldSim sim, uint tick, EventSink events)
        {
            VoteState vote = sim.Vote;
            RunDef def = sim.Data != null ? sim.Data.run : null;
            for (int i = 0; i <= Wire.MaxPlayers; i++) _count[i] = 0;
            int skip = 0;
            VoteEndMsg end = new VoteEndMsg();
            end.tally = new byte[Wire.MaxPlayers];
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                byte t = vote.VoteOf(i);
                end.tally[i] = t;
                if (t == VoteTarget.None) continue;
                if (t == VoteTarget.Skip) skip++;
                else if (t == VoteTarget.Dummy) _count[Wire.MaxPlayers]++;
                else if (VoteTarget.IsPlayer(t)) _count[t]++;
            }
            int top = 0;
            int topIndex = -1;
            bool tie = false;
            for (int i = 0; i <= Wire.MaxPlayers; i++)
            {
                if (_count[i] == 0) continue;
                if (_count[i] > top)
                {
                    top = _count[i];
                    topIndex = i;
                    tie = false;
                }
                else if (_count[i] == top) tie = true;
            }
            byte banished = VoteTarget.None;
            if (top > 0 && !tie && skip < top) banished = topIndex == Wire.MaxPlayers ? VoteTarget.Dummy : (byte)topIndex;

            end.header = events.Header();
            end.banished = banished;
            // The reveal: LabyrinthRule's table, read here and sent only because the player is out for good.
            end.role = VoteTarget.IsPlayer(banished) && _roles != null && _roles.IsMage(banished) ? LabyrinthRole.Mage : LabyrinthRole.Weapon;
            events.Emit(end.Encode());

            // The banished body breaks through the normal break path; the soul that pops out is a ghost from here on.
            if (VoteTarget.IsPlayer(banished))
            {
                PlayerState p = sim.Players[banished];
                if (p != null && p.weaponId != Wire.NoId) events.Emit(WeaponRule.Broken(events, p.weaponId, BreakCause.HostForced));
            }
            _unpauseTick = tick + LabyrinthRule.Ticks(def != null ? def.voteResultSeconds : 4f);
            NetDebug.Log("host: vote resolved: " + (banished == VoteTarget.None ? "nobody banished" : TargetName(banished) + " banished as " + end.role)
                + " (top " + top + ", skip " + skip + (tie ? ", tie" : "") + ")");
        }

        static string TargetName(byte target)
        {
            if (target == VoteTarget.Skip) return "SKIP";
            if (target == VoteTarget.Dummy) return "the practice dummy";
            if (target == VoteTarget.None) return "nobody";
            return "slot " + target;
        }

        static void Refuse(EventSink events, byte fromSlot, VoteRefusal reason, uint waitTicks, string what)
        {
            VoteRefusedMsg msg = new VoteRefusedMsg();
            msg.reason = reason;
            uint tenths = (waitTicks * 10u + (uint)Protocol.Tick.PerSecond - 1u) / (uint)Protocol.Tick.PerSecond;
            msg.waitTenths = (ushort)(tenths > 65535u ? 65535u : tenths);
            events.Reply(fromSlot, msg.Encode());
            NetDebug.Log("host: " + what + " refused: " + reason + (waitTicks > 0 ? ", " + waitTicks + " ticks left" : ""));
        }
    }
}
