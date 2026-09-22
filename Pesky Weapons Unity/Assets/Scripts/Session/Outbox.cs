using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Transport;

namespace Pesky.Session
{
    /// <summary>
    /// Per-peer lists of payloads due to go out. Flush packs, for each peer,
    /// the oldest payloads that fit one FRAME (at most Frame.MaxBytes) and
    /// sends it; whatever does not fit waits for the next flush, oldest
    /// first, so order per peer is never disturbed.
    /// </summary>
    public sealed class Outbox
    {
        sealed class Lane
        {
            public readonly string peerId;
            public readonly List<byte[]> queue = new List<byte[]>(32);

            public Lane(string peerId)
            {
                this.peerId = peerId;
            }
        }

        readonly Dictionary<string, Lane> _lanes = new Dictionary<string, Lane>(Wire.MaxPlayers);
        readonly List<Lane> _order = new List<Lane>(Wire.MaxPlayers);
        readonly List<byte[]> _packing = new List<byte[]>(Frame.MaxCount);

        public int LaneCount => _order.Count;

        public int Pending(string peerId) =>
            peerId != null && _lanes.TryGetValue(peerId, out var lane) ? lane.queue.Count : 0;

        public void Enqueue(string peerId, byte[] payload)
        {
            if (string.IsNullOrEmpty(peerId) || payload == null || payload.Length == 0) return;
            if (!_lanes.TryGetValue(peerId, out var lane))
            {
                lane = new Lane(peerId);
                _lanes[peerId] = lane;
                _order.Add(lane);
            }
            lane.queue.Add(payload);
        }

        public void EnqueueAll(IReadOnlyList<string> peerIds, byte[] payload)
        {
            if (peerIds == null) return;
            for (var i = 0; i < peerIds.Count; i++) Enqueue(peerIds[i], payload);
        }

        /// <summary>Drops the lane and everything queued for the peer.</summary>
        public void Remove(string peerId)
        {
            if (peerId == null || !_lanes.TryGetValue(peerId, out var lane)) return;
            _lanes.Remove(peerId);
            _order.Remove(lane);
        }

        public void Clear()
        {
            _lanes.Clear();
            _order.Clear();
        }

        /// <summary>One FRAME per peer with something queued; the rest spills to the next flush.</summary>
        public void Flush(INetTransport transport)
        {
            if (transport == null) return;
            for (var i = 0; i < _order.Count; i++)
            {
                var lane = _order[i];
                if (lane.queue.Count == 0) continue;
                _packing.Clear();
                if (TakeFrame(lane.queue, _packing) == 0) continue;
                var frame = Frame.Pack(_packing);
                NetStats.CountOut(frame.Length, 1);
                transport.SendTo(lane.peerId, frame);
            }
            _packing.Clear();
        }

        /// <summary>
        /// Moves the oldest payloads that fit one FRAME from the queue into
        /// <paramref name="into"/> and returns how many moved. A single payload
        /// too large for any frame is dropped so the lane never jams.
        /// </summary>
        public static int TakeFrame(List<byte[]> queue, List<byte[]> into)
        {
            var total = Frame.HeaderBytes;
            var count = 0;
            while (count < queue.Count && count < Frame.MaxCount)
            {
                var body = queue[count];
                var size = Frame.PerMessageBytes + body.Length;
                if (total + size > Frame.MaxBytes) break;
                total += size;
                count++;
            }
            if (count == 0)
            {
                if (queue.Count > 0) queue.RemoveAt(0);
                return 0;
            }
            for (var i = 0; i < count; i++) into.Add(queue[i]);
            queue.RemoveRange(0, count);
            return count;
        }
    }
}
