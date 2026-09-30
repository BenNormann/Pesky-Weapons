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
    /// Mage picked, and the spike spins while the dummy stands in its target room.
    ///
    /// THE NUDGE LESSON. It also HOPS on a schedule that is a pure function of the LevelClock (one hop
    /// every hopPeriod seconds, steered back toward where it was placed), so it is in the air regularly and
    /// the Mage can practise left click (nudge) and right click (pull) on it. The host's rule reaches it as
    /// the reserved target NudgeReqMsg.PracticeTarget through IHostWorld.TryGetPracticeTarget, and the
    /// validated NUDGE_EVENT comes back through WorldAuthority.Nudge on the host, which owns it. That is
    /// the whole tutorial shim: a reserved target id and two lines of IHostWorld.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticeDummy : MonoBehaviour
    {
        [Tooltip("The practice labyrinth (set aside; empty in the run-mode tutorial, which leaves the compass spike idle).")]
        [OptionalRef][SerializeField] LabyrinthDirector labyrinth;
        [Tooltip("The HUD whose map holds the practice chip's target (set aside; empty in the run-mode tutorial).")]
        [OptionalRef][SerializeField] LabyrinthHud hud;
        [Tooltip("Turned about Y to point its +Z child at the doorway the dummy's compass names.")]
        [OptionalRef][SerializeField] Transform spikePivot;

        [Header("Curse (the curse lesson)")]
        [Tooltip("The label over the dummy's head: its name normally, the curse it received and a countdown while one is on it.")]
        [OptionalRef][SerializeField] TMPro.TextMeshPro label;
        [SerializeField] string idleLabel = "DUMMY";
        [Tooltip("The level clock the curse countdown reads.")]
        [OptionalRef][SerializeField] LevelClock curseClock;
        [Tooltip("How fast the spike turns while spinning, degrees per second.")]
        [SerializeField] float spinDegreesPerSecond = 220f;
        [Tooltip("How quickly the spike settles on a new doorway.")]
        [SerializeField] float turnSharpness = 12f;

        [Header("Hop (the nudge lesson)")]
        [Tooltip("The level clock the hop schedule reads: hop k happens at hopPhase + k * hopPeriod seconds.")]
        [SerializeField] LevelClock clock;
        [Tooltip("The dummy's own body. Kinematic, or empty, means it never hops and can never be nudged.")]
        [SerializeField] Rigidbody body;
        [SerializeField] float hopPeriod = 3f;
        [SerializeField] float hopPhase = 0f;
        [Tooltip("Upward launch speed, m/s. 9 at g = 20: a 2 m apex and 0.9 s in the air.")]
        [SerializeField] float hopSpeed = 9f;
        [Tooltip("Most horizontal speed a hop may use to steer back toward where the dummy was placed, m/s.")]
        [SerializeField] float homeReturnMaxSpeed = 6f;
        [Tooltip("Knocked farther than this from home (or fallen 2 m below it), the next hop starts from home again.")]
        [SerializeField] float resetDistance = 8f;
        [Tooltip("Seconds after the last floor contact that still count as standing.")]
        [SerializeField] float groundedGrace = 0.08f;
        [Tooltip("Height of the body's middle above its feet (the pivot): what the Mage aims at and the host measures to.")]
        [SerializeField] float centreHeight = 0.9f;

        Vector3 _home;
        long _hopIndex = long.MinValue;
        float _lastGroundTime = -999f;

        /// <summary>Off the floor right now, as far as its own contacts say.</summary>
        public bool IsAirborne
        {
            get { return body != null && !body.isKinematic && Time.fixedTime - _lastGroundTime > groundedGrace; }
        }

        /// <summary>The middle of the body, where a nudge is aimed and measured.</summary>
        public Vector3 Centre
        {
            get { return (body != null ? body.position : transform.position) + Vector3.up * centreHeight; }
        }

        Pesky.Protocol.CurseKind _curse;
        long _curseUntilMs;
        string _shownLabel;

        /// <summary>The curse on the dummy right now, or None.</summary>
        public Pesky.Protocol.CurseKind CurseOn { get { return _curse; } }

        /// <summary>
        /// The host's validated curse on the dummy (target NudgeReqMsg.PracticeTarget). A dummy has no launch, no
        /// camera and no screen, so the whole effect is the label over its head: the curse's name and a countdown.
        /// </summary>
        public void Curse(Pesky.Protocol.CurseKind kind, float seconds)
        {
            _curse = kind;
            LevelClock c = curseClock != null ? curseClock : clock;
            long now = c != null ? c.Ms : (long)(Time.time * 1000f);
            _curseUntilMs = now + (long)(seconds * 1000f);
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (label == null) return;
            string want = idleLabel;
            if (_curse != Pesky.Protocol.CurseKind.None)
            {
                LevelClock c = curseClock != null ? curseClock : clock;
                long now = c != null ? c.Ms : (long)(Time.time * 1000f);
                long left = _curseUntilMs - now;
                if (left <= 0) _curse = Pesky.Protocol.CurseKind.None;
                else want = Pesky.Data.RunDef.CurseName((int)_curse) + " " + ((left + 999) / 1000);
            }
            if (want == _shownLabel) return;
            _shownLabel = want;
            label.text = want;
        }

        /// <summary>The host's validated nudge, applied to this body. Clamped by the caller.</summary>
        public void Nudge(Vector3 velocityChange)
        {
            if (body == null || body.isKinematic) return;
            body.AddForce(velocityChange, ForceMode.VelocityChange);
        }

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
            _home = transform.position;
            // A sleeping body gets no OnCollisionStay, and a dummy asleep on the floor would read as airborne.
            if (body != null) body.sleepThreshold = 0f;
        }

        void FixedUpdate()
        {
            if (body == null || body.isKinematic || clock == null || hopPeriod < 0.5f) return;
            long hop = (long)System.Math.Floor((clock.Seconds - hopPhase) / hopPeriod);
            if (hop == _hopIndex) return;
            bool first = _hopIndex == long.MinValue;
            _hopIndex = hop;
            // No hop the instant the level loads, and none while still in the air from the last one.
            if (first || IsAirborne) return;
            Hop();
        }

        /// <summary>One scheduled hop: straight up, plus whatever sideways speed lands it back home.</summary>
        void Hop()
        {
            Vector3 away = body.position - _home;
            if (away.magnitude > resetDistance || body.position.y < _home.y - 2f)
            {
                body.position = _home;
                transform.position = _home;
                away = Vector3.zero;
            }
            float g = Mathf.Max(0.1f, -Physics.gravity.y);
            float air = 2f * hopSpeed / g;
            Vector3 back = Vector3.ClampMagnitude(new Vector3(-away.x, 0f, -away.z) / Mathf.Max(0.1f, air), homeReturnMaxSpeed);
            body.linearVelocity = new Vector3(back.x, hopSpeed, back.z);
            body.angularVelocity = Vector3.zero;
        }

        void OnCollisionEnter(Collision collision)
        {
            NoteGround(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            NoteGround(collision);
        }

        void NoteGround(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y <= 0.5f) continue;
                _lastGroundTime = Time.fixedTime;
                return;
            }
        }

        void Update()
        {
            RefreshLabel();
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
