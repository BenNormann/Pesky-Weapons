using System.Collections.Generic;

namespace Pesky.Session
{
    /// <summary>
    /// The small FIFO of host events waiting for their tick. Events arrive
    /// in send order from one host with non-decreasing ticks, so a queue
    /// keeps them ordered; the front is applied once the sim reaches its tick.
    /// </summary>
    public sealed class PendingEvents
    {
        struct Item
        {
            public uint tick;
            public byte[] payload;
        }

        readonly Queue<Item> _queue = new Queue<Item>(64);

        public int Count => _queue.Count;

        public void Push(uint tick, byte[] payload)
        {
            var item = new Item();
            item.tick = tick;
            item.payload = payload;
            _queue.Enqueue(item);
        }

        public bool TryPeekTick(out uint tick)
        {
            if (_queue.Count == 0)
            {
                tick = 0;
                return false;
            }
            tick = _queue.Peek().tick;
            return true;
        }

        public byte[] Pop() => _queue.Dequeue().payload;

        /// <summary>Discards every event stamped at or before the tick (a snapshot already holds their effect).</summary>
        public void DropUpTo(uint tick)
        {
            while (_queue.Count > 0 && _queue.Peek().tick <= tick) _queue.Dequeue();
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}
