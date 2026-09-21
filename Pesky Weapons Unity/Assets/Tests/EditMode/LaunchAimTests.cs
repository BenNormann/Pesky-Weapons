using NUnit.Framework;
using Pesky.Data;
using Pesky.Game;
using UnityEngine;

namespace Pesky.Tests
{
    public sealed class LaunchAimTests
    {
        MovementTuning _t;

        static readonly Vector3[] WallNormals =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1f, 0f, 1f).normalized,
            new Vector3(-1f, 0.28f, 0.3f).normalized,
            new Vector3(0.2f, -0.28f, -1f).normalized
        };

        [SetUp]
        public void SetUp()
        {
            _t = ScriptableObject.CreateInstance<MovementTuning>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_t);
        }

        [Test]
        public void RestingPitchGivesMinimumLift_FullTiltGivesMaximum()
        {
            Assert.AreEqual(15f, LaunchAim.LiftDegrees(_t.restingCameraPitchDeg, _t), 1e-3f);
            Assert.AreEqual(80f, LaunchAim.LiftDegrees(_t.maxLiftCameraPitchDeg, _t), 1e-3f);
            Assert.AreEqual(15f, LaunchAim.LiftDegrees(75f, _t), 1e-3f, "looking further down never lowers the lift");
            Assert.AreEqual(45f, LaunchAim.LiftDegrees(LaunchAim.PitchForLift(45f, _t), _t), 1e-3f);
        }

        [Test]
        public void GroundedLaunchIsNeverBelowMinimumLift()
        {
            for (float pitch = -90f; pitch <= 90f; pitch += 2.5f)
            {
                for (float yaw = 0f; yaw < 360f; yaw += 15f)
                {
                    Vector3 dir = LaunchAim.Grounded(yaw, pitch, _t);
                    float elevation = LaunchAim.ElevationDegrees(dir);
                    Assert.AreEqual(1f, dir.magnitude, 1e-4f);
                    Assert.GreaterOrEqual(elevation, _t.minLiftDeg - 1e-3f, "pitch " + pitch + " yaw " + yaw);
                    Assert.LessOrEqual(elevation, _t.maxLiftDeg + 1e-3f, "pitch " + pitch + " yaw " + yaw);
                }
            }
        }

        [Test]
        public void HeadingFollowsYaw()
        {
            for (float yaw = 0f; yaw < 360f; yaw += 7.5f)
            {
                for (float pitch = -35f; pitch <= 75f; pitch += 11f)
                {
                    Vector3 dir = LaunchAim.Grounded(yaw, pitch, _t);
                    float heading = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, heading), 1e-2f, "yaw " + yaw + " pitch " + pitch);
                }
            }
            Assert.Greater(LaunchAim.Grounded(90f, 20f, _t).x, 0.9f, "yaw 90 points along +X");
            Assert.Greater(LaunchAim.Grounded(0f, 20f, _t).z, 0.9f, "yaw 0 points along +Z");
        }

        [Test]
        public void WallJumpAlwaysLeavesTheWall()
        {
            foreach (Vector3 n in WallNormals)
            {
                for (float yaw = 0f; yaw < 360f; yaw += 5f)
                {
                    for (float pitch = -35f; pitch <= 75f; pitch += 5f)
                    {
                        Vector3 dir = LaunchAim.WallJump(yaw, pitch, n, _t);
                        Assert.AreEqual(1f, dir.magnitude, 1e-4f);
                        Assert.Greater(Vector3.Dot(dir, n), _t.wallMinDot, "normal " + n + " yaw " + yaw + " pitch " + pitch);
                        Assert.Greater(dir.y, 0f, "a wall jump always goes up: normal " + n + " yaw " + yaw + " pitch " + pitch);
                    }
                }
            }
        }

        [Test]
        public void WallJumpReflectsAnAimThatPointsIntoTheWall()
        {
            // Wall normal +X, aiming into the wall and toward +Z: the rebound keeps going toward +Z.
            Vector3 dir = LaunchAim.WallJump(315f, 20f, Vector3.right, _t);
            Assert.Greater(dir.x, 0.3f);
            Assert.Greater(dir.z, 0f);

            // Aiming straight into the wall comes straight back out.
            Vector3 back = LaunchAim.WallJump(270f, 20f, Vector3.right, _t);
            Assert.Greater(back.x, 0.9f);
            Assert.AreEqual(0f, back.z, 1e-4f);
        }

        [Test]
        public void DirectionPrefersGroundOverWall()
        {
            Vector3 grounded = LaunchAim.Direction(270f, 20f, true, true, Vector3.right, _t);
            Assert.Less(grounded.x, 0f, "grounded launches follow the aim even next to a wall");
            Vector3 wall = LaunchAim.Direction(270f, 20f, false, true, Vector3.right, _t);
            Assert.Greater(wall.x, 0f);
        }
    }
}
