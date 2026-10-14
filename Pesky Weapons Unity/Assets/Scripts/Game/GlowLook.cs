using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Shared view helper for the thin emissive border trims on action pieces (lever, pressure plate).
    /// The trims use M_ActionGlow; the state colour is pushed per renderer through a property block.
    /// </summary>
    public static class GlowLook
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public static void SetEmission(Renderer r, MaterialPropertyBlock mpb, Color hdr)
        {
            if (r == null || mpb == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColor, hdr);
            r.SetPropertyBlock(mpb);
        }
    }
}
