// Pesky - the soft glow round an action piece (pressure plate, impact lever). Additive, unlit, no lights.
// Three shapes from one shader, picked per material by _Mode:
//   0 = floor halo: a flat quad bigger than the piece; light spills outward from a rectangle that sits
//       _Margin metres inside the quad's edge (the piece's border) and fades to nothing at the quad's edge.
//   1 = rising sheen: a vertical quad standing on an edge; bright at the bottom, gone at the top.
//   2 = shell: a cube a little bigger than a box-shaped part (lever post, handle); every edge of the box
//       glows, _Edge metres wide, over a faint _Fill across the faces. Use _Cull Back on shell materials.
// The state colour arrives per renderer as _EmissionColor (GlowLook), the same property the trims use.
// _Pulse breathes the brightness so an idle piece reads as "do something with me".
Shader "Pesky/ActionGlow"
{
    Properties
    {
        [HDR] _EmissionColor ("Glow colour", Color) = (0.9, 0.68, 0.2, 1)
        _Intensity ("Intensity", Float) = 0.8
        [Enum(Halo,0,Sheen,1,Shell,2)] _Mode ("Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        _Edge ("Shell: edge glow width (m)", Float) = 0.04
        _Fill ("Shell: face fill", Range(0, 1)) = 0.12
        _Margin ("Halo: spill width (m)", Float) = 0.35
        _Falloff ("Halo: falloff sharpness", Float) = 2.2
        _Pulse ("Pulse amount", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse speed", Float) = 2.2
        _SpeckColor ("Speck colour (the magic's stars)", Color) = (0.85, 0.76, 1.0, 1)
        _SpeckStrength ("Speck strength", Float) = 1.6
        _Scale ("Speck scale (cells per metre)", Float) = 6
        _Speed ("Speck speed", Float) = 0.6
        _Density ("Speck density", Range(0, 1)) = 0.14
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
            Cull [_Cull]

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
                float _Cull;
                float _Edge;
                float _Fill;
                float4 _SpeckColor;
                float _SpeckStrength;
                float _Scale;
                float _Speed;
                float _Density;
            CBUFFER_END

            // The same drifting specks as Pesky/MagicFlow (world-space cells, one speck in a fraction of them, each on
            // its own little orbit), so the glow reads as the labyrinth's magic spilling out, not a flat light.
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float specks(float2 uv, float t)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float h = hash(g);
                float on = step(1.0 - _Density, h);
                float2 c = 0.5 + 0.33 * float2(sin(t * 1.3 + h * 40.0), cos(t * 0.9 + h * 70.0));
                float d = length(f - c);
                return on * smoothstep(0.2, 0.05, d);
            }

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float2 sizeM : TEXCOORD1; float phase : TEXCOORD2;
                float3 positionOS : TEXCOORD3; float3 normalOS : TEXCOORD4; float3 scaleOS : TEXCOORD5;
                float3 positionWS : TEXCOORD6; float3 normalWS : TEXCOORD7;
            };

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
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                o.scaleOS = float3(o.sizeM, length(unity_ObjectToWorld._m02_m12_m22));
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
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
                else if (_Mode > 1.5)
                {
                    // Unit cube: distance in metres to the nearest edge of the face this fragment is on.
                    float3 n = abs(i.normalOS);
                    float3 toEdge = (0.5 - abs(i.positionOS)) * i.scaleOS;
                    float d = n.x > 0.5 ? min(toEdge.y, toEdge.z) : (n.y > 0.5 ? min(toEdge.x, toEdge.z) : min(toEdge.x, toEdge.y));
                    a = exp(-max(d, 0.0) / max(_Edge, 0.001)) + _Fill;
                }
                else
                {
                    // Bright at the foot, gone at the top, softened at both ends of the edge.
                    float up = 1.0 - i.uv.y;
                    float ends = smoothstep(0.0, 0.08, i.uv.x) * smoothstep(1.0, 0.92, i.uv.x);
                    a = up * up * up * ends;
                }
                float pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed + i.phase);
                // The stars: world-space specks on the surface, drifting, brightest where the glow is.
                float3 n = abs(i.normalWS);
                float2 suv = n.y > max(n.x, n.z) ? i.positionWS.xz : (n.x > n.z ? i.positionWS.zy : i.positionWS.xy);
                float t = _Time.y * _Speed;
                float s = specks(suv * _Scale + float2(t, 0.0), t)
                        + 0.7 * specks(suv * _Scale * 1.7 + float2(-t * 0.6, t * 0.4), t * 1.4);
                float3 glow = _EmissionColor.rgb + _SpeckColor.rgb * saturate(s) * _SpeckStrength;
                float3 c = glow * (_Intensity * a * pulse);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
