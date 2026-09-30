using UnityEngine;

namespace Pesky.Data
{
    /// <summary>
    /// Everything about the simplified RUN that is data rather than code: how many rooms a run strings
    /// together, the timer, and the Mage's five curses with their numbers. One asset
    /// (Assets/Data/Run.asset), referenced from GameData.run, so the host's rule and every peer's effects
    /// read the same numbers from the same build.
    ///
    /// The room POOL is not here: it is the scene (RunDirector.pool), and the host asks its own scene for
    /// it when the round opens. Room ids are still indices into LabyrinthDef.rooms, which remains the list
    /// of authored rooms. See docs/RUN.md.
    /// </summary>
    [CreateAssetMenu(fileName = "Run", menuName = "Pesky/Run")]
    public sealed class RunDef : ScriptableObject
    {
        [Header("The run")]
        [Tooltip("How many pool rooms a run strings together between the start room and the Exit room. Fewer if the pool is smaller.")]
        [Min(1)] public int roomsPerRun = 5;

        [Tooltip("Seconds on the timer once the first player leaves the start room. The Mage wins the instant it reaches zero.")]
        [Min(10f)] public float timerSeconds = 300f;

        [Tooltip("The HUD's timer turns red with this many seconds left.")]
        [Min(0f)] public float warningSeconds = 30f;

        [Header("Curses (keys 1-5, Mage only)")]
        [Tooltip("Metres from the Mage's own body to the target, checked by the host on the streamed positions.")]
        [Min(0f)] public float curseRange = 15f;

        [Tooltip("Seconds between one Mage's curses. ONE cooldown shared by all five keys.")]
        [Min(0f)] public float curseCooldown = 30f;

        [Tooltip("Seconds a curse lasts on its victim. A new curse on the same victim replaces the old one.")]
        [Min(0f)] public float curseDuration = 20f;

        [Tooltip("Half-angle of the cone around the centre of the Mage's screen in which a body is picked as the target, degrees. Client side only.")]
        [Range(1f, 45f)] public float curseAimCone = 8f;

        [Header("1 Magnetic")]
        [Tooltip("The nearest OTHER weapon within this many metres pulls the victim's launch direction.")]
        [Min(0f)] public float magneticRange = 20f;

        [Tooltip("How far the launch turns toward that weapon, as a fraction of the angle between them. 0.5 = halfway.")]
        [Range(0f, 1f)] public float magneticBend = 0.5f;

        [Header("2 Nausea")]
        [Tooltip("Camera roll sway, degrees, peak.")]
        [Min(0f)] public float nauseaRoll = 6f;

        [Tooltip("Camera yaw sway, degrees, peak.")]
        [Min(0f)] public float nauseaYaw = 4f;

        [Tooltip("Seconds per full sway cycle.")]
        [Min(0.1f)] public float nauseaPeriod = 3f;

        [Tooltip("The launch heading wanders by up to this many degrees, on a slower second sine.")]
        [Min(0f)] public float nauseaHeadingDrift = 12f;

        [Header("3 Slippery")]
        [Tooltip("The physics material the victim's weapon colliders wear while slippery. P_Slick: zero friction, so it slides and cannot settle.")]
        public PhysicsMaterial slipperyMaterial;

        [Header("4 Blindness")]
        [Tooltip("Radius of the clear circle around the centre of the screen, as a fraction of the screen height.")]
        [Range(0.02f, 0.5f)] public float blindnessClearRadius = 0.12f;

        [Tooltip("How dark the rest of the screen goes, 0-1.")]
        [Range(0f, 1f)] public float blindnessOpacity = 0.96f;

        [Header("5 Heavy")]
        [Tooltip("The launch speed is multiplied by this while heavy.")]
        [Range(0.05f, 1f)] public float heavySpeedScale = 0.5f;

        /// <summary>The five curses' names, by CurseKind value (1..5). Empty for anything else.</summary>
        public static string CurseName(int curse)
        {
            switch (curse)
            {
                case 1: return "MAGNETIC";
                case 2: return "NAUSEA";
                case 3: return "SLIPPERY";
                case 4: return "BLINDNESS";
                case 5: return "HEAVY";
                default: return "";
            }
        }
    }
}
