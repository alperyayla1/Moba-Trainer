// Additive energy trail: hot white core along the centre line, soft edges across the width,
// colour and fade come from the TrailRenderer's vertex colours.
Shader "MobaTrainer/AdditiveTrail"
{
    Properties
    {
        _Intensity ("Intensity", Range(0.5, 4)) = 1.8
        _CoreWidth ("Core Width", Range(0.05, 0.6)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Trail"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _CoreWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

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
                float across = abs(IN.uv.y * 2.0 - 1.0);
                float soft = pow(saturate(1.0 - across), 1.6);
                float core = saturate(1.0 - across / _CoreWidth);
                half3 col = lerp(IN.color.rgb, half3(1, 1, 1), core * 0.85) * _Intensity;
                return half4(col, IN.color.a * soft);
            }
            ENDHLSL
        }
    }
}
