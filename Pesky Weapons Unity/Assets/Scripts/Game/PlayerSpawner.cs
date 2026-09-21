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

            Transform point = spawnPoints[Mathf.Clamp(localSpawnIndex, 0, spawnPoints.Length - 1)];
            LocalSoul = Instantiate(soulPrefab, point.position, Quaternion.identity);
            LocalSoul.name = soulPrefab.name;
            if (orbitCamera != null) orbitCamera.SetLook(point.eulerAngles.y, orbitCamera.RestingPitch);
            LocalSoul.Init(orbitCamera, authority);
            if (SoulSpawned != null) SoulSpawned(LocalSoul);
        }
    }
}
