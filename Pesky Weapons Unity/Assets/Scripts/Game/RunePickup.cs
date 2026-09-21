using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A rune: hidden (and untouchable) until its condition holds, then a touch attaches its modifier to the
    /// weapon that touched it. Modifiers live on the weapon and are lost when that weapon breaks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunePickup : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [SerializeField] ModifierDef modifier;
        [Tooltip("The rune appears when every enemy in this room is dead. Empty = available at once.")]
        [OptionalRef][SerializeField] RoomVolume revealWhenRoomCleared;
        [SerializeField] GameObject visual;
        [SerializeField] Collider touchTrigger;
        [SerializeField] float spinDegreesPerSecond = 45f;

        bool _taken;
        bool _visible;

        public int SceneId { get { return id; } }
        public ModifierDef Modifier { get { return modifier; } }
        public bool Taken { get { return _taken; } }
        public bool IsAvailable
        {
            get { return !_taken && (revealWhenRoomCleared == null || revealWhenRoomCleared.Cleared); }
        }

        void OnEnable()
        {
            if (authority != null) authority.PickupTaken += OnPickupTaken;
            SetVisible(false);
        }

        void OnDisable()
        {
            if (authority != null) authority.PickupTaken -= OnPickupTaken;
        }

        void Update()
        {
            bool shouldShow = IsAvailable;
            if (shouldShow != _visible) SetVisible(shouldShow);
            if (_visible && visual != null)
                visual.transform.localRotation = Quaternion.Euler(0f, Time.time * spinDegreesPerSecond, 0f);
        }

        void SetVisible(bool on)
        {
            _visible = on;
            if (visual != null) visual.SetActive(on);
            if (touchTrigger != null) touchTrigger.enabled = on;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsAvailable || authority == null) return;
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null || !weapon.IsPossessed) return;
            authority.RequestPickup(id, weapon);
        }

        /// <summary>Authority only.</summary>
        public void ApplyTaken()
        {
            _taken = true;
        }

        void OnPickupTaken(int pickupId, WeaponBody taker)
        {
            if (pickupId == id) SetVisible(false);
        }
    }
}
