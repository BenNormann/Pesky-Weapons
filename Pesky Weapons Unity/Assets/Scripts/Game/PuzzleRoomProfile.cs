using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Authored matchmaking/difficulty metadata for a puzzle room. RunDirector does not filter on it yet;
    /// it deliberately lives on the room now so future party-size and difficulty selection can be added
    /// without reverse-engineering each scene. The current Resonance Forge is tuned around four players.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleRoomProfile : MonoBehaviour
    {
        [SerializeField] string puzzleId = "puzzle";
        [SerializeField] string displayName = "Puzzle Room";
        [TextArea(2, 5)] [SerializeField] string summary;

        [Header("Party size")]
        [Min(1)] [SerializeField] int minimumPlayers = 3;
        [Min(1)] [SerializeField] int optimalPlayers = 4;
        [Min(1)] [SerializeField] int maximumPlayers = 8;

        [Header("Pacing")]
        [Min(5f)] [SerializeField] float expectedSolveSeconds = 55f;
        [Range(1, 5)] [SerializeField] int difficultyTier = 2;

        public string PuzzleId { get { return puzzleId; } }
        public string DisplayName { get { return displayName; } }
        public string Summary { get { return summary; } }
        public int MinimumPlayers { get { return minimumPlayers; } }
        public int OptimalPlayers { get { return optimalPlayers; } }
        public int MaximumPlayers { get { return maximumPlayers; } }
        public float ExpectedSolveSeconds { get { return expectedSolveSeconds; } }
        public int DifficultyTier { get { return difficultyTier; } }

        public bool Supports(int playerCount)
        {
            return playerCount >= minimumPlayers && playerCount <= maximumPlayers;
        }

        void OnValidate()
        {
            minimumPlayers = Mathf.Max(1, minimumPlayers);
            optimalPlayers = Mathf.Max(minimumPlayers, optimalPlayers);
            maximumPlayers = Mathf.Max(optimalPlayers, maximumPlayers);
        }
    }
}
