using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The tutorial's practice crew member, given a body: a grey-box figure standing in the practice
    /// labyrinth with a world-space compass over its head. Its compass is worked out exactly like the
    /// player's own (the shortest path through the doorways, from the cell the dummy stands in), but from
    /// the target the Mage set on the map's practice chip, which lives only on this machine. So bending
    /// the stand-in visibly swings the spike over its head toward the Resurrection Room or the room the
    /// Mage picked, and the spike spins while the dummy stands in its target room. A pure view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticeDummy : MonoBehaviour
    {
        [SerializeField] LabyrinthDirector labyrinth;
        [Tooltip("The HUD whose map holds the practice chip's target.")]
        [SerializeField] LabyrinthHud hud;
        [Tooltip("Turned about Y to point its +Z child at the doorway the dummy's compass names.")]
        [SerializeField] Transform spikePivot;
        [Tooltip("How fast the spike turns while spinning, degrees per second.")]
        [SerializeField] float spinDegreesPerSecond = 220f;
        [Tooltip("How quickly the spike settles on a new doorway.")]
        [SerializeField] float turnSharpness = 12f;

        float _spin;
        float _yaw;
        bool _hasYaw;

        public bool Spinning { get; private set; }
        /// <summary>The doorway (a Heading) the spike points at, or -1 while spinning.</summary>
        public int Doorway { get; private set; }
        public CompassTargetKind TargetKind { get; private set; }

        void Awake()
        {
            Doorway = -1;
        }

        void Update()
        {
            if (labyrinth == null || spikePivot == null) return;

            CompassTargetKind kind = CompassTargetKind.GoodEnd;
            int targetCell = LabyrinthGrid.NoCell;
            if (hud != null) hud.TryGetPracticeCompass(out kind, out targetCell);
            TargetKind = kind;

            bool spin = true;
            float wantYaw = 0f;
            Doorway = -1;
            LabyrinthGrid grid = labyrinth.Grid;
            int cell = labyrinth.CellAt(transform.position);
            if (grid != null && cell != LabyrinthGrid.NoCell)
            {
                CompassHint hint;
                switch (kind)
                {
                    case CompassTargetKind.BadEnd: hint = grid.ToCell(cell, grid.BadEndCell); break;
                    case CompassTargetKind.Cell: hint = grid.ToCell(cell, targetCell); break;
                    default: hint = grid.ToExit(cell); break;
                }
                MagicDoor door;
                Vector3 direction;
                if (hint.known && !hint.spin && CompassModel.TryAim(labyrinth, cell, hint.doorway, transform.position, out door, out direction))
                {
                    spin = false;
                    wantYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    Doorway = hint.doorway;
                }
            }

            Spinning = spin;
            if (spin)
            {
                _spin += spinDegreesPerSecond * Time.deltaTime;
                if (_spin >= 360f) _spin -= 360f;
                _yaw = _spin;
                _hasYaw = false;
            }
            else if (!_hasYaw)
            {
                _yaw = wantYaw;
                _hasYaw = true;
            }
            else
            {
                _yaw = Mathf.LerpAngle(_yaw, wantYaw, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            }
            spikePivot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }
}
