using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// What a curse on the possessing player does to a launch (docs/RUN.md): a speed scale (HEAVY) and a bend
    /// of the direction (MAGNETIC toward the nearest weapon, NAUSEA a wandering heading). Set on the motor by
    /// the local player's CurseEffects while a curse is on; null means no curse. Evaluate applies it, so the
    /// trajectory preview shows the cursed launch too.
    /// </summary>
    public interface ILaunchCurse
    {
        float SpeedScale { get; }
        Vector3 BendDirection(Vector3 direction, Vector3 from);
    }

    public enum LaunchResult
    {
        Launched,
        WallJumped,
        /// <summary>A blade stuck in a wooden wall launched straight up it to stab back in higher (round 29).</summary>
        Climbed,
        RefusedCooldown,
        RefusedAirborne,
        RefusedWallJumpUsed,
        RefusedBroken
    }

    /// <summary>
    /// Turns intent into motion (docs/SLICE-1.md section 2). It never reads input itself: whoever drives
    /// the weapon calls SetAim / TryLaunch / SetRoll, which is also the seam the netcode port will use.
    /// Launch SETS the velocity to dir * launchSpeed. Roll is ignored unless the WeaponDef says canRoll (Orb).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponBody))]
    public sealed class WeaponMotor : MonoBehaviour
    {
        [SerializeField] WeaponBody weapon;

        float _yawDeg;
        float _pitchDeg = 20f;
        Vector2 _roll;
        float _lastLaunchTime = -999f;
        Vector3 _lastLaunchDirection = Vector3.up;
        float _lastWallJumpFixedTime = -999f;
        int _wallJumpsUsed;

        public WeaponBody Weapon { get { return weapon; } }
        public float YawDeg { get { return _yawDeg; } }
        public float PitchDeg { get { return _pitchDeg; } }
        public int WallJumpsUsed { get { return _wallJumpsUsed; } }
        public Vector2 RollInput { get { return _roll; } }
        /// <summary>The direction the last launch set out on: a braced friend struck by this body flies along it.</summary>
        public Vector3 LastLaunchDirection { get { return _lastLaunchDirection; } }

        /// <summary>The curse on whoever possesses this weapon, or null. Owned by the local player's CurseEffects.</summary>
        public ILaunchCurse Curse { get; set; }

        void Reset()
        {
            weapon = GetComponent<WeaponBody>();
        }

        void OnValidate()
        {
            if (weapon == null) weapon = GetComponent<WeaponBody>();
        }

        /// <summary>Camera yaw and pitch in degrees (pitch + looks down).</summary>
        public void SetAim(float yawDeg, float pitchDeg)
        {
            _yawDeg = yawDeg;
            _pitchDeg = pitchDeg;
        }

        /// <summary>Roll input (x = right, y = forward), camera relative. Only the Orb reacts.</summary>
        public void SetRoll(Vector2 input)
        {
            _roll = weapon.Def != null && weapon.Def.canRoll ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;
        }

        /// <summary>What a launch would do right now, and the velocity it would set.</summary>
        public LaunchResult Evaluate(out Vector3 velocity)
        {
            MovementTuning t = weapon.Tuning;
            velocity = Vector3.zero;
            if (weapon.IsBroken) return LaunchResult.RefusedBroken;
            // A wedged body (a blade in a floor-wall seam) is pushed clear first, so the launch is never absorbed by geometry.
            weapon.ResolveOverlaps();

            LaunchResult result;
            Vector3 dir;
            if (weapon.IsGrounded)
            {
                dir = LaunchAim.Grounded(_yawDeg, _pitchDeg, t);
                result = LaunchResult.Launched;
                if (weapon.IsStuck)
                {
                    // Stuck in wood: never launch into it. On a wooden WALL, looking back at the timber and launching CLIMBS
                    // it: straight up along the face, and the blade stabs back in where it stops rising (WeaponBody.TickClimb);
                    // looking away leaves it, and an aim that still points into the wall rebounds like a wall jump (without
                    // using one up). A floor or ceiling just loses the part of the aim that points into it.
                    Vector3 n = weapon.StuckNormal;
                    float into = Vector3.Dot(dir, n);
                    if (Mathf.Abs(n.y) < t.wallNormalY)
                    {
                        Vector3 flatN = new Vector3(n.x, 0f, n.z).normalized;
                        if (t.climbEnabled && Vector3.Dot(LaunchAim.Heading(_yawDeg), flatN) < -t.climbHeadingDot)
                        {
                            dir = Vector3.up;
                            result = LaunchResult.Climbed;
                        }
                        else if (into < t.wallMinDot) dir = LaunchAim.WallJump(_yawDeg, _pitchDeg, n, t);
                    }
                    else if (into < 0f && (dir - n * into).sqrMagnitude > 1e-6f)
                    {
                        dir = (dir - n * into).normalized;
                    }
                }
            }
            else if (weapon.IsTouchingWall)
            {
                if (_wallJumpsUsed >= t.wallJumpsPerAirtime) return LaunchResult.RefusedWallJumpUsed;
                dir = LaunchAim.WallJump(_yawDeg, _pitchDeg, weapon.WallNormal, t);
                result = LaunchResult.WallJumped;
            }
            else
            {
                return LaunchResult.RefusedAirborne;
            }

            float speed = weapon.Def.launchSpeed;
            if (result == LaunchResult.Climbed) speed *= t.climbSpeedScale;
            ILaunchCurse curse = Curse;
            if (curse != null)
            {
                Vector3 bent = curse.BendDirection(dir, weapon.Body != null ? weapon.Body.position : transform.position);
                if (bent.sqrMagnitude > 1e-6f) dir = bent.normalized;
                speed *= Mathf.Clamp(curse.SpeedScale, 0.05f, 1f);
            }
            velocity = dir * speed;
            if (t.compensateIntegrator) velocity.y += 0.5f * t.gravity * Time.fixedDeltaTime;
            if (Time.time - _lastLaunchTime < t.launchCooldown) return LaunchResult.RefusedCooldown;
            return result;
        }

        /// <summary>The Jump input. Returns what happened.</summary>
        public LaunchResult TryLaunch()
        {
            Vector3 velocity;
            LaunchResult result = Evaluate(out velocity);
            if (result != LaunchResult.Launched && result != LaunchResult.WallJumped && result != LaunchResult.Climbed) return result;

            Vector3 climbNormal = weapon.StuckNormal;   // read before Unstick: a climb hop probes for the timber it left
            weapon.Unstick();
            Rigidbody rb = weapon.Body;
            rb.WakeUp();
            rb.linearVelocity = velocity;

            // A little nose-up tumble about the launch's right-hand axis.
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 1e-6f && weapon.Def.launchTorque != 0f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);
                rb.AddTorque(-right * weapon.Def.launchTorque, ForceMode.VelocityChange);
            }

            weapon.MarkLaunched();
            _lastLaunchTime = Time.time;
            if (velocity.sqrMagnitude > 1e-6f) _lastLaunchDirection = velocity.normalized;
            if (result == LaunchResult.WallJumped)
            {
                _wallJumpsUsed++;
                _lastWallJumpFixedTime = Time.fixedTime;
            }
            else if (result == LaunchResult.Climbed) weapon.BeginClimb(climbNormal);
            return result;
        }

        void FixedUpdate()
        {
            if (weapon.IsBroken) return;

            // The wall-jump counter resets on the first ground contact after the wall jump.
            if (_wallJumpsUsed > 0 && weapon.LastGroundContactTime > _lastWallJumpFixedTime) _wallJumpsUsed = 0;

            if (_roll.sqrMagnitude > 1e-4f && weapon.Def.canRoll)
            {
                Vector3 forward = LaunchAim.Heading(_yawDeg);
                Vector3 right = LaunchAim.Right(_yawDeg);
                // Rolling forward spins about +right; rolling right spins about -forward.
                weapon.MarkAnimated();
                Vector3 torque = (right * _roll.y - forward * _roll.x) * weapon.Def.rollTorque;
                weapon.Body.AddTorque(torque, ForceMode.Force);
            }
        }
    }
}
