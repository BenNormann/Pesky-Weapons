using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A run of magical conduit from a source to a consumer, broken by CircuitBridges in order. Segment i
    /// (a renderer wearing M_MagicFlow) is live when bridges 0..i-1 are all closed; the whole circuit is live
    /// when every bridge is. Whole ONCE is whole for good: the circuit SEALS, every break fills itself in, the
    /// weapons may leave, and the whole run pulses with power. The host keeps reporting a sealed circuit's
    /// bridges closed, so every peer sees the same seal. Otherwise it holds no state of its own: the bridges
    /// carry the host state, this is the view plus the one question a DoorCondition (CircuitLive) or a
    /// MagicBridge asks. Lives under Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Circuit : MonoBehaviour
    {
        [Tooltip("The breaks, in order from the source.")]
        [SerializeField] CircuitBridge[] bridges = new CircuitBridge[0];
        /// <summary>One stretch of conduit between two breaks: every rail piece that lights together.</summary>
        [System.Serializable]
        public struct Run
        {
            public Renderer[] pieces;
        }

        [Tooltip("The conduit stretches in order from the source: stretch 0 is always live, stretch i needs bridges 0..i-1 closed. One more than there are bridges.")]
        [SerializeField] Run[] segments = new Run[0];
        [Tooltip("Renderers that follow the WHOLE circuit: the door's own barrier, a magic bridge's deck.")]
        [OptionalRef][SerializeField] Renderer[] consumers = new Renderer[0];

        static readonly int LiveId = Shader.PropertyToID("_Live");
        MaterialPropertyBlock _mpb;
        int _shownLive = -1;
        bool _sealed;

        /// <summary>How many bridges from the source are closed in a row.</summary>
        public int ClosedInARow
        {
            get
            {
                int n = 0;
                for (int i = 0; i < bridges.Length; i++)
                {
                    if (bridges[i] == null || !bridges[i].Closed) break;
                    n++;
                }
                return n;
            }
        }

        /// <summary>Power reaches the consumer: every break is closed, or was once (sealed).</summary>
        public bool IsLive { get { return _sealed || ClosedInARow >= bridges.Length; } }
        /// <summary>The circuit was whole once and has filled its breaks in: live for good.</summary>
        public bool IsSealed { get { return _sealed; } }
        public int BridgeCount { get { return bridges.Length; } }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            int live = ClosedInARow;
            if (!_sealed && bridges.Length > 0 && live >= bridges.Length) _sealed = true;   // whole once: whole for good
            if (_sealed) live = bridges.Length;
            // The first open break is fed (it sparks); the ones after it are dead. Sealed, every break fills itself in.
            for (int i = 0; i < bridges.Length; i++)
                if (bridges[i] != null) { bridges[i].Powered = i <= live; bridges[i].Sealed = _sealed; }
            // Sealed, the whole run pulses with power: _Live above 1 brightens the shader.
            float glow = _sealed ? 1.35f + 0.35f * Mathf.Sin(Time.time * 3.5f) : 1f;
            if (live == _shownLive && !_sealed) return;
            _shownLive = live;
            for (int i = 0; i < segments.Length; i++)
            {
                Renderer[] pieces = segments[i].pieces;
                if (pieces == null) continue;
                float v = i <= live ? glow : 0f;
                for (int j = 0; j < pieces.Length; j++) Set(pieces[j], v);
            }
            bool whole = live >= bridges.Length;
            for (int i = 0; i < consumers.Length; i++) Set(consumers[i], whole ? glow : 0f);
            for (int i = 0; i < bridges.Length; i++) if (bridges[i] != null) bridges[i].Glow = glow;
        }

        void Set(Renderer r, float live)
        {
            if (r == null || _mpb == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(LiveId, live);
            r.SetPropertyBlock(_mpb);
        }
    }
}
