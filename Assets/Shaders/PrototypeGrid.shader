Shader "GameCombat/Prototype Grid"
{
    Properties
    {
        _BaseColor("Surface", Color) = (0.25,0.32,0.38,1)
        _BaseMap("Base map", 2D) = "white" {}
        _GridColor("Meter lines", Color) = (0.5,0.62,0.67,1)
        _MajorColor("Four meter lines", Color) = (0.65,0.8,0.84,1)
        _GridSize("Grid spacing (meters)", Float) = 1
        _LineWidth("Line width (meters)", Float) = 0.018
        _Checker("Checker contrast", Range(0,0.2)) = 0.035
        _Cutoff("Alpha cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _GridColor, _MajorColor;
                float4 _BaseMap_ST;
                float _GridSize, _LineWidth, _Checker, _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            float Grid(float2 p, float width)
            {
                float2 footprint = max(fwidth(p), 0.0001);
                float2 distanceToLine = abs(frac(p + 0.5) - 0.5);
                float2 coverage = 1 - smoothstep(width, width + footprint, distanceToLine);
                coverage *= saturate(1 - footprint * 2);
                return max(coverage.x, coverage.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float3 a = abs(n);
                // Dominant-axis projection keeps every cube face at the same meter scale.
                float2 uv = a.y >= max(a.x,a.z) ? i.positionWS.xz : a.x > a.z ? i.positionWS.zy : i.positionWS.xy;
                uv /= max(0.01, _GridSize);
                float minor = Grid(uv, _LineWidth / max(0.01, _GridSize));
                float major = Grid(uv / 4, _LineWidth * 0.5 / max(0.01, _GridSize));
                float checker = fmod(abs(floor(uv.x) + floor(uv.y)), 2);
                half3 color = _BaseColor.rgb * (1 - checker * _Checker);
                color = lerp(color, _GridColor.rgb, minor * 0.65);
                color = lerp(color, _MajorColor.rgb, major * 0.8);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination = half3(0.5,0.54,0.6) + light.color * saturate(dot(n, light.direction)) * light.shadowAttenuation * 0.55;
                return half4(MixFog(color * illumination, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
