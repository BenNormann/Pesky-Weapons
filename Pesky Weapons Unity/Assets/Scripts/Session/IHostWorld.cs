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
    }
}
