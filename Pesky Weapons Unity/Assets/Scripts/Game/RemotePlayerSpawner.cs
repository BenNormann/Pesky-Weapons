using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Makes one RemotePlayerView per OTHER present slot and removes it when the slot empties. It reads the
    /// sim's player table every frame instead of listening for slot events, because the session outlives
    /// the scene: a level loaded into a running session must show the players who were already there.
    /// A peer leaving clears its slot through PEER_SLOTS, which despawns its view; the session ending
    /// (the host left: SESSION_END) despawns them all. Every view is told about the pause (it holds) and a
    /// banished player's chip says BANISHED and the role the vote revealed (docs/VOTING.md).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RemotePlayerSpawner : MonoBehaviour
    {
        [SerializeField] SessionRunner session;
        [SerializeField] WorldAuthority authority;
        [SerializeField] RemotePlayerView viewPrefab;
        [Tooltip("Spawned views are parented here to keep the hierarchy tidy.")]
        [SerializeField] Transform viewRoot;
        [Tooltip("Under a banished player's name.")]
        [SerializeField] string banishedMageTag = "BANISHED - THE ARCH MAGE";
        [SerializeField] string banishedWeaponTag = "BANISHED - A WEAPON";

        readonly RemotePlayerView[] _views = new RemotePlayerView[Wire.MaxPlayers];

        public RemotePlayerView ViewOf(byte slot)
        {
            return slot < _views.Length ? _views[slot] : null;
        }

        void Update()
        {
            NetSession s = session != null ? session.Session : null;
            if (s == null || s.Sim == null || viewPrefab == null) return;

            bool live = s.IsStarted && s.Slots.HasLocalSlot && s.Phase != SessionPhase.Ended;
            bool paused = authority != null && authority.IsPaused;
            byte local = s.LocalSlot;
            PlayerTable players = s.Sim.Players;
            for (int i = 0; i < _views.Length; i++)
            {
                PlayerState row = players[i];
                bool want = live && row != null && row.present && i != local;
                RemotePlayerView view = _views[i];
                if (!want)
                {
                    if (view != null)
                    {
                        Destroy(view.gameObject);
                        _views[i] = null;
                    }
                    continue;
                }
                if (view == null)
                {
                    view = Instantiate(viewPrefab, row.pos, Quaternion.identity, viewRoot);
                    view.name = viewPrefab.name + "_" + i;
                    view.Init((byte)i, row.name, authority != null ? authority.ViewCamera : null);
                    _views[i] = view;
                }
                view.SetName(ChipText(row, (byte)i));
                view.SetPaused(paused);
                view.Feed(row, authority != null ? authority.RemoteWeaponOf((byte)i) : null);
            }
        }

        string ChipText(PlayerState row, byte slot)
        {
            string name = string.IsNullOrEmpty(row.name) ? "Player " + (slot + 1) : row.name;
            if (authority == null || !authority.IsBanished(slot)) return name;
            return name + "\n" + (authority.RevealedRole(slot) == LabyrinthRole.Mage ? banishedMageTag : banishedWeaponTag);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null) Destroy(_views[i].gameObject);
        }
    }
}
