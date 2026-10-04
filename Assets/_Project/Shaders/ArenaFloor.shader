// Near-black arena floor: anti-aliased world-space grid, radial vignette and a soft rim at the edge.
Shader "MobaTrainer/ArenaFloor"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.055,0.043,0.043,1)
        _GridColor ("Grid", Color) = (0.165,0.125,0.125,1)
        _RimColor ("Rim", Color) = (0.42,0.106,0.443,1)
        _GridSize ("Grid Size", Float) = 2
        _LineWidth ("Line Width", Range(0.001,0.2)) = 0.03
        _Radius ("Arena Radius", Float) = 22
        _RimWidth ("Rim Width", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Floor"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GridColor;
                half4 _RimColor;
                float _GridSize;
                float _LineWidth;
                float _Radius;
                float _RimWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.positionWS.xz / _GridSize;
                float2 fw = max(fwidth(p), 1e-4);
                float2 g = abs(frac(p - 0.5) - 0.5);
                float2 lines = 1.0 - smoothstep(_LineWidth - fw, _LineWidth + fw, g);
                float gridMask = max(lines.x, lines.y);

                float r = length(IN.positionWS.xz) / _Radius;
                float vignette = saturate(1.0 - r * r * 0.55);
                half3 col = lerp(_BaseColor.rgb, _GridColor.rgb, gridMask * vignette);
                col *= lerp(0.55, 1.0, vignette);

                float distToEdge = abs(length(IN.positionWS.xz) - _Radius);
                float rimAA = fwidth(distToEdge);
                float rim = 1.0 - smoothstep(_RimWidth * 0.5 - rimAA, _RimWidth * 0.5 + rimAA, distToEdge);
                float glow = exp(-distToEdge * 1.6) * 0.35 * step(length(IN.positionWS.xz), _Radius);
                col = lerp(col, _RimColor.rgb, saturate(rim + glow));

                float outside = smoothstep(_Radius, _Radius + 0.6, length(IN.positionWS.xz));
                col = lerp(col, _BaseColor.rgb * 0.35, outside);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
