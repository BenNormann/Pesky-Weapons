using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A touch pickup on the Pickup layer (which only collides with Weapon, so a free soul cannot take it).
    /// Keys are party inventory and are never lost, so the authority keeps them, not the weapon.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyPickup : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [Tooltip("Which key this is. Doors ask the party for this id.")]
        [SerializeField] int keyId = 1;
        [Tooltip("Hidden when the key has been taken.")]
        [SerializeField] GameObject visual;
        [SerializeField] float spinDegreesPerSecond = 60f;
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSeconds = 2f;

        Vector3 _visualLocal;
        bool _taken;

        public int SceneId { get { return id; } }
        public int KeyId { get { return keyId; } }
        public bool Taken { get { return _taken; } }

        void Awake()
        {
            if (visual != null) _visualLocal = visual.transform.localPosition;
        }

        void OnEnable()
        {
            if (authority != null) authority.PickupTaken += OnPickupTaken;
        }

        void OnDisable()
        {
            if (authority != null) authority.PickupTaken -= OnPickupTaken;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_taken || authority == null) return;
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null) return;
            authority.RequestPickup(id, weapon);
        }

        /// <summary>Authority only.</summary>
        public void ApplyTaken()
        {
            _taken = true;
        }

        void OnPickupTaken(int pickupId, WeaponBody taker)
        {
            if (pickupId != id) return;
            if (visual != null) visual.SetActive(false);
        }

        void Update()
        {
            if (_taken || visual == null) return;
            visual.transform.localRotation = Quaternion.Euler(0f, Time.time * spinDegreesPerSecond, 0f);
            float phase = bobSeconds > 0.001f ? Time.time / bobSeconds : 0f;
            visual.transform.localPosition = _visualLocal + Vector3.up * (Mathf.Sin(phase * Mathf.PI * 2f) * bobHeight);
        }
    }
}
