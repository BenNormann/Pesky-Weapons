using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Where two or more PowerLines meet on a wall (round 29): a box that has power when ANY input line is live
    /// (or every one, with Require All), and is itself the source of the line out of it. A gate held up by a
    /// plate OR pinned by a lever is a junction with two lines in and one line out to the winch. A pure view,
    /// like the lines. Lives under Gameplay; its box (an M_MagicFlow block in Geometry) lights with it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerJunction : MonoBehaviour
    {
        [Tooltip("The lines coming in.")]
        [SerializeField] PowerLine[] inputs = new PowerLine[0];
        [Tooltip("On: every input must be live (AND). Off: any one is enough (OR).")]
        [SerializeField] bool requireAll;
        [Tooltip("The junction box renderer (M_MagicFlow); lights with the junction.")]
        [OptionalRef][SerializeField] Renderer box;

        static readonly int LiveId = Shader.PropertyToID("_Live");
        MaterialPropertyBlock _mpb;
        int _shown = -1;

        public bool IsLive
        {
            get
            {
                if (inputs == null || inputs.Length == 0) return false;
                for (int i = 0; i < inputs.Length; i++)
                {
                    bool live = inputs[i] != null && inputs[i].IsLive;
                    if (requireAll && !live) return false;
                    if (!requireAll && live) return true;
                }
                return requireAll;
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
            if (box == null) return;
            int live = IsLive ? 1 : 0;
            if (live == _shown) return;
            _shown = live;
            box.GetPropertyBlock(_mpb);
            _mpb.SetFloat(LiveId, live);
            box.SetPropertyBlock(_mpb);
        }
    }
}
