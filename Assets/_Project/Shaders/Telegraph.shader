// Fill-progress telegraph: the area up to _Fill (along uv.y: radius for discs, length for lines,
// distance from apex for cones) is drawn solid, the rest faint, with a bright leading edge.
Shader "MobaTrainer/Telegraph"
{
    Properties
    {
        _Color ("Color", Color) = (1,0.2,0.2,0.4)
        _Fill ("Fill", Range(0,1)) = 0
        _BaseAlpha ("Unfilled Alpha", Range(0,1)) = 0.35
        _EdgeWidth ("Leading Edge Width", Range(0,0.2)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Telegraph"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Fill;
                float _BaseAlpha;
                float _EdgeWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float d = IN.uv.y;
                float aa = max(fwidth(d), 1e-4);
                float filled = 1.0 - smoothstep(_Fill - aa, _Fill + aa, d);
                float edge = (1.0 - smoothstep(0.0, _EdgeWidth, abs(d - _Fill))) * step(0.001, _Fill) * step(_Fill, 0.999);
                half alpha = _Color.a * lerp(_BaseAlpha, 1.0, filled) + edge * 0.6;
                half3 rgb = lerp(_Color.rgb, 1.0.xxx, edge * 0.6);
                return half4(rgb, saturate(alpha) * IN.color.a);
            }
            ENDHLSL
        }
    }
}
