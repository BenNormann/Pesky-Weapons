// Pesky - the magical energy that runs the labyrinth: conduits, teleport doors, the magic bridge.
// Medium dark purple with specks of light lavender drifting through it. _Live 0 = dead (a dim, still
// purple-grey), 1 = live. Driven per renderer through a MaterialPropertyBlock by Circuit / CircuitBridge /
// MagicBridge. Unlit on purpose: it glows by being bright, no lights needed. Surface-space UVs picked from
// the dominant world axis, so a wire of any length or orientation shows specks of the same size.
Shader "Pesky/MagicFlow"
{
    Properties
    {
        _BaseColor ("Live colour", Color) = (0.16, 0.06, 0.30, 1)
        _SpeckColor ("Speck colour", Color) = (0.85, 0.76, 1.0, 1)
        _DeadColor ("Dead colour", Color) = (0.11, 0.09, 0.15, 1)
        _Live ("Live (0 dead, 1 live)", Range(0, 1)) = 1
        _Scale ("Speck scale (cells per metre)", Float) = 6
        _Speed ("Speck speed", Float) = 0.6
        _Density ("Speck density", Range(0, 1)) = 0.14
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpeckColor;
                float4 _DeadColor;
                float _Live;
                float _Scale;
                float _Speed;
                float _Density;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // One speck per grid cell, in a fraction of the cells, each drifting on its own little orbit.
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

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = abs(i.normalWS);
                float2 uv = n.y > max(n.x, n.z) ? i.positionWS.xz : (n.x > n.z ? i.positionWS.zy : i.positionWS.xy);
                float t = _Time.y * _Speed;
                float s = specks(uv * _Scale + float2(t, 0.0), t)
                        + 0.7 * specks(uv * _Scale * 1.7 + float2(-t * 0.6, t * 0.4), t * 1.4);
                float3 live = _BaseColor.rgb + _SpeckColor.rgb * saturate(s);
                float3 col = lerp(_DeadColor.rgb, live, saturate(_Live));
                // _Live above 1: a sealed circuit pulsing with power, the whole run surging brighter.
                col += (_BaseColor.rgb + 0.5 * _SpeckColor.rgb) * max(0.0, _Live - 1.0);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // Depth for the opaque pass and for anything that reads the depth texture.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionHCS : SV_POSITION; };
            V vertDepth(A v) { V o; o.positionHCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half fragDepth(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
