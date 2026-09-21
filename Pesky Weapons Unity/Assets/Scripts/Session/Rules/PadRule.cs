using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// The host's scratch-pad brain: it wipes the pad at the start of every round, and it is the validator
    /// for PAD_STROKE_REQ. A stroke a player sends is checked for size and rate, given the next sequence
    /// number, and broadcast as a PAD_STROKE that every peer paints in that order.
    ///
    /// NOTHING HERE IS SECRET. The pad is the one shared surface in the game that everybody can see and
    /// write on, Mage included; drawing a lie on it is a legitimate move. A refused stroke is dropped in
    /// silence like every other refusal in this project, so a flood tells the sender nothing.
    /// </summary>
    public sealed class PadRule : IHostRule, IIntentValidator
    {
        readonly float[] _tokens = new float[Wire.MaxPlayers];

        bool _roundOpen;
        bool _armed;
        uint _lastTick;
        ushort _seq;

        // ---------------------------------------------------------------- the rule

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null) return;
            LabyrinthDef def = sim.Data != null ? sim.Data.labyrinth : null;

            RefillBuckets(def, tick);

            bool playing = sim.Phase == SessionPhase.Playing;
            if (playing && !_roundOpen)
            {
                // A new round starts on a blank pad: whatever the last crew drew is not this crew's map.
                _roundOpen = true;
                _seq = 0;
                Wipe(events);
            }
            else if (!playing && _roundOpen)
            {
                _roundOpen = false;
            }
        }

        void RefillBuckets(LabyrinthDef def, uint tick)
        {
            float startRate = def != null ? def.padStrokeBurst : 40;
            if (startRate < 1f) startRate = 1f;
            if (!_armed)
            {
                // Start full, so the very first line a player draws is never the one the host throws away.
                _armed = true;
                _lastTick = tick;
                for (int i = 0; i < _tokens.Length; i++) _tokens[i] = startRate;
                return;
            }
            if (tick <= _lastTick) return;
            float seconds = (tick - _lastTick) * (Protocol.Tick.Ms / 1000f);
            _lastTick = tick;

            float rate = def != null ? def.padStrokesPerSecond : 20f;
            if (rate < 1f) rate = 1f;
            float burst = def != null ? def.padStrokeBurst : 40;
            if (burst < 1f) burst = 1f;
            for (int i = 0; i < _tokens.Length; i++)
            {
                _tokens[i] += rate * seconds;
                if (_tokens[i] > burst) _tokens[i] = burst;
            }
        }

        void Wipe(EventSink events)
        {
            PadClearMsg msg = new PadClearMsg();
            msg.header = events.Header();
            events.Emit(msg.Encode());
        }

        // ---------------------------------------------------------------- the intent

        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (sim == null || events == null || fromSlot >= Wire.MaxPlayers) return;
            PlayerState player = sim.Players[fromSlot];
            if (player == null || !player.present) return;

            PadStrokeReqMsg req;
            if (!PadStrokeReqMsg.TryDecode(payload, out req)) return;

            int count = req.PointCount;
            if (count < 1 || count > PadStrokeReqMsg.MaxPoints) return;
            if (req.width > PadStrokeReqMsg.MaxWidth) return;

            // The rate cap. A refused stroke simply never appears, on the sender's screen as well once its
            // own optimistic copy times out.
            if (_tokens[fromSlot] < 1f) return;
            _tokens[fromSlot] -= 1f;

            unchecked { _seq++; }
            PadStrokeMsg msg = new PadStrokeMsg();
            msg.header = events.Header();
            msg.seq = _seq;
            msg.slot = fromSlot;
            msg.flags = req.flags;
            msg.width = req.width;
            msg.points = req.points;
            events.Emit(msg.Encode());
        }

        /// <summary>The host's own CLEAR button, so Game does not have to build the message itself.</summary>
        public static byte[] ClearPayload(EventHeader header)
        {
            PadClearMsg msg = new PadClearMsg();
            msg.header = header;
            return msg.Encode();
        }
    }
}
