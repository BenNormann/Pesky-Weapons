using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The rider fix shared by every clock-driven platform (ClockMover, Lift). PhysX does not hold a
    /// Rigidbody on a kinematic platform, and friction on top of an explicit carry moves the rider twice,
    /// so: the surface uses the zero-friction P_Slick material and the platform adds its own per-step
    /// delta to every non-kinematic body found in a box just above the surface - exactly once, through
    /// Rigidbody.position only (also moving the Transform doubles the carry). Velocity is untouched, so a
    /// launch from the platform keeps its own arc.
    /// </summary>
    public static class RiderCarry
    {
        /// <summary>Collects the riders standing in the box. Returns how many were found.</summary>
        public static int Collect(Transform platform, Vector3 boxCenter, Vector3 boxSize, LayerMask mask,
            Rigidbody self, Collider[] buffer, List<Rigidbody> riders)
        {
            riders.Clear();
            Vector3 centre = platform.TransformPoint(boxCenter);
            int count = Physics.OverlapBoxNonAlloc(centre, boxSize * 0.5f, buffer, platform.rotation,
                mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = buffer[i].attachedRigidbody;
                if (rb == null || rb == self || rb.isKinematic) continue;
                if (riders.Contains(rb)) continue;
                riders.Add(rb);
            }
            return riders.Count;
        }

        /// <summary>Move, do not push: every rider gets the platform's delta once.</summary>
        public static void Move(List<Rigidbody> riders, Vector3 delta)
        {
            for (int i = 0; i < riders.Count; i++) riders[i].position = riders[i].position + delta;
        }
    }
}
