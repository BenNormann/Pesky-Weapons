using System.Collections.Generic;

namespace Pesky.Game
{
    /// <summary>
    /// What the narrator says when a crew has been in a room too long (round 29c). Keyed by the room's PuzzleRoomProfile
    /// id, with three keys of its own for rooms that have no profile. Line 1 is the nudge at one minute, line 2 the
    /// detailed hint at two. The narrator is sarcastic and a little mean (owner's brief); the facts match
    /// docs/HINTS.md. Edit the text here; nothing else needs to change.
    /// </summary>
    public static class NarratorLines
    {
        public const string StartRoom = "start";
        public const string GateHall = "gate_hall";
        public const string PoolRoom = "pool";

        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { StartRoom, new[] {
                "Still admiring the racks? Fly INTO a weapon. Any weapon. Even the banana would be an improvement.",
                "Possess a weapon by flying into it. Then LOOK where you want to go and press SPACE. There are doors. The goblins did not build this room for you to live in." } },
            { "gatehouse", new[] {
                "The gate is up only while something HEAVY sits on the plate. Yes. That means somebody stays behind. It is called friendship.",
                "A Mace or a Hammer parks on the plate behind the crates; its wire lights up to the junction and the gate rises. Somebody ELSE walks under it, up the stair on the BACK of the gate wall into the winch room, and hits the lever: the gate stays up for good. Then two guards, and a ten-kilo plate hiding behind the crates in the far left corner. Take your time. I have nowhere to be." } },
            { "well_room", new[] {
                "It is a lift. It hangs on exactly one rope. I am not going to spell out what a blade does to a rope.",
                "Sword or Dagger: throw yourself point-first at a wooden post and you stick. Look AT the post and launch: you hop up it and stick again. From up top, throw yourself at the rope, any part of it. The lift falls into a ramp; the heavy one rides it up to the plate on the far balcony. Eight kilos. A Dagger is one kilo. Do the maths." } },
            { "porter_room", new[] {
                "The exit opens when the hall is clear, and the grate opens only from the inside. Somebody in there is terribly helpful and terribly stupid.",
                "Kill the two. The Dagger lies STILL right against the bars. Anyone bangs the grate. The porter comes over, pulls the Dagger through and carries it to its stand, because it is a goblin. Inside, the Dagger hits the lever on the back of the wall and the grate rises. You are welcome." } },
            { "circuit_room", new[] {
                "Four wires leave the generator lit. One leaves dead. I wonder, I really do, which one is your problem.",
                "Follow the dead wire: into the pit, then up to the gallery. It is broken in two places. A Sword-length piece of METAL lying still across each break closes it, both at the same time; the breaks even pull you into place, that is how little they think of you. Then the exit comes alive." } },
            { "bat_room", new[] {
                "Nobody jumps that far. A braced friend, batted by a heavy, goes a great deal further. Shift to brace. Think about it.",
                "The Dagger stands braced on the lip, holding Shift. The Hammer or the Mace launches straight INTO it, aimed at the far bank. The Dagger lies still among the goblins, creeps to the short break beside the bridge's end and lies across it. Bridge. Fell in? The magnet lifts metal out; wood takes the stair." } },
            { GateHall, new[] {
                "One doorway is lit. That is the way out. All of you, in the ring in front of it, at the same time. A group activity.",
                "The exit counts everyone still standing inside the ring at once. If one of you is off wandering, the rest of you are just waiting. There is a vote button for that." } },
            { PoolRoom, new[] {
                "A room with two doors. The far one is the way on. I believe in you. Barely.",
                "The exit is the other door. Launch through the purple. That is the whole room." } },
        };

        /// <summary>Line 1 or 2 for a room key; a key nobody wrote lines for gets the plain room's.</summary>
        public static string Hint(string key, int which)
        {
            string[] lines;
            if (key == null || !Lines.TryGetValue(key, out lines)) lines = Lines[PoolRoom];
            return lines[which <= 1 ? 0 : 1];
        }
    }
}
