using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One colour per player slot, so a crew member is the same chip on every screen. Eight of
    /// them, because Wire.MaxPlayers is eight; the index wraps rather than throwing.
    /// </summary>
    public static class SlotColors
    {
        static readonly Color[] Palette =
        {
            new Color(0.93f, 0.76f, 0.31f), // amber   - the host's slot 0
            new Color(0.40f, 0.72f, 0.94f), // sky
            new Color(0.47f, 0.80f, 0.49f), // green
            new Color(0.89f, 0.45f, 0.40f), // red
            new Color(0.71f, 0.55f, 0.90f), // violet
            new Color(0.95f, 0.60f, 0.30f), // orange
            new Color(0.40f, 0.85f, 0.82f), // teal
            new Color(0.88f, 0.55f, 0.75f)  // pink
        };

        public static int Count { get { return Palette.Length; } }

        public static Color For(int slot)
        {
            int index = slot % Palette.Length;
            if (index < 0) index += Palette.Length;
            return Palette[index];
        }
    }
}
