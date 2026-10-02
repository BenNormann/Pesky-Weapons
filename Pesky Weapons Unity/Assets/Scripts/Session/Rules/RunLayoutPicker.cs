using System.Collections.Generic;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// Pure run-layout selection. Guaranteed rooms are included first, in their configured order, when
    /// present in the scene pool. Random rooms fill only the remaining slots. This makes a fully-authored
    /// run continuous and repeatable while still supporting random filler rooms in a larger future pool.
    /// Kept separate from RunRule so room-selection policy is easy to test.
    /// </summary>
    public static class RunLayoutPicker
    {
        public static ushort[] Pick(IList<ushort> pool, int[] guaranteedRoomIds, int requestedCount, uint seed)
        {
            if (pool == null || pool.Count == 0 || requestedCount <= 0) return new ushort[0];

            List<ushort> candidates = new List<ushort>(pool.Count);
            HashSet<ushort> available = new HashSet<ushort>();
            for (int i = 0; i < pool.Count; i++)
            {
                ushort id = pool[i];
                if (available.Add(id)) candidates.Add(id);
            }

            int want = requestedCount;
            if (want > RunState.MaxRooms) want = RunState.MaxRooms;
            if (want > candidates.Count) want = candidates.Count;
            if (want < 1) return new ushort[0];

            Rng rng = new Rng(seed);
            List<ushort> picked = new List<ushort>(want);
            HashSet<ushort> chosen = new HashSet<ushort>();

            if (guaranteedRoomIds != null)
            {
                for (int i = 0; i < guaranteedRoomIds.Length && picked.Count < want; i++)
                {
                    int raw = guaranteedRoomIds[i];
                    if (raw < 0 || raw > ushort.MaxValue) continue;
                    ushort id = (ushort)raw;
                    if (available.Contains(id) && chosen.Add(id)) picked.Add(id);
                }
            }

            // Shuffle the complete candidate list before filling. This also avoids bias when the scene
            // registry happens to be ordered by room id.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                ushort swap = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swap;
            }
            for (int i = 0; i < candidates.Count && picked.Count < want; i++)
                if (chosen.Add(candidates[i])) picked.Add(candidates[i]);

            return picked.ToArray();
        }
    }
}
