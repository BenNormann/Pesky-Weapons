using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Pure launch-direction maths (docs/SLICE-1.md section 2). No scene state, no time: camera yaw and
    /// pitch plus the contact situation go in, a unit direction comes out. Covered by EditMode tests.
    /// Camera pitch convention: positive looks DOWN at the weapon, negative looks up.
    /// </summary>
    public static class LaunchAim
    {
        /// <summary>Linear pitch-to-lift map, clamped to [minLift, maxLift].</summary>
        public static float LiftDegrees(float cameraPitchDeg, MovementTuning t)
        {
            float k = Mathf.InverseLerp(t.restingCameraPitchDeg, t.maxLiftCameraPitchDeg, cameraPitchDeg);
            return Mathf.Lerp(t.minLiftDeg, t.maxLiftDeg, k);
        }

        /// <summary>Inverse of <see cref="LiftDegrees"/>: the camera pitch that produces a given lift.</summary>
        public static float PitchForLift(float liftDeg, MovementTuning t)
        {
            float k = Mathf.InverseLerp(t.minLiftDeg, t.maxLiftDeg, liftDeg);
            return Mathf.Lerp(t.restingCameraPitchDeg, t.maxLiftCameraPitchDeg, k);
        }

        /// <summary>Horizontal unit heading for a camera yaw (0 = +Z, 90 = +X).</summary>
        public static Vector3 Heading(float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>Right-hand vector of a heading (for camera-relative torque).</summary>
        public static Vector3 Right(float yawDeg)
        {
            Vector3 f = Heading(yawDeg);
            return new Vector3(f.z, 0f, -f.x);
        }

        static Vector3 Elevate(Vector3 flatUnit, float liftDeg)
        {
            float r = liftDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            return new Vector3(flatUnit.x * c, Mathf.Sin(r), flatUnit.z * c);
        }

        /// <summary>Grounded launch: heading = yaw, elevation = lift map. Never below minLift.</summary>
        public static Vector3 Grounded(float yawDeg, float pitchDeg, MovementTuning t)
        {
            return Elevate(Heading(yawDeg), LiftDegrees(pitchDeg, t));
        }

        /// <summary>
        /// Wall jump: the grounded aim, reflected about the wall normal if it points into the wall, then
        /// blended with the normal raised by the same lift, then pushed out along the normal until
        /// dot(dir, wallNormal) is above wallMinDot.
        /// </summary>
public static Vector3 WallJump(float yawDeg, float pitchDeg, Vector3 wallNormal, MovementTuning t)
        {
            float lift = LiftDegrees(pitchDeg, t);
            Vector3 heading = Heading(yawDeg);
            Vector3 flatN = new Vector3(wallNormal.x, 0f, wallNormal.z);
            if (flatN.sqrMagnitude < 1e-8f) return Elevate(heading, lift);
            flatN.Normalize();
            Vector3 n = wallNormal.normalized;

            // Reflect the heading off the wall, blend it toward the normal, keep the lift as the elevation.
            if (Vector3.Dot(heading, flatN) < 0f) heading = Vector3.Reflect(heading, flatN);
            Vector3 blended = Vector3.Lerp(heading, flatN, t.wallReboundBlend);
            heading = blended.sqrMagnitude > 1e-8f ? blended.normalized : flatN;
            Vector3 dir = Elevate(heading, lift);

            float target = Mathf.Clamp(t.wallMinDot + t.wallDotMargin, 0f, 0.95f);
            float a = Vector3.Dot(dir, n);
            if (a < target)
            {
                // Smallest k with dot(normalize(dir + k n), n) == target.
                float k = -a + target * Mathf.Sqrt((1f - a * a) / (1f - target * target));
                dir = (dir + n * k).normalized;
            }
            return dir;
        }

        /// <summary>Grounded wins; otherwise a wall normal gives the rebound.</summary>
        public static Vector3 Direction(float yawDeg, float pitchDeg, bool grounded, bool onWall, Vector3 wallNormal, MovementTuning t)
        {
            if (!grounded && onWall) return WallJump(yawDeg, pitchDeg, wallNormal, t);
            return Grounded(yawDeg, pitchDeg, t);
        }

        /// <summary>Elevation of a direction above the horizontal, in degrees.</summary>
        public static float ElevationDegrees(Vector3 dir)
        {
            return Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
