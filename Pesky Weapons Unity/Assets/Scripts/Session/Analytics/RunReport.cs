using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Session.Analytics
{
    /// <summary>
    /// One run's analytics report (docs/ANALYTICS.md): the runs row, a rooms row per room anybody entered,
    /// a votes row per meeting. Built up by RunAnalyticsWatcher while the run plays and serialized with
    /// JsonUtility, so the field names here ARE the Sheet's column names (tools/analytics/Code.gs). Times
    /// are seconds of GAME time since the run's timer started.
    /// </summary>
    public sealed class RunReport
    {
        public const int SchemaVersion = 1;

        [Serializable]
        public sealed class RunRow
        {
            public string run_id;
            public string started_at;
            public string build;
            public int players_start;
            public int players_end;
            public int mages;
            public string outcome = "None";
            public string ended = "abandoned";
            public float duration_s;
            public float timer_s;
            public int rooms_total;
            public int rooms_reached;
            public int votes_called;
            public bool mage_banished;
        }

        [Serializable]
        public sealed class RoomRow
        {
            public int index;
            public int room_id;
            public string room;
            public float entered_s;
            public float seconds;
            public bool cleared;
        }

        [Serializable]
        public sealed class VoteRow
        {
            public int n;
            public float at_s;
            public int players;
            public string banished;
        }

        [Serializable]
        sealed class Payload
        {
            public int v = SchemaVersion;
            public RunRow run;
            public List<RoomRow> rooms;
            public List<VoteRow> votes;
        }

        readonly RunRow _run = new RunRow();
        readonly List<RoomRow> _rooms = new List<RoomRow>(8);
        readonly List<VoteRow> _votes = new List<VoteRow>(4);

        public RunRow Run { get { return _run; } }
        public IReadOnlyList<RoomRow> Rooms { get { return _rooms; } }
        public IReadOnlyList<VoteRow> Votes { get { return _votes; } }

        /// <summary>True once Finish or Abandon closed it; nothing changes it after that.</summary>
        public bool Closed { get; private set; }

        public RunReport(string runId, string startedAtUtc, string build, int players, int roomsTotal, float timerSeconds)
        {
            _run.run_id = runId;
            _run.started_at = startedAtUtc;
            _run.build = build;
            _run.players_start = players;
            _run.players_end = players;
            _run.rooms_total = roomsTotal;
            _run.timer_s = Round(timerSeconds);
        }

        /// <summary>
        /// Somebody stands in position <paramref name="index"/> of the run at <paramref name="seconds"/>
        /// (0-based; rooms_total is the Exit room). Only a FIRST entry counts: it opens that room's row and
        /// clears every earlier room still open, timed up to now. Going back changes nothing.
        /// </summary>
        public void Entered(int index, int roomId, string label, float seconds)
        {
            if (Closed || index < 0 || index > _run.rooms_total) return;
            if (index + 1 <= _run.rooms_reached) return;
            for (int i = 0; i < _rooms.Count; i++)
            {
                RoomRow open = _rooms[i];
                if (open.cleared || open.index - 1 >= index) continue;
                open.cleared = true;
                open.seconds = Round(seconds - open.entered_s);
            }
            _run.rooms_reached = index + 1;
            if (index == _run.rooms_total) return; // the Exit room has no row of its own
            RoomRow row = new RoomRow();
            row.index = index + 1;
            row.room_id = roomId;
            row.room = label;
            row.entered_s = Round(seconds);
            _rooms.Add(row);
        }

        /// <summary>A meeting ended at <paramref name="seconds"/>. banished: "mage", "weapon" or "nobody".</summary>
        public void Voted(float seconds, int players, string banished)
        {
            if (Closed) return;
            VoteRow row = new VoteRow();
            row.n = _votes.Count + 1;
            row.at_s = Round(seconds);
            row.players = players;
            row.banished = banished;
            _votes.Add(row);
            _run.votes_called = _votes.Count;
            if (banished == "mage") _run.mage_banished = true;
        }

        /// <summary>The round ended normally (ROUND_RESULT).</summary>
        public void Finish(string outcome, int mages, int playersEnd, float seconds)
        {
            if (Closed) return;
            _run.outcome = outcome;
            _run.ended = "result";
            _run.mages = mages;
            Close(playersEnd, seconds);
        }

        /// <summary>The host left or closed the page mid-run: what is known so far, outcome None.</summary>
        public void Abandon(int playersEnd, float seconds)
        {
            if (Closed) return;
            Close(playersEnd, seconds);
        }

        void Close(int playersEnd, float seconds)
        {
            _run.players_end = playersEnd;
            _run.duration_s = Round(seconds);
            CloseOpenRooms(seconds);
            Closed = true;
        }

        void CloseOpenRooms(float seconds)
        {
            for (int i = 0; i < _rooms.Count; i++)
                if (!_rooms[i].cleared) _rooms[i].seconds = Round(seconds - _rooms[i].entered_s);
        }

        /// <summary>
        /// The report as the Sheet takes it. An open report is written as if abandoned right now, with
        /// the room still being played timed up to <paramref name="seconds"/>, so the page can hold it for
        /// a tab that closes mid-run; the report itself is left open.
        /// </summary>
        public string ToJson(float seconds)
        {
            if (Closed) return Serialize(_run, _rooms);
            RunRow run = JsonUtility.FromJson<RunRow>(JsonUtility.ToJson(_run));
            run.duration_s = Round(seconds);
            List<RoomRow> rooms = new List<RoomRow>(_rooms.Count);
            for (int i = 0; i < _rooms.Count; i++)
            {
                RoomRow r = JsonUtility.FromJson<RoomRow>(JsonUtility.ToJson(_rooms[i]));
                if (!r.cleared) r.seconds = Round(seconds - r.entered_s);
                rooms.Add(r);
            }
            return Serialize(run, rooms);
        }

        string Serialize(RunRow run, List<RoomRow> rooms)
        {
            Payload wire = new Payload();
            wire.run = run;
            wire.rooms = rooms;
            wire.votes = _votes;
            return JsonUtility.ToJson(wire);
        }

        /// <summary>A tenth of a second is plenty, and keeps the Sheet readable.</summary>
        static float Round(float seconds)
        {
            return Mathf.Round(Mathf.Max(0f, seconds) * 10f) / 10f;
        }
    }
}
