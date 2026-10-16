using NUnit.Framework;
using Pesky.Session.Analytics;
using UnityEngine;

namespace Pesky.Tests
{
    public sealed class RunReportTests
    {
        static RunReport NewReport()
        {
            return new RunReport("abc123", "2026-10-15 20:00:00", "0.1", 4, 5, 480f);
        }

        [Test]
        public void EnteringTheNextRoomClearsTheOneBeforeIt()
        {
            RunReport report = NewReport();
            report.Entered(0, 3, "Bat Room", 2f);
            report.Entered(1, 4, "Circuit Room", 62.5f);

            Assert.AreEqual(2, report.Rooms.Count);
            Assert.IsTrue(report.Rooms[0].cleared);
            Assert.AreEqual(60.5f, report.Rooms[0].seconds, 0.01f);
            Assert.IsFalse(report.Rooms[1].cleared);
            Assert.AreEqual(2, report.Run.rooms_reached);
        }

        [Test]
        public void OnlyTheFirstEntryCountsAndGoingBackChangesNothing()
        {
            RunReport report = NewReport();
            report.Entered(0, 3, "Bat Room", 2f);
            report.Entered(1, 4, "Circuit Room", 30f);
            report.Entered(0, 3, "Bat Room", 40f);
            report.Entered(1, 4, "Circuit Room", 50f);

            Assert.AreEqual(2, report.Rooms.Count);
            Assert.AreEqual(2f, report.Rooms[0].entered_s, 0.01f);
            Assert.AreEqual(30f, report.Rooms[1].entered_s, 0.01f);
        }

        [Test]
        public void ASkippedObservationStillClosesEveryEarlierRoom()
        {
            RunReport report = NewReport();
            report.Entered(0, 3, "Bat Room", 0f);
            report.Entered(2, 5, "Chasm", 100f);

            Assert.IsTrue(report.Rooms[0].cleared);
            Assert.AreEqual(3, report.Run.rooms_reached);
        }

        [Test]
        public void TheExitRoomClosesTheLastRoomWithoutARowOfItsOwn()
        {
            RunReport report = NewReport();
            report.Entered(4, 7, "Last", 300f);
            report.Entered(5, -1, "Exit", 330f);

            Assert.AreEqual(1, report.Rooms.Count);
            Assert.IsTrue(report.Rooms[0].cleared);
            Assert.AreEqual(30f, report.Rooms[0].seconds, 0.01f);
            Assert.AreEqual(6, report.Run.rooms_reached, "rooms_total + 1 means the Exit room was reached.");
        }

        [Test]
        public void FinishTimesTheRoomStillBeingPlayedAndLocksTheReport()
        {
            RunReport report = NewReport();
            report.Entered(0, 3, "Bat Room", 10f);
            report.Finish("TimedOut", 1, 3, 480f);
            report.Entered(1, 4, "Circuit Room", 490f);
            report.Voted(490f, 3, "mage");

            Assert.IsTrue(report.Closed);
            Assert.AreEqual("TimedOut", report.Run.outcome);
            Assert.AreEqual("result", report.Run.ended);
            Assert.AreEqual(3, report.Run.players_end);
            Assert.AreEqual(470f, report.Rooms[0].seconds, 0.01f);
            Assert.IsFalse(report.Rooms[0].cleared);
            Assert.AreEqual(1, report.Rooms.Count);
            Assert.AreEqual(0, report.Votes.Count);
        }

        [Test]
        public void AVoteThatCatchesTheMageIsCounted()
        {
            RunReport report = NewReport();
            report.Voted(90f, 4, "weapon");
            report.Voted(200f, 3, "mage");

            Assert.AreEqual(2, report.Run.votes_called);
            Assert.IsTrue(report.Run.mage_banished);
            Assert.AreEqual(2, report.Votes[1].n);
        }

        [Test]
        public void AnOpenReportSerializesAsAbandonedWithoutClosing()
        {
            RunReport report = NewReport();
            report.Entered(0, 3, "Bat Room", 10f);
            string json = report.ToJson(25f);

            Assert.IsFalse(report.Closed);
            Assert.AreEqual(0f, report.Rooms[0].seconds, 0.01f, "Serializing must not change the live report.");
            StringAssert.Contains("\"v\":1", json);
            StringAssert.Contains("\"ended\":\"abandoned\"", json);
            StringAssert.Contains("\"duration_s\":25.0", json);
            StringAssert.Contains("\"seconds\":15.0", json);
        }

        [Test]
        public void TimesAreWrittenAsTenthsNotFloatNoise()
        {
            RunReport report = NewReport();
            report.Abandon(3, 74.1f);
            StringAssert.Contains("\"duration_s\":74.1,", report.ToJson(0f), "A float's binary value (74.09999847) must not reach the Sheet.");
        }

        [Test]
        public void TheJsonCarriesTheSheetsColumnNames()
        {
            RunReport report = NewReport();
            report.Entered(0, 3, "Bat Room", 1f);
            report.Voted(60f, 4, "nobody");
            report.Finish("Escaped", 1, 4, 300f);
            string json = report.ToJson(0f);

            foreach (string column in new[] { "run_id", "started_at", "build", "players_start", "players_end", "mages",
                "outcome", "ended", "duration_s", "timer_s", "rooms_total", "rooms_reached", "votes_called",
                "mage_banished", "index", "room_id", "room", "entered_s", "seconds", "cleared", "n", "at_s",
                "players", "banished" })
            {
                StringAssert.Contains("\"" + column + "\":", json);
            }
            Assert.DoesNotThrow(() => JsonUtility.FromJson<RunReport.RunRow>(json));
        }
    }
}
