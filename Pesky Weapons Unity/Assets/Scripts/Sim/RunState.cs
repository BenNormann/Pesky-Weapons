using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>
    /// The run half of the shared world: the sequence of room ids this run strings together, and the timer.
    /// All of it is public knowledge and rides the snapshot (SnapshotPartKind.Run). Nothing secret lives
    /// here: who the Mage is stays in the host's LabyrinthRule, and a curse is applied only by its victim.
    ///
    /// Doors resolve from the sequence at traversal time (RunDirector): room k's exit leads to room k + 1's
    /// entry and back; the start room's exit leads to room 0; the last room's exit leads to the Exit room.
    /// The outcome of the round is still LabyrinthState.Outcome / MageMask, filled by ROUND_RESULT.
    /// </summary>
    public sealed class RunState
    {
        /// <summary>The largest run RUN_LAYOUT can carry (its count is a u8) and the snapshot part allows.</summary>
        public const int MaxRooms = 32;

        readonly ushort[] _roomIds = new ushort[MaxRooms];
        int _count;

        /// <summary>How many rooms the run has; 0 until RUN_LAYOUT arrives.</summary>
        public int Count { get { return _count; } }

        /// <summary>True once the first player left the start room: the timer is running.</summary>
        public bool Started { get; private set; }

        /// <summary>The sim tick the timer started at.</summary>
        public uint StartTick { get; private set; }

        /// <summary>The sim tick the Mage wins at, if the crew is not in the Exit room by then.</summary>
        public uint DeadlineTick { get; private set; }

        /// <summary>Bumped whenever the sequence or the timer changed, so a view can notice cheaply.</summary>
        public uint Rev { get; private set; }

        /// <summary>The room id at position k of the run, or -1 outside the sequence.</summary>
        public int RoomAt(int index)
        {
            return index >= 0 && index < _count ? _roomIds[index] : -1;
        }

        /// <summary>Where a room id sits in the run, or -1 when it is not part of it.</summary>
        public int IndexOf(int roomId)
        {
            for (int i = 0; i < _count; i++) if (_roomIds[i] == roomId) return i;
            return -1;
        }

        /// <summary>Whole seconds left on the timer at a tick; 0 when it has run out; the full timer before it starts is not known here (see RunDef).</summary>
        public long TicksLeft(uint tick)
        {
            if (!Started) return 0;
            return tick >= DeadlineTick ? 0 : (long)(DeadlineTick - tick);
        }

        public void ResetRound()
        {
            _count = 0;
            Started = false;
            StartTick = 0;
            DeadlineTick = 0;
            Rev++;
        }

        public void Apply(in RunLayoutMsg msg)
        {
            _count = 0;
            int n = msg.roomIds != null ? msg.roomIds.Length : 0;
            if (n > MaxRooms) n = MaxRooms;
            for (int i = 0; i < n; i++) _roomIds[i] = msg.roomIds[i];
            _count = n;
            // A new layout is a new round: the timer starts again from nothing.
            Started = false;
            StartTick = 0;
            DeadlineTick = 0;
            Rev++;
        }

        public void Apply(in RunStartMsg msg)
        {
            Started = true;
            StartTick = msg.startTick;
            DeadlineTick = msg.deadlineTick;
            Rev++;
        }

        /// <summary>The sequence as a layout message, for the snapshot and for the host's broadcast.</summary>
        public RunLayoutMsg ToLayout()
        {
            RunLayoutMsg msg = new RunLayoutMsg();
            msg.roomIds = new ushort[_count];
            for (int i = 0; i < _count; i++) msg.roomIds[i] = _roomIds[i];
            return msg;
        }

        // ---------------------------------------------------------------- snapshot

        public void Write(NetWriter w)
        {
            w.U8((byte)_count);
            for (int i = 0; i < _count; i++) w.U16(_roomIds[i]);
            w.Bool(Started).U32(StartTick).U32(DeadlineTick);
        }

        public void Read(NetReader r)
        {
            int n = r.U8();
            if (n > MaxRooms) n = MaxRooms;
            for (int i = 0; i < n; i++) _roomIds[i] = r.U16();
            _count = r.Failed ? 0 : n;
            Started = r.Bool();
            StartTick = r.U32();
            DeadlineTick = r.U32();
            Rev++;
        }
    }
}
