namespace Pesky.Protocol
{
    /// <summary>
    /// The four doorways of every room, and the four directions of the grid. North is -Z of the map (row 0
    /// is the north edge), east is +X (column 0 is the west edge). The value is the wire value: a doorway
    /// byte and the exit direction ride LAB_LAYOUT. Opposite is (d + 2) and 3.
    /// </summary>
    public enum Heading : byte { North = 0, East = 1, South = 2, West = 3 }

    /// <summary>
    /// A player's secret role. Only the host knows the whole table; each Mage learns its own from a
    /// ROLE_ASSIGN addressed to it alone. Everyone else is a Weapon and is never told anything.
    /// </summary>
    public enum LabyrinthRole : byte { Weapon = 0, Mage = 1 }

    /// <summary>
    /// What a compass points at. GoodEnd is the truth and the default; the other two are what a Mage bent
    /// it to. COMPASS_TARGETS carries this to the affected player alone and never says who set it.
    /// </summary>
    public enum CompassTargetKind : byte { GoodEnd = 0, BadEnd = 1, Cell = 2 }

    /// <summary>How a round ended. Stable: append only.</summary>
    public enum RoundOutcome : byte
    {
        None = 0,
        /// <summary>Enough of the crew gathered at the exit doorway: the weapons win.</summary>
        Escaped = 1,
        /// <summary>A majority of the players stood in the Resurrection Room at once: the Mage wins.</summary>
        Resurrected = 2,
    }
}
