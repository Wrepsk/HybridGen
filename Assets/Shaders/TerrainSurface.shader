// HybridGen/TerrainSurface
// URP terrain shader that blends shore, vegetation, rock, and snow using:
//   - World-space vertex height (normalized by _MaxHeight)
//   - Surface steepness from dot(normal, up)
//   - Per-chunk moisture texture (set via MaterialPropertyBlock)
Shader "HybridGen/TerrainSurface"
{
    Properties
    {
        // Set per-chunk via MaterialPropertyBlock at runtime
        _MoistureMap ("Moisture Map", 2D) = "gray" {}
        _MaxHeight   ("Max Height",  Float) = 40.0

        // Height thresholds (normalized 0-1 relative to _MaxHeight)
        _WaterLevel          ("Water Level",          Range(0,1)) = 0.15
        _SandLevel           ("Shore Top",            Range(0,1)) = 0.22
        _ShoreWidth          ("Shore Width",          Range(0.0,0.2)) = 0.05
        _GrassMaxLevel       ("Grass Max Level",      Range(0,1)) = 0.58
        _RockLevel           ("Snow Level",           Range(0,1)) = 0.75
        _RockSlopeThreshold  ("Steepness Threshold",  Range(0,1)) = 0.50
        _ShoreSlopeThreshold ("Shore Steepness Max",  Range(0,1)) = 0.28
        _BlendWidth          ("Blend Width",          Range(0.005,0.15)) = 0.04

        // Biome colors
        _WaterColor ("Water",     Color) = (0.08, 0.28, 0.62, 1)
        _SandColor  ("Sand",      Color) = (0.76, 0.70, 0.50, 1)
        _GrassColor ("Grass",     Color) = (0.22, 0.52, 0.12, 1)
        _DryColor   ("Dry Grass", Color) = (0.55, 0.50, 0.22, 1)
        _RockColor  ("Rock",      Color) = (0.42, 0.38, 0.34, 1)
        _SnowColor  ("Snow",      Color) = (0.92, 0.96, 1.00, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ------------------------------------------------------------------ //
        // Forward Lit pass                                                    //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Moisture texture + sampler live outside the CBUFFER so they work
            // correctly with MaterialPropertyBlock (no SRP Batcher for chunks).
            TEXTURE2D(_MoistureMap);
            SAMPLER(sampler_MoistureMap);

            CBUFFER_START(UnityPerMaterial)
                float  _MaxHeight;
                float  _WaterLevel;
                float  _SandLevel;
                float  _ShoreWidth;
                float  _GrassMaxLevel;
                float  _RockLevel;
                float  _RockSlopeThreshold;
                float  _ShoreSlopeThreshold;
                float  _BlendWidth;
                float4 _WaterColor;
                float4 _SandColor;
                float4 _GrassColor;
                float4 _DryColor;
                float4 _RockColor;
                float4 _SnowColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.uv          = IN.uv;
                OUT.shadowCoord = GetShadowCoord(posInputs);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                // ------ height (0 = sea level, 1 = maximum peak) -----------
                float heightN = saturate(IN.positionWS.y / _MaxHeight);

                // ------ moisture from per-chunk GPU texture ----------------
                float moisture = SAMPLE_TEXTURE2D(_MoistureMap, sampler_MoistureMap, IN.uv).r;

                // ------ steepness from dot(normal, up) ---------------------
                float3 upDirWS   = float3(0, 1, 0);
                float  upDot     = saturate(dot(normalWS, upDirWS));
                float  steepness = 1.0 - upDot;
                float  shoreTop  = saturate(max(_SandLevel, _WaterLevel + _ShoreWidth));

                float bw = _BlendWidth; // alias for readability

                // ------ biome weight calculation ---------------------------
                // Each weight is non-negative and the bands stay continuous.

                // Water: below _WaterLevel
                float wWater = 1.0 - smoothstep(_WaterLevel - bw, _WaterLevel + bw, heightN);

                // Shore band: sand on flatter coasts, rock on steep coasts.
                float wShore = smoothstep(_WaterLevel - bw, _WaterLevel + bw, heightN)
                             * (1.0 - smoothstep(shoreTop - bw, shoreTop + bw, heightN));
                float shoreFlat = 1.0 - smoothstep(_ShoreSlopeThreshold - bw,
                                                   _ShoreSlopeThreshold + bw,
                                                   steepness);
                float wSand = wShore * shoreFlat;
                float wRockyShore = wShore * (1.0 - shoreFlat);

                // Above the shore band, split flat ground from steep slopes.
                float aboveShore = smoothstep(shoreTop - bw, shoreTop + bw, heightN);
                float steepRock  = smoothstep(_RockSlopeThreshold - bw,
                                              _RockSlopeThreshold + bw,
                                              steepness);
                float flatGround = aboveShore * (1.0 - steepRock);

                // High flat ground turns barren before the snow line.
                float grassAllowed = 1.0 - smoothstep(_GrassMaxLevel - bw,
                                                      _GrassMaxLevel + bw,
                                                      heightN);
                float snowMask = smoothstep(_RockLevel - bw, _RockLevel + bw, heightN);
                float wSnow = flatGround * snowMask;
                float wVeg  = flatGround * (1.0 - snowMask) * grassAllowed;
                float wHighBare = flatGround * (1.0 - snowMask) * (1.0 - grassAllowed);

                // Rock covers steep terrain, rocky coasts, and high barren ground.
                float wRock = wRockyShore + aboveShore * steepRock + wHighBare;

                // Split vegetation by moisture: grass vs dry/savanna
                float wGrass = wVeg * moisture;
                float wDry   = wVeg * (1.0 - moisture);

                // ------ composite albedo -----------------------------------
                float4 albedo = wWater * _WaterColor
                              + wSand  * _SandColor
                              + wGrass * _GrassColor
                              + wDry   * _DryColor
                              + wRock  * _RockColor
                              + wSnow  * _SnowColor;

                // ------ URP lighting (diffuse + ambient + shadows) ---------
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float  NdotL     = saturate(dot(normalWS, mainLight.direction));
                float3 ambient   = SampleSH(normalWS);
                float3 lighting  = mainLight.color * mainLight.shadowAttenuation * NdotL + ambient;

                float3 finalColor = MixFog(albedo.rgb * lighting, IN.fogFactor);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        // Shadow Caster pass                                                  //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest  LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        // Depth Only pass (needed for SSAO, depth pre-pass, etc.)            //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
