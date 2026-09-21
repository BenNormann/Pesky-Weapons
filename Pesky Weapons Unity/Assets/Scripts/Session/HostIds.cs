using Pesky.Protocol;

namespace Pesky.Session
{
    /// <summary>
    /// The host's id counters: enemies, pickups, doors and snapshots. Each
    /// u16 counter starts at 1 and skips 0 and Wire.NoId on wrap. Host-only
    /// state; it is neither hashed nor snapshotted.
    /// </summary>
    public sealed class HostIds
    {
        ushort _enemy;
        ushort _pickup;
        ushort _door;
        byte _snapshot;

        public ushort NextEnemy() => Next(ref _enemy);
        public ushort NextPickup() => Next(ref _pickup);
        public ushort NextDoor() => Next(ref _door);

        public byte NextSnapshot()
        {
            _snapshot++;
            return _snapshot;
        }

        static ushort Next(ref ushort counter)
        {
            counter++;
            if (counter == 0 || counter == Wire.NoId) counter = 1;
            return counter;
        }
    }
}
