using System;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>Spawns the local PlayerSoul prefab at a spawn point and hands it to the orbit camera.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] PlayerSoul soulPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("Which spawn point the local player uses (the netcode port will pick by player slot).")]
        [SerializeField] int localSpawnIndex;
        [Tooltip("Handed to the soul so every shared state change it makes goes through the authority.")]
        [SerializeField] WorldAuthority authority;
        [Tooltip("The scene's SessionRunner: the local slot picks the spawn point.")]
        [SerializeField] SessionRunner session;

        public PlayerSoul LocalSoul { get; private set; }
        public WorldAuthority Authority { get { return authority; } }

        /// <summary>Raised once the local soul exists, so views (the HUD) can bind to it.</summary>
        public event Action<PlayerSoul> SoulSpawned;

        void Start()
        {
            if (soulPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("PlayerSpawner needs a soul prefab and at least one spawn point.", this);
                return;
            }

            // One spawn point per player slot; slots past the authored points wrap around.
            int index = session != null && session.HasSlot
                ? session.LocalSlot % spawnPoints.Length
                : Mathf.Clamp(localSpawnIndex, 0, spawnPoints.Length - 1);
            Transform point = spawnPoints[index];
            LocalSoul = Instantiate(soulPrefab, point.position, Quaternion.identity);
            LocalSoul.name = soulPrefab.name;
            if (orbitCamera != null) orbitCamera.SetLook(point.eulerAngles.y, orbitCamera.RestingPitch);
            LocalSoul.Init(orbitCamera, authority);
            if (SoulSpawned != null) SoulSpawned(LocalSoul);
        }
    }
}
