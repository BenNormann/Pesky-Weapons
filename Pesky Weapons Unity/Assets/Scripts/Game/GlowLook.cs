using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Shared view helper for the glow on action pieces (lever, pressure plate). The trims wear M_MagicFlow (the
    /// labyrinth's magic: dark purple with drifting lavender specks, `_Live` 1 idle and above 1 surging), the halo,
    /// sheen and shells wear Pesky/ActionGlow in the same purple (`_EmissionColor`). Both properties are pushed to
    /// every glow renderer through one property block; a material ignores the one it does not have.
    /// </summary>
    public static class GlowLook
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int Live = Shader.PropertyToID("_Live");
        /// <summary>The _Live value a magic trim wears while its piece is active: the sealed circuit's surge.</summary>
        public const float ActiveLive = 1.6f;

        public static void SetEmission(Renderer r, MaterialPropertyBlock mpb, Color hdr) { Set(r, mpb, hdr, 1f); }

        public static void Set(Renderer r, MaterialPropertyBlock mpb, Color hdr, float live)
        {
            if (r == null || mpb == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColor, hdr);
            mpb.SetFloat(Live, live);
            r.SetPropertyBlock(mpb);
        }
    }
}
