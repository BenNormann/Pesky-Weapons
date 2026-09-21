using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// What the compass reads, once a frame, for the HUD to draw. Direction only: there is never a
    /// distance, because the labyrinth has no geometry to measure across.
    ///
    /// THE GREEN SPIKE is everybody's compass. It points along the SHORTEST PATH through the doorways,
    /// wrapped edges included, and what it names is a DOORWAY OF THE ROOM YOU ARE IN. Normally the target
    /// is the Exit. For a player a Mage has bent it, it is the Resurrection Room or a room the Mage picked,
    /// and NOTHING here can tell the difference: the target arrived as a COMPASS_TARGETS addressed to this
    /// peer alone, with no author on it. A bent compass is still green and simply points the wrong way.
    ///
    /// THE RED SPIKE exists on a Mage's machine alone, and points at the Resurrection Room. A Mage's green
    /// spike is always the truth, because the host never bends a Mage.
    ///
    /// A spike SPINS while the player is standing in that spike's target room, and while it has no reading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompassModel : MonoBehaviour
    {
        [SerializeField] SessionRunner sessionRunner;
        [SerializeField] LabyrinthDirector labyrinth;

        [Tooltip("How fast a spike turns while it is spinning or lost, degrees per second.")]
        [SerializeField] float spinDegreesPerSecond = 220f;

        NetSession _session;
        float _spinAngle;
        float _redSpinAngle;

        // ---------------------------------------------------------------- the green spike

        /// <summary>True when the green spike has anything to say at all, spinning included.</summary>
        public bool HasReading { get; private set; }

        /// <summary>True when the target is the room the player is in (or there is no path): the spike turns on its own.</summary>
        public bool Spinning { get; private set; }

        /// <summary>Which doorway of the current room to take (a Heading), or -1 while spinning.</summary>
        public int Doorway { get; private set; }

        /// <summary>The doorway the green spike is pointing at, or null.</summary>
        public MagicDoor TargetDoorway { get; private set; }

        /// <summary>Flat, normalised, world space. While spinning it turns on its own so a HUD can just read it.</summary>
        public Vector3 WorldDirection { get; private set; }

        // ---------------------------------------------------------------- the red spike (a Mage alone)

        /// <summary>True only on a Mage's machine: this peer has a second, red reading to draw.</summary>
        public bool HasRedReading { get; private set; }

        public bool RedSpinning { get; private set; }

        public int RedDoorway { get; private set; }

        public Vector3 RedWorldDirection { get; private set; }

        void Awake()
        {
            _session = sessionRunner != null ? sessionRunner.Session : null;
            WorldDirection = Vector3.forward;
            RedWorldDirection = Vector3.forward;
            Doorway = -1;
            RedDoorway = -1;
        }

        void LateUpdate()
        {
            if (_session == null && sessionRunner != null) _session = sessionRunner.Session;
            Recompute();

            if (Spinning)
            {
                _spinAngle += spinDegreesPerSecond * Time.deltaTime;
                if (_spinAngle >= 360f) _spinAngle -= 360f;
                WorldDirection = Quaternion.Euler(0f, _spinAngle, 0f) * Vector3.forward;
            }
            if (RedSpinning)
            {
                _redSpinAngle += spinDegreesPerSecond * Time.deltaTime;
                if (_redSpinAngle >= 360f) _redSpinAngle -= 360f;
                RedWorldDirection = Quaternion.Euler(0f, _redSpinAngle, 0f) * Vector3.forward;
            }
        }

        void Recompute()
        {
            HasReading = false;
            Spinning = true;
            Doorway = -1;
            TargetDoorway = null;
            HasRedReading = false;
            RedSpinning = true;
            RedDoorway = -1;

            if (_session == null || _session.Sim == null || labyrinth == null) return;
            LabyrinthState lab = _session.Sim.Labyrinth;
            if (lab == null || lab.Grid == null) return;

            int cell = labyrinth.LocalCell;
            if (cell == LabyrinthGrid.NoCell) return;

            Vector3 from;
            if (!labyrinth.TryLocalPosition(out from)) return;

            // Green: this player's own compass, bent or true.
            CompassHint hint = lab.Hint(cell);
            if (hint.known)
            {
                HasReading = true;
                MagicDoor door;
                Vector3 direction;
                if (!hint.spin && TryAim(cell, hint.doorway, from, out door, out direction))
                {
                    Doorway = hint.doorway;
                    TargetDoorway = door;
                    WorldDirection = direction;
                    Spinning = false;
                }
            }

            // Red: the Resurrection Room, and only a Mage is ever shown it.
            if (lab.LocalRole != LabyrinthRole.Mage) return;
            CompassHint red = lab.Grid.ToCell(cell, lab.Grid.BadEndCell);
            if (!red.known) return;
            HasRedReading = true;
            MagicDoor redDoor;
            Vector3 redDirection;
            if (red.spin || !TryAim(cell, red.doorway, from, out redDoor, out redDirection)) return;
            RedDoorway = red.doorway;
            RedWorldDirection = redDirection;
            RedSpinning = false;
        }

        /// <summary>The flat world direction from the player to one doorway of the room they are standing in.</summary>
        bool TryAim(int cell, int doorway, Vector3 from, out MagicDoor door, out Vector3 direction)
        {
            direction = Vector3.forward;
            door = labyrinth.DoorwayOf(cell, doorway);
            if (door == null) return false;
            Vector3 flat = door.transform.position - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return false;
            direction = flat.normalized;
            return true;
        }
    }
}
