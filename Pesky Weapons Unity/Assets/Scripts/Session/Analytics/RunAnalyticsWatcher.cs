using System;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Session.Analytics
{
    /// <summary>
    /// The host's analytics recorder (docs/ANALYTICS.md). Observe it once a frame with the host's sim: it
    /// watches the shared state everybody already has (the run timer, the players' streamed poses, the
    /// vote's results, ROUND_RESULT) and builds one RunReport per run. It sends nothing and changes
    /// nothing; the owner decides what to do with the two events.
    ///
    /// A run is recorded only when at least <see cref="MinPlayers"/> players were present the moment its
    /// timer started. The count is taken once: a run that loses players later is still recorded, a run
    /// that gains them is not.
    /// </summary>
    public sealed class RunAnalyticsWatcher
    {
        public const int MinPlayers = 3;

        readonly Func<Vector3, int> _positionAt;
        readonly Func<int, string> _labelOf;
        readonly string _build;

        RunReport _report;
        bool _live;
        uint _startTick;
        uint _lastVoteEnd;
        readonly uint[] _poseAtStart = new uint[Wire.MaxPlayers];

        // The pause bookkeeping as of the last frame the round was Playing. SESSION_END lands on the same tick as
        // ROUND_RESULT and a phase change resets PauseState, so by the frame the result is seen, GameTick() has
        // forgotten every vote's pause and a run's length would count its meetings.
        uint _pausedTicks;
        bool _paused;
        uint _pauseStart;

        /// <summary>The run's report so far, held for a page that closes mid-run. Raised whenever it changed.</summary>
        public event Action<string> Changed;

        /// <summary>The run is over (its result, or abandoned): the closed report, to send.</summary>
        public event Action<string> Finished;

        /// <summary>The run being recorded, or the last one; null before the first recorded run.</summary>
        public RunReport Report { get { return _report; } }

        public bool Recording { get { return _report != null && !_report.Closed; } }

        /// <param name="positionAt">Where a world point is in the run: 0 to Count - 1 for its rooms, Count for the Exit room, -1 anywhere else.</param>
        /// <param name="labelOf">A room id's name for the Sheet.</param>
        /// <param name="build">The game's version string.</param>
        public RunAnalyticsWatcher(Func<Vector3, int> positionAt, Func<int, string> labelOf, string build)
        {
            _positionAt = positionAt;
            _labelOf = labelOf;
            _build = build ?? "";
        }

        public void Observe(WorldSim sim)
        {
            if (sim == null) return;
            if (sim.Phase == SessionPhase.Playing)
            {
                _pausedTicks = sim.Pause.PausedTotalTicks;
                _paused = sim.Pause.Paused;
                _pauseStart = sim.Pause.PauseStartTick;
            }
            RunState run = sim.Run;
            bool live = sim.Phase == SessionPhase.Playing && run.Started;
            bool started = live && (!_live || run.StartTick != _startTick);
            _live = live;

            if (Recording)
            {
                if (sim.Labyrinth.Outcome != RoundOutcome.None)
                {
                    _report.Finish(sim.Labyrinth.Outcome.ToString(), Bits(sim.Labyrinth.MageMask), Present(sim),
                        SecondsAt(sim, GameTickAt(sim.Labyrinth.OutcomeTick)));
                    Raise(Finished, _report.ToJson(0f));
                    return;
                }
                // The round closed with no result (back to the lobby), or a new run began under it.
                if (!live || started) Abandon(sim);
                else
                {
                    if (Track(sim)) Raise(Changed, _report.ToJson(Now(sim)));
                    return;
                }
            }

            if (!started || sim.Labyrinth.Outcome != RoundOutcome.None) return;
            _startTick = run.StartTick;
            _lastVoteEnd = sim.Vote.HasResult ? sim.Vote.LastEndGameTick : 0u;
            // A pose from before the run (where the last run left everybody, the Exit room) says nothing about this one.
            for (int i = 0; i < _poseAtStart.Length && i < sim.Players.Count; i++)
                _poseAtStart[i] = sim.Players[i] != null ? sim.Players[i].poseCount : 0u;
            int players = Present(sim);
            if (players < MinPlayers)
            {
                NetDebug.Log("analytics: run not recorded, " + players + " player(s) at its start (needs " + MinPlayers + ")");
                return;
            }
            float timer = run.DeadlineTick > run.StartTick ? (run.DeadlineTick - run.StartTick) / (float)Protocol.Tick.PerSecond : 0f;
            string id = Guid.NewGuid().ToString("N").Substring(0, 12);
            _report = new RunReport(id, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), _build, players, run.Count, timer);
            NetDebug.Log("analytics: recording run " + id + " with " + players + " players");
            Track(sim);
            Raise(Changed, _report.ToJson(Now(sim)));
        }

        /// <summary>The host is leaving mid-run (the scene closing): close the report as abandoned and raise Finished.</summary>
        public void Abandon(WorldSim sim)
        {
            if (!Recording || sim == null) return;
            _report.Abandon(Present(sim), Now(sim));
            NetDebug.Log("analytics: run " + _report.Run.run_id + " abandoned");
            Raise(Finished, _report.ToJson(0f));
        }

        /// <summary>Rooms first entered and meetings ended since the last frame. True when the report changed.</summary>
        bool Track(WorldSim sim)
        {
            bool changed = false;
            float now = Now(sim);
            int before = _report.Run.rooms_reached;
            for (int i = 0; i < sim.Players.Count; i++)
            {
                PlayerState p = sim.Players[i];
                // A ghost passes the doors freely (docs/VOTING.md), so only the living say how far the run got; and
                // only a pose streamed since the run began.
                if (p == null || !p.present || p.poseCount <= _poseAtStart[i] || sim.Vote.IsBanished(i)) continue;
                int at = _positionAt != null ? _positionAt(p.pos) : -1;
                // Doors join room k to room k + 1 only, so the next first entry is always the next position; a
                // jump further (a stale pose in the Exit room) is not a place anybody walked to.
                if (at != _report.Run.rooms_reached) continue;
                int roomId = at < sim.Run.Count ? sim.Run.RoomAt(at) : -1;
                _report.Entered(at, roomId, roomId >= 0 && _labelOf != null ? _labelOf(roomId) : "Exit", now);
            }
            if (_report.Run.rooms_reached != before) changed = true;

            VoteState vote = sim.Vote;
            if (vote.HasResult && vote.LastEndGameTick != _lastVoteEnd && !vote.Practice && vote.LastBanished != VoteTarget.Dummy)
            {
                _lastVoteEnd = vote.LastEndGameTick;
                string banished = !VoteTarget.IsPlayer(vote.LastBanished) ? "nobody"
                    : vote.LastRole == LabyrinthRole.Mage ? "mage" : "weapon";
                _report.Voted(SecondsAt(sim, vote.LastEndGameTick), Present(sim), banished);
                changed = true;
            }

            return changed;
        }

        float Now(WorldSim sim)
        {
            return SecondsAt(sim, GameTickAt(sim.Tick));
        }

        /// <summary>The game tick at a sim tick, from the pause bookkeeping of the last Playing frame (see _pausedTicks).</summary>
        uint GameTickAt(uint tick)
        {
            if (_paused && tick > _pauseStart) tick = _pauseStart;
            return tick > _pausedTicks ? tick - _pausedTicks : 0u;
        }

        float SecondsAt(WorldSim sim, uint gameTick)
        {
            return gameTick > _startTick ? (gameTick - _startTick) / (float)Protocol.Tick.PerSecond : 0f;
        }

        static int Present(WorldSim sim)
        {
            int n = 0;
            for (int i = 0; i < sim.Players.Count; i++)
            {
                PlayerState p = sim.Players[i];
                if (p != null && p.present) n++;
            }
            return n;
        }

        static int Bits(byte mask)
        {
            int n = 0;
            for (; mask != 0; mask &= (byte)(mask - 1)) n++;
            return n;
        }

        static void Raise(Action<string> handler, string json)
        {
            if (handler != null) handler(json);
        }
    }
}
