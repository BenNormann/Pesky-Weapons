using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    public enum LaunchResult
    {
        Launched,
        WallJumped,
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
        float _lastWallJumpFixedTime = -999f;
        int _wallJumpsUsed;

        public WeaponBody Weapon { get { return weapon; } }
        public float YawDeg { get { return _yawDeg; } }
        public float PitchDeg { get { return _pitchDeg; } }
        public int WallJumpsUsed { get { return _wallJumpsUsed; } }
        public Vector2 RollInput { get { return _roll; } }

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

            LaunchResult result;
            Vector3 dir;
            if (weapon.IsGrounded)
            {
                dir = LaunchAim.Grounded(_yawDeg, _pitchDeg, t);
                if (weapon.IsStuck)
                {
                    // Stuck in wood: never launch into it. A wooden wall rebounds like a wall jump (without
                    // using one up); a floor or ceiling just loses the part of the aim that points into it.
                    Vector3 n = weapon.StuckNormal;
                    float into = Vector3.Dot(dir, n);
                    if (Mathf.Abs(n.y) < t.wallNormalY)
                    {
                        if (into < t.wallMinDot) dir = LaunchAim.WallJump(_yawDeg, _pitchDeg, n, t);
                    }
                    else if (into < 0f && (dir - n * into).sqrMagnitude > 1e-6f)
                    {
                        dir = (dir - n * into).normalized;
                    }
                }
                result = LaunchResult.Launched;
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

            velocity = dir * weapon.Def.launchSpeed;
            if (t.compensateIntegrator) velocity.y += 0.5f * t.gravity * Time.fixedDeltaTime;
            if (Time.time - _lastLaunchTime < t.launchCooldown) return LaunchResult.RefusedCooldown;
            return result;
        }

        /// <summary>The Jump input. Returns what happened.</summary>
        public LaunchResult TryLaunch()
        {
            Vector3 velocity;
            LaunchResult result = Evaluate(out velocity);
            if (result != LaunchResult.Launched && result != LaunchResult.WallJumped) return result;

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
            if (result == LaunchResult.WallJumped)
            {
                _wallJumpsUsed++;
                _lastWallJumpFixedTime = Time.fixedTime;
            }
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
