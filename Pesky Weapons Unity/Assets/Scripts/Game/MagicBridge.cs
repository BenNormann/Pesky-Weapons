using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A span of pure magic across a gap: solid and lit only while its Circuit is whole. Dead, it is NOT THERE:
    /// not drawn and no collider at all, so a body that tries it falls through. No state of its
    /// own: the circuit's bridges carry it, and every peer draws the same answer. Not in the NavMesh
    /// (NavMeshModifier ignoreFromBuild on the deck): the goblins on either side stay on their side.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicBridge : MonoBehaviour
    {
        [SerializeField] Circuit circuit;
        [Tooltip("The walkable slab's collider: on while the circuit is live.")]
        [SerializeField] Collider deck;
        [Tooltip("The slab's renderer (M_MagicFlow).")]
        [SerializeField] Renderer deckRenderer;
        [Tooltip("The bridge coming alive: the crystal shimmer, along the deck.")]
        [OptionalRef][SerializeField] AudioClip liveSound;
        [Range(0f, 1f)][SerializeField] float liveVolume = 1f;
        bool _applied;

        static readonly int LiveId = Shader.PropertyToID("_Live");
        MaterialPropertyBlock _mpb;
        bool _solid = true;

        public bool IsSolid { get { return _solid; } }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            Apply(circuit != null && circuit.IsLive);
        }

        void Update()
        {
            bool live = circuit != null && circuit.IsLive;
            if (live != _solid) Apply(live);
        }

        void Apply(bool live)
        {
            // Alive from dead, after the first look (a bridge that starts alive is silent).
            if (live && !_solid && _applied && deck != null) Sfx.Play(liveSound, deck.bounds.center, liveVolume);
            _applied = true;
            _solid = live;
            if (deck != null) deck.enabled = live;
            if (deckRenderer == null || _mpb == null) return;
            deckRenderer.enabled = live;
            deckRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(LiveId, live ? 1f : 0f);
            deckRenderer.SetPropertyBlock(_mpb);
        }
    }
}
