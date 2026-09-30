namespace Pesky.Game
{
    /// <summary>
    /// The scene names the flow between scenes is written in. They are defaults for serialized
    /// fields, not lookups: a component still shows its scene in the Inspector, and nothing here
    /// loads a scene by itself. Every load is guarded, because Labyrinth does not exist yet.
    /// </summary>
    public static class SceneNames
    {
        /// <summary>Build index 0: nothing but the bootstrap that loads the menu.</summary>
        public const string Boot = "Boot";

        /// <summary>Build index 1: the title and the room page.</summary>
        public const string MainMenu = "MainMenu";

        /// <summary>The labyrinth grid scene (docs/LABYRINTH.md). Set aside: it exists but is out of the build.</summary>
        public const string Labyrinth = "Labyrinth";

        /// <summary>Build index 3: the simplified run (docs/RUN.md), which START loads.</summary>
        public const string Run = "Run";

        /// <summary>The tutorial level. Zone1 until the tutorial stage builds its own.</summary>
        public const string Tutorial = "Zone1";
    }
}
