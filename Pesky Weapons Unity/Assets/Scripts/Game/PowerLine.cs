using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A SWITCH IS A WIRE (round 29, docs/level-design/rooms-v5-gatehouse.md). A pressure plate, a lever, a door, a
    /// circuit or a junction is the SOURCE of a run of magical conduit (renderers wearing M_MagicFlow) that is LIVE
    /// while the source is on: a held or latched plate, a lever that is on, a door that is open, a circuit that is
    /// whole, a junction with power. The rails and whatever hangs at their far end (a doorway's dead portal, a winch
    /// drum) light together, so a player who follows a wire always finds what it does. A pure view: it changes nothing
    /// shared, the source carries the host state. One source per line; lines meet in a PowerJunction. Lives under
    /// Gameplay; the builder's Line() wires it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerLine : MonoBehaviour
    {
        [Header("Source (exactly one)")]
        [Tooltip("Live while the plate is held (a hold plate) or has latched.")]
        [OptionalRef][SerializeField] PressurePlate plate;
        [Tooltip("Live while the lever is on.")]
        [OptionalRef][SerializeField] ImpactLever lever;
        [Tooltip("Live while the door is open (an exit whose condition is not a switch: room cleared, always open).")]
        [OptionalRef][SerializeField] Door door;
        [Tooltip("Live while the circuit is whole.")]
        [OptionalRef][SerializeField] Circuit circuit;
        [Tooltip("Live while the junction has power.")]
        [OptionalRef][SerializeField] PowerJunction junction;

        [Tooltip("Every rail piece and consumer that lights with this line (M_MagicFlow renderers).")]
        [SerializeField] Renderer[] rails = new Renderer[0];

        static readonly int LiveId = Shader.PropertyToID("_Live");
        MaterialPropertyBlock _mpb;
        int _shown = -1;

        /// <summary>Power is on this line right now.</summary>
        public bool IsLive
        {
            get
            {
                if (plate != null && (plate.Held || plate.Latched)) return true;
                if (lever != null && lever.IsOn) return true;
                if (door != null && door.IsOpen) return true;
                if (circuit != null && circuit.IsLive) return true;
                if (junction != null && junction.IsLive) return true;
                return false;
            }
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            _shown = -1;
        }

        void Update()
        {
            int live = IsLive ? 1 : 0;
            if (live == _shown) return;
            _shown = live;
            for (int i = 0; i < rails.Length; i++)
            {
                Renderer r = rails[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(LiveId, live);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
