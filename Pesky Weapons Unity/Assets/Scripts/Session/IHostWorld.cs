using System.Collections.Generic;
using Pesky.Protocol;

namespace Pesky.Session
{
    /// <summary>
    /// The Game-side half of the host. Pesky's goblins are NavMeshAgent brains and its kit pieces are
    /// physics objects, so two things a plain C# rule cannot know live behind this interface: where the
    /// enemies are this tick, and whether a client's kit claim is true of the scene. Game implements it
    /// (WorldAuthority) and hands it to NetSession.HostWorld; only the host ever calls it.
    /// </summary>
    public interface IHostWorld
    {
        /// <summary>
        /// Fill <paramref name="into"/> with the enemy rows worth sending. When <paramref name="intervalDue"/>
        /// is false only rows whose state changed (not mere movement) belong. Returns true when at least one
        /// row changed state, which sends the batch at once instead of waiting for the interval.
        /// </summary>
        bool CollectEnemyRows(List<EnemyStateMsg.Row> into, bool intervalDue);

        /// <summary>
        /// A client claims its weapon changed a kit piece. Check it against the scene (the piece exists, is
        /// not already in that state, the claimant's weapon is the right kind and near enough) and fill in
        /// value and clockMs. False refuses the claim.
        /// </summary>
        bool ValidateKit(byte fromSlot, float speed, ref KitStateMsg proposal);

        /// <summary>
        /// Host: where every player is in the labyrinth. Fills <paramref name="cellBySlot"/> with the grid
        /// cell each slot stands in (-1 unknown) and <paramref name="atExitBySlot"/> with whether that slot
        /// is gathered at the exit doorway. False when this scene has no labyrinth, which leaves the round
        /// rules idle. Only the host calls it.
        /// </summary>
        bool CollectPlayerCells(int[] cellBySlot, bool[] atExitBySlot);

        /// <summary>
        /// Host: true when nothing of the authored World geometry lies on the straight line between two points
        /// (a Mage's nudge needs to see its target). Checked on the host's own scene.
        /// </summary>
        bool HasLineOfSight(UnityEngine.Vector3 from, UnityEngine.Vector3 to);

        /// <summary>
        /// Host, tutorial only: where the practice dummy is and whether it is in the air, so a nudge on
        /// NudgeReqMsg.PracticeTarget goes through the same checks as one on a player. False when this scene
        /// has no dummy.
        /// </summary>
        bool TryGetPracticeTarget(out UnityEngine.Vector3 position, out bool airborne);

        /// <summary>
        /// Host, run mode: the room ids of this scene's POOL (every room a run may string together; never
        /// the start or Exit room). False when this scene has no run (the tutorial, the labyrinth), which
        /// leaves the run rule waiting. The host's scene is asked, not data, because the pool is authored.
        /// </summary>
        bool CollectRunPool(List<ushort> into);

        /// <summary>
        /// Host, run mode: where every player stands, from the sim's streamed poses against this scene's
        /// rooms: the start room, one of the run's rooms, or inside the Exit room's volume. Unknown for a
        /// slot with no pose or standing in no room. False when this scene has no run.
        /// </summary>
        bool CollectRunPlaces(RunPlace[] placeBySlot);
    }
}
