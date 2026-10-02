using UnityEngine;

namespace Pesky.Data
{
    /// <summary>
    /// Which round the host runs and every peer's HUD expects. Labyrinth is the 5x5 grid of docs/LABYRINTH.md
    /// (set aside); Run is the five-rooms-and-a-timer dungeon of docs/RUN.md. Stable: append only.
    /// </summary>
    public enum GameMode : byte { Labyrinth = 0, Run = 1 }

    /// <summary>
    /// The one read-only data asset the sim is built from. NetSession takes it
    /// in Start and hands it to WorldSim, so every peer's sim reads the same
    /// numbers from the same build. Tables are indexed by the byte ids that
    /// travel on the wire: never reorder them, only append.
    /// </summary>
    [CreateAssetMenu(fileName = "GameData", menuName = "Pesky/Game Data")]
    public sealed class GameData : ScriptableObject
    {
        [Tooltip("Weapon kinds, indexed by the weapon type id on the wire. Append only.")]
        public WeaponDef[] weapons;

        [Tooltip("Enemy kinds, indexed by the enemy kind id on the wire. Append only.")]
        public EnemyDef[] enemies;

        [Tooltip("Curse and blessing modifiers, indexed by modifier id. Append only.")]
        public ModifierDef[] modifiers;

        [Tooltip("Shared launch, soul and possession numbers.")]
        public MovementTuning movement;
        [Tooltip("The labyrinth: grid size, room list, the Mage's limits and the round's thresholds.")]
        public LabyrinthDef labyrinth;

        [Tooltip("Which round this data plays: the set-aside labyrinth grid, or the simplified run. The host's rules and every HUD read it.")]
        public GameMode mode = GameMode.Run;

        [Tooltip("The run: rooms per run, the timer and the five curses. Used when mode is Run.")]
        public RunDef run;


        [Header("Impact damage (host side hit validation)")]
        [Tooltip("Relative speed at which impact damage starts.")]
        public float impactSpeedMin = 3f;
        [Tooltip("Relative speed at which impact damage is full.")]
        public float impactSpeedMax = 12f;
        [Tooltip("Per weapon, per enemy hit cooldown in seconds.")]
        public float hitCooldownSeconds = 0.35f;

        [Header("Friend ballistics (batting another player's weapon)")]
        [Tooltip("Relative speed below which a touch between two players is not a bat.")]
        public float batMinSpeed = 4f;
        [Tooltip("The largest velocity change one bat may give, m/s.")]
        public float batMaxSpeed = 18f;
        [Tooltip("Bounciness of a bat: 0 = the target just takes the share of the momentum, 1 = fully elastic.")]
        [Range(0f, 1f)] public float batRestitution = 0.5f;
        [Tooltip("A BRACED friend (standing up, Shift held) flies along the batter's aim at this speed times the batter's share of the two masses. 19: a Mace or Hammer sends a Dagger or Banana 14-17 m at 45 deg (the Bat Room's chasm is 14 m); a Sword sends a Dagger 11 m.")]
        public float batBraceSpeed = 19f;
        [Tooltip("The batter stops dead when it bats a braced friend (it gave its momentum away), so it does not follow them over the edge.")]
        public bool batterStops = true;

        
public WeaponDef Weapon(int id) => weapons != null && id >= 0 && id < weapons.Length ? weapons[id] : null;

        public EnemyDef Enemy(int id) => enemies != null && id >= 0 && id < enemies.Length ? enemies[id] : null;

        public ModifierDef Modifier(int id) => modifiers != null && id >= 0 && id < modifiers.Length ? modifiers[id] : null;
    }
}
