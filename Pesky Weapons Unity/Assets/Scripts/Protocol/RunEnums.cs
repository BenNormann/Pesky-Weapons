namespace Pesky.Protocol
{
    /// <summary>
    /// The Mage's five curses, keys 1-5. The value rides CURSE_REQ and CURSE_EVENT. Stable: append only.
    /// </summary>
    public enum CurseKind : byte
    {
        None = 0,
        /// <summary>Launches bend toward the nearest other weapon.</summary>
        Magnetic = 1,
        /// <summary>The view sways and the launch heading drifts.</summary>
        Nausea = 2,
        /// <summary>The weapon wears a zero-friction material and cannot settle.</summary>
        Slippery = 3,
        /// <summary>The screen darkens to a small clear circle in the middle.</summary>
        Blindness = 4,
        /// <summary>Launch speed halved.</summary>
        Heavy = 5,
    }

    /// <summary>Why the host said no to a CURSE_REQ. Sent back to the asker alone. Stable: append only.</summary>
    public enum CurseRefusal : byte
    {
        /// <summary>Not another player holding a weapon (or the tutorial dummy): a soul, an empty slot, yourself.</summary>
        NoTarget = 0,
        OutOfRange = 1,
        Cooldown = 2,
        /// <summary>The curse id is not one of the five.</summary>
        NoSuchCurse = 3,
    }

    /// <summary>
    /// Where a player stands in the run, as the host's scene answers it (IHostWorld.CollectRunPlaces):
    /// the start room, one of the run's rooms (which is what starts the timer), or the Exit room's volume.
    /// </summary>
    public enum RunPlace : byte { Unknown = 0, Start = 1, Rooms = 2, Exit = 3 }
}
