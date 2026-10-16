using NUnit.Framework;
using Pesky.Protocol;
using Pesky.Session.Analytics;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Tests
{
    /// <summary>
    /// The watcher against a real WorldSim, driven the way the host's sim is: phase, layout, poses, the run timer,
    /// a vote's pause, ROUND_RESULT with SESSION_END on the same tick. A pose's x is its place in the run:
    /// 0-4 the five rooms, 5 the Exit room, negative the start room.
    /// </summary>
    public sealed class RunAnalyticsWatcherTests
    {
        const int Rooms = 5;
        static readonly int PerSecond = Tick.PerSecond;

        WorldSim _sim;
        RunAnalyticsWatcher _watcher;
        string _finished;
        ushort _seq;

        void SetUp(int players)
        {
            _sim = new WorldSim(null, 1u);
            for (int i = 0; i < players; i++) _sim.Players[i].present = true;
            _sim.AdvanceTo(10);
            _sim.Apply(new SessionPhaseMsg { header = new EventHeader(10), phase = SessionPhase.Playing });
            _sim.Apply(new RunLayoutMsg { roomIds = new ushort[] { 3, 4, 5, 6, 7 } });
            _watcher = new RunAnalyticsWatcher(v => v.x < 0f ? -1 : Mathf.Min(Rooms, (int)v.x), id => "Room " + id, "test");
            _finished = null;
            _watcher.Finished += json => _finished = json;
        }

        void Pose(int slot, float place)
        {
            _sim.SetPlayerPose((byte)slot, 0, new Vector3(place, 0f, 0f), Quaternion.identity, Vector3.zero, ++_seq);
        }

        void At(float seconds)
        {
            _sim.AdvanceTo(100u + (uint)Mathf.RoundToInt(seconds * PerSecond));
            _watcher.Observe(_sim);
        }

        void StartRun()
        {
            _sim.AdvanceTo(100);
            uint start = _sim.Pause.GameTick(100);
            _sim.Apply(new RunStartMsg { header = new EventHeader(100), startTick = start, deadlineTick = start + (uint)(480 * PerSecond) });
            _watcher.Observe(_sim);
        }

        uint TickAt(float seconds) { return 100u + (uint)Mathf.RoundToInt(seconds * PerSecond); }

        [Test]
        public void ARunWithTwoPlayersIsNotRecorded()
        {
            SetUp(2);
            StartRun();
            Assert.IsNull(_watcher.Report);
        }

        [Test]
        public void ARunsLengthLeavesOutItsVotesEvenWhenTheEndResetsThePause()
        {
            SetUp(3);
            StartRun();
            Assert.IsTrue(_watcher.Recording);

            At(60f);
            _sim.Apply(new PauseBeginMsg { header = new EventHeader(TickAt(60f)), reason = PauseReason.Vote });
            At(70f);
            _sim.Apply(new PauseEndMsg { header = new EventHeader(TickAt(80f)), reason = PauseReason.Vote });
            At(80f);
            At(200f);

            // The result and the session's end arrive on the same tick, as RunRule.Finish emits them.
            uint end = TickAt(300f);
            _sim.AdvanceTo(end);
            _sim.Apply(new RoundResultMsg { header = new EventHeader(end), outcome = RoundOutcome.Escaped, mageMask = 1 });
            _sim.Apply(new SessionEndMsg { header = new EventHeader(end), reason = SessionEndReason.Escaped });
            _watcher.Observe(_sim);

            Assert.IsNotNull(_finished);
            Assert.AreEqual("result", _watcher.Report.Run.ended);
            Assert.AreEqual(280.0, _watcher.Report.Run.duration_s, 0.05, "300 s on the clock minus the 20 s meeting.");
        }

        [Test]
        public void PosesLeftOverFromTheLastRunDoNotCountAsRoomsReached()
        {
            SetUp(3);
            // Where the last run left everybody: the Exit room.
            for (int i = 0; i < 3; i++) Pose(i, 5f);
            StartRun();
            At(1f);
            Assert.AreEqual(0, _watcher.Report.Run.rooms_reached, "A pose from before the run says nothing about it.");

            Pose(0, -1f);
            At(2f);
            Pose(0, 0f);
            At(12f);
            Assert.AreEqual(1, _watcher.Report.Run.rooms_reached);
            Assert.AreEqual(12.0, _watcher.Report.Rooms[0].entered_s, 0.05);
        }

        [Test]
        public void AJumpPastTheNextRoomIsIgnored()
        {
            SetUp(3);
            StartRun();
            Pose(0, 0f);
            At(5f);
            Pose(1, 5f);
            At(6f);
            Assert.AreEqual(1, _watcher.Report.Run.rooms_reached, "Doors join room k to k + 1 only.");

            Pose(0, 1f);
            At(40f);
            Assert.AreEqual(2, _watcher.Report.Run.rooms_reached);
            Assert.IsTrue(_watcher.Report.Rooms[0].cleared);
            Assert.AreEqual(35.0, _watcher.Report.Rooms[0].seconds, 0.05);
        }
    }
}
