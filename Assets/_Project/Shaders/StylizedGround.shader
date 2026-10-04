// Stylized stone-plaza ground: procedural flagstones (running bond) with grout, per-stone tint,
// soft moss and dirt patches, lit by the main light with shadows, fading to dark beyond the arena.
Shader "MobaTrainer/StylizedGround"
{
    Properties
    {
        _StoneA ("Stone A", Color) = (0.60,0.63,0.62,1)
        _StoneB ("Stone B", Color) = (0.50,0.54,0.54,1)
        _Grout ("Grout", Color) = (0.28,0.31,0.28,1)
        _Moss ("Moss", Color) = (0.36,0.50,0.22,1)
        _Dirt ("Dirt", Color) = (0.38,0.33,0.24,1)
        _Outer ("Outside Colour", Color) = (0.07,0.09,0.08,1)
        _TileSize ("Tile Size", Float) = 1.15
        _GroutWidth ("Grout Width", Range(0.005,0.2)) = 0.045
        _MossAmount ("Moss Amount", Range(0,1)) = 0.5
        _Radius ("Arena Radius", Float) = 20
        _EdgeFade ("Edge Fade", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _StoneA; half4 _StoneB; half4 _Grout; half4 _Moss; half4 _Dirt; half4 _Outer;
            float _TileSize; float _GroutWidth; float _MossAmount; float _Radius; float _EdgeFade;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Ground"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.fog = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }
            float fbm(float2 p) { return vnoise(p) * 0.55 + vnoise(p * 2.1 + 7.3) * 0.3 + vnoise(p * 4.3 + 1.7) * 0.15; }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 w = IN.positionWS.xz;
                float2 p = w / _TileSize;
                float row = floor(p.y);
                p.x += fmod(abs(row), 2.0) * 0.5;               // running bond
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 fw = max(fwidth(p), 1e-4);

                // Grout lines with a little wobble so it doesn't look like a grid.
                float wob = (vnoise(w * 3.0) - 0.5) * 0.03;
                float2 d = min(f, 1.0 - f) + wob;
                float edge = min(d.x, d.y);
                float grout = 1.0 - smoothstep(_GroutWidth - fw.x, _GroutWidth + fw.x, edge);
                float bevel = smoothstep(_GroutWidth, _GroutWidth + 0.12, edge);   // darker stone rims

                float h = hash21(cell);
                half3 stone = lerp(_StoneA.rgb, _StoneB.rgb, h);
                stone *= lerp(0.9, 1.06, vnoise(w * 1.7 + h * 10.0));
                stone *= lerp(0.82, 1.0, bevel);

                half3 col = lerp(stone, _Grout.rgb, grout);

                // Moss creeps into the grout first, then covers patches.
                float m = fbm(w * 0.18);
                float mossMask = saturate((m - (1.0 - _MossAmount)) * 3.0);
                mossMask = saturate(mossMask + grout * saturate(m * 1.6 - 0.35));
                half3 moss = _Moss.rgb * lerp(0.8, 1.15, vnoise(w * 4.0));
                col = lerp(col, moss, mossMask * 0.85);

                float dirt = saturate((fbm(w * 0.09 + 31.0) - 0.62) * 3.5);
                col = lerp(col, _Dirt.rgb, dirt * 0.6);

                // Lighting: main light (with shadows) + soft ambient.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light light = GetMainLight(shadowCoord); // brazier glow on the floor is an additive decal, not a light
                float ndl = saturate(light.direction.y);
                half3 lit = col * (light.color * ndl * light.shadowAttenuation) + col * SampleSH(float3(0,1,0));

                // Darken toward and past the arena edge (soft "fog of war" ring).
                float r = length(w);
                float outside = smoothstep(_Radius - 1.0, _Radius + _EdgeFade, r);
                lit = lerp(lit, _Outer.rgb, outside);

                lit = MixFog(lit, IN.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert (float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
