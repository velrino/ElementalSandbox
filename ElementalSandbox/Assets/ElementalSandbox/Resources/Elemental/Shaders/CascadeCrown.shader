// Ported from createCascadeCrownMaterial in src/materials/CascadeBladeMaterial.js.
// Layer 3a — the crown of blades standing out of the heart.
Shader "Elemental/CascadeCrown"
{
    Properties
    {
        _bladeWaist("bladeWaist",Float)=0.22
        _bladeRootPower("bladeRootPower",Float)=0.55
        _bladeTipPower("bladeTipPower",Float)=0.9
        _bladeWidth("bladeWidth",Float)=0.085
        _bladeThick("bladeThick",Float)=0.4
        _bladeEdge("bladeEdge",Float)=0.8
        _bladeBow("bladeBow",Float)=0.04
        _bladeTwist("bladeTwist",Float)=0.3
        _bladeEdgeGlow("bladeEdgeGlow",Float)=0.5
        _bladeEdgePower("bladeEdgePower",Float)=14
        _bladeRim("bladeRim",Float)=0.12
        _bladeRimPower("bladeRimPower",Float)=2.6
        _bladeTipGlow("bladeTipGlow",Float)=0.35
        _bladeTipStart("bladeTipStart",Float)=0.8
        _bladeVein("bladeVein",Float)=0.3
        _bladeVeinScale("bladeVeinScale",Float)=5.5
        _bladeVeinBands("bladeVeinBands",Float)=3
        _bladeVeinSharp("bladeVeinSharp",Float)=3.4
        _bladeHeartBleed("bladeHeartBleed",Float)=0.12
        _bladeHeartReach("bladeHeartReach",Float)=1.3
        _bladeChargeGain("bladeChargeGain",Float)=0.85
        _bladeBurnGlow("bladeBurnGlow",Float)=4
        _bladeRoughness("bladeRoughness",Float)=0.45
        _bladeMetalness("bladeMetalness",Float)=0.05
        _bladeGlow("bladeGlow",Float)=1
        _crownSwell("crownSwell",Float)=0.12
        _colorBladeBody("colorBladeBody",Color)=(0.03,0.14,0.18,1)
        _colorBladeFacet("colorBladeFacet",Color)=(0.12,0.56,0.57,1)
        _colorBladeBodyDeep("colorBladeBodyDeep",Color)=(0.11,0.07,0.25,1)
        _colorBladeFacetDeep("colorBladeFacetDeep",Color)=(0.37,0.29,0.74,1)
        _colorBladeEdge("colorBladeEdge",Color)=(0.25,0.89,1,1)
        _colorBladeVein("colorBladeVein",Color)=(0.66,0.55,1,1)
        _colorBladeHot("colorBladeHot",Color)=(0.56,0.95,1,1)
        _colorHeart("colorHeart",Color)=(0.5,0.97,0.9,1)
        _Centre("Centre",Vector)=(0,0,0,0)
        _Inner("Inner",Float)=0.28
        _Charge("Charge",Float)=0
        _Fade("Fade",Float)=1
        _Collapse("Collapse",Float)=0
        _Pulse("Pulse",Float)=0
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
        // The section closes to a line at both edges, so the far wall is what
        // the camera sees through the near one wherever a blade is edge-on.
        Cull Off


        Pass
        {
            Name "Forward"
            Tags {"LightMode"="UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SourceNoise.hlsl"
            #include "CascadeCrownVertex.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _bladeRoughness, _bladeMetalness;

            struct A { float4 pos:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float4 blade:TEXCOORD1;  // t, a, seed, tone
                float2 state:TEXCOORD2;  // live, fog
            };

            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 world, nrm;
                float t, a, seed, tone, live;
                crownVertex(i.pos.xy,
                            UNITY_ACCESS_INSTANCED_PROP(Crown, _CrownDir),
                            UNITY_ACCESS_INSTANCED_PROP(Crown, _CrownShape),
                            world, nrm, t, a, seed, tone, live);
                o.world = world;
                o.pos = TransformWorldToHClip(world);
                o.blade = float4(t, a, seed, tone);
                o.state = float2(live, ComputeFogFactor(o.pos.z));
                return o;
            }

            half4 frag(V i):SV_Target
            {
                if (i.state.x < 0.004) discard;
                float burn;
                if (crownCull(i.blade.x, burn) < 0.0) discard;

                float3 N = bladeFlatNormal(i.world);
                float3 V = GetWorldSpaceNormalizeViewDir(i.world);
                // Double sided: face the normal at the camera so a blade seen
                // through its own far wall is not lit from behind.
                N *= sign(dot(N, V));

                float3 tint;
                float3 glow = bladeGlow(N, i.world, V, i.blade.x, i.blade.y,
                                        i.blade.z, i.blade.w, 0.0, burn, tint);

                InputData data = (InputData)0;
                data.positionWS = i.world;
                data.normalWS = N;
                data.viewDirectionWS = V;
                data.shadowCoord = TransformWorldToShadowCoord(i.world);
                data.bakedGI = SampleSH(N);
                data.fogCoord = i.state.y;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = tint;
                surface.metallic = _bladeMetalness;
                surface.smoothness = 1.0 - saturate(_bladeRoughness);
                surface.emission = glow;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, i.state.y);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SourceNoise.hlsl"
            #include "CascadeCrownVertex.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 pos:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos:SV_POSITION; float2 state:TEXCOORD0; };

            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 world, nrm;
                float t, a, seed, tone, live;
                crownVertex(i.pos.xy,
                            UNITY_ACCESS_INSTANCED_PROP(Crown, _CrownDir),
                            UNITY_ACCESS_INSTANCED_PROP(Crown, _CrownShape),
                            world, nrm, t, a, seed, tone, live);
                // Shared with the colour pass, or a blade half gone goes on
                // laying a whole shadow.
                o.state = float2(live, t);
                o.pos = TransformWorldToHClip(ApplyShadowBias(world, nrm, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.pos.z = min(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.pos.z = max(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 frag(V i):SV_Target
            {
                if (i.state.x < 0.004) discard;
                float burn;
                if (crownCull(i.state.y, burn) < 0.0) discard;
                return 0;
            }
            ENDHLSL
        }
    }
}
