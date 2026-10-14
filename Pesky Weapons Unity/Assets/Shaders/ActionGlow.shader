// Pesky - the soft glow round an action piece (pressure plate, impact lever). Additive, unlit, no lights.
// Two shapes from one shader, picked per material by _Mode:
//   0 = floor halo: a flat quad bigger than the piece; light spills outward from a rectangle that sits
//       _Margin metres inside the quad's edge (the piece's border) and fades to nothing at the quad's edge.
//   1 = rising sheen: a vertical quad standing on an edge; bright at the bottom, gone at the top.
// The state colour arrives per renderer as _EmissionColor (GlowLook), the same property the trims use.
// _Pulse breathes the brightness so an idle piece reads as "do something with me".
Shader "Pesky/ActionGlow"
{
    Properties
    {
        [HDR] _EmissionColor ("Glow colour", Color) = (0.9, 0.68, 0.2, 1)
        _Intensity ("Intensity", Float) = 0.8
        [Enum(Halo,0,Sheen,1)] _Mode ("Mode", Float) = 0
        _Margin ("Halo: spill width (m)", Float) = 0.35
        _Falloff ("Halo: falloff sharpness", Float) = 2.2
        _Pulse ("Pulse amount", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse speed", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float _Intensity;
                float _Mode;
                float _Margin;
                float _Falloff;
                float _Pulse;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float2 sizeM : TEXCOORD1; float phase : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                // The quad's size in metres, from the object's own scale (Unity's Quad is 1 x 1 in x, y).
                o.sizeM = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                // Pieces placed apart breathe out of step.
                float3 originWS = unity_ObjectToWorld._m03_m13_m23;
                o.phase = dot(originWS, float3(1.7, 0.0, 2.3));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float a;
                if (_Mode < 0.5)
                {
                    // Distance in metres from the inner rectangle (the piece's border), outward positive.
                    float2 p = abs(i.uv - 0.5) * i.sizeM;
                    float2 box = max(i.sizeM * 0.5 - _Margin, 0.001);
                    float2 q = p - box;
                    float outside = length(max(q, 0.0));
                    float inside = -min(max(q.x, q.y), 0.0);
                    float t = saturate(outside / _Margin);
                    // Exponential spill outward, forced to zero at the quad edge; a short soft lip inward.
                    a = exp(-t * _Falloff * 2.0) * (1.0 - t) * (1.0 - t);
                    a *= (outside > 0.0) ? 1.0 : exp(-inside * 14.0);
                }
                else
                {
                    // Bright at the foot, gone at the top, softened at both ends of the edge.
                    float up = 1.0 - i.uv.y;
                    float ends = smoothstep(0.0, 0.08, i.uv.x) * smoothstep(1.0, 0.92, i.uv.x);
                    a = up * up * up * ends;
                }
                float pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed + i.phase);
                float3 c = _EmissionColor.rgb * (_Intensity * a * pulse);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
