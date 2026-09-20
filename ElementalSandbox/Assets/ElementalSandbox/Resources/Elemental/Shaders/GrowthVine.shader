// Ported from createVineMaterial in src/materials/GrowthVineMaterial.js.
// Layer 2 — the tendrils. Bark: dark, fibrous wood with something alive
// running under it.
Shader "Elemental/GrowthVine"
{
    Properties
    {
        _vines("vines",Float)=15
        _vineStagger("vineStagger",Float)=0.38
        _vineSeat("vineSeat",Float)=0.74
        _vineSpread("vineSpread",Float)=0.75
        _vineHeight("vineHeight",Float)=3.2
        _vineHeightJitter("vineHeightJitter",Float)=0.6
        _vineRise("vineRise",Float)=0.74
        _vineBelly("vineBelly",Float)=0.36
        _vineLean("vineLean",Float)=0.55
        _vineTwist("vineTwist",Float)=0.12
        _vineCurlAt("vineCurlAt",Float)=0.78
        _vineCurlTurns("vineCurlTurns",Float)=0.45
        _vineCurlPinch("vineCurlPinch",Float)=0.42
        _vineCurlLift("vineCurlLift",Float)=0.1
        _vineWander("vineWander",Float)=0.75
        _vineWanderScale("vineWanderScale",Float)=2.2
        _vineSway("vineSway",Float)=0.085
        _vineSwaySpeed("vineSwaySpeed",Float)=0.75
        _vineThick("vineThick",Float)=0.085
        _vineTaper("vineTaper",Float)=0.24
        _vineKnots("vineKnots",Float)=0.32
        _vineKnotScale("vineKnotScale",Float)=9
        _vineRim("vineRim",Float)=0.22
        _vineRimPower("vineRimPower",Float)=2.6
        _vineGlow("vineGlow",Float)=1
        _barkScale("barkScale",Float)=5.5
        _barkContrast("barkContrast",Float)=1.6
        _barkFibre("barkFibre",Float)=0.55
        _barkFibreBands("barkFibreBands",Float)=3
        _barkFibreScale("barkFibreScale",Float)=26
        _barkRoughness("barkRoughness",Float)=0.92
        _seamWidth("seamWidth",Float)=0.05
        _seamBands("seamBands",Float)=2.4
        _seamScale("seamScale",Float)=5
        _seamFlow("seamFlow",Float)=0.35
        _seamGlow("seamGlow",Float)=0.45
        _sapPulse("sapPulse",Float)=1.4
        _sapSpeed("sapSpeed",Float)=0.34
        _sapWidth("sapWidth",Float)=0.12
        _frontGlow("frontGlow",Float)=5
        _frontWidth("frontWidth",Float)=0.09
        _witherRise("witherRise",Float)=0.58
        _witherScale("witherScale",Float)=3.2
        _witherEdge("witherEdge",Float)=0.1
        _witherEdgeGlow("witherEdgeGlow",Float)=2.4
        _colorBark("colorBark",Color)=(0.2,0.16,0.11,1)
        _colorBarkLight("colorBarkLight",Color)=(0.42,0.35,0.25,1)
        _colorSeam("colorSeam",Color)=(0.34,0.88,0.55,1)
        _colorSeamCore("colorSeamCore",Color)=(0.75,1,0.85,1)
        _colorFront("colorFront",Color)=(0.85,1,0.69,1)
        _colorWither("colorWither",Color)=(1,0.6,0.24,1)
        _Centre("Centre",Vector)=(0,0,0,0)
        _Seed("Seed",Float)=0
        _Grow("Grow",Float)=0
        _Wither("Wither",Float)=0
        _Radius("Radius",Float)=4
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5"}
        Cull Off

        Pass
        {
            Name "Forward"
            Tags {"LightMode"="UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ElementalCommon.hlsl"
            #include "SourceNoise.hlsl"
            #include "GrowthPath.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _vineRim, _vineRimPower, _vineGlow;
            float _barkScale, _barkContrast, _barkFibre, _barkFibreBands, _barkFibreScale, _barkRoughness;
            float _seamWidth, _seamBands, _seamScale, _seamFlow, _seamGlow;
            float _sapPulse, _sapSpeed, _sapWidth, _frontGlow, _frontWidth;
            float4 _colorBark, _colorBarkLight, _colorSeam, _colorSeamCore, _colorFront, _colorWither;
            float _GlobalGlow;

            struct A { float4 pos:POSITION; float2 vine:TEXCOORD0; };
            struct V
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float3 normal:TEXCOORD1;
                float4 growth:TEXCOORD2;  // t, front, seed, angle
                float fog:TEXCOORD3;
            };

            V vert(A i)
            {
                V o;
                float vine = i.vine.x;
                float grow = vineGrowth(vine);
                // The tube is drawn in the *grown* part of the stem, so the
                // whole buffer compresses into it: a tendril extends rather
                // than fading in, and keeps every one of its samples while it does.
                float t = i.pos.x * grow;

                float3 here = vinePoint(vine, t);
                float3 tangent, n1, n2;
                vineFrame(vine, t, here, tangent, n1, n2);

                float angle = i.pos.y * GTAU;
                float3 nrm = n1 * cos(angle) + n2 * sin(angle);
                float3 world = here + nrm * vineRadius(vine, t, grow);

                o.world = world;
                o.normal = nrm;
                o.pos = TransformWorldToHClip(world);
                o.growth = float4(t, grow, vineDice(vine).w, i.pos.y);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i):SV_Target
            {
                float witherEdge;
                if (growthWither(i.world, i.growth.x, i.growth.z, witherEdge) < 0.0) discard;

                float3 N = normalize(i.normal);
                float3 V = GetWorldSpaceNormalizeViewDir(i.world);
                // Absolute rather than clamped: on a closed tube seen edge-on
                // the far wall is still wood, and clamping paints it flat black.
                float ndv = abs(dot(N, V));

                /* ---- the wood ---- */
                float grain = fbm3(i.world * _barkScale + i.growth.z * 23.0) * 0.5 + 0.5;
                grain = saturate((grain - 0.5) * _barkContrast + 0.5);
                // Fibres, in the stem's own frame so they run the length of it.
                float fib = ridged(float3(i.growth.w * _barkFibreBands, i.growth.x * _barkFibreScale, i.growth.z * 7.0), 4);
                fib = saturate(fib * 0.55);

                float3 bark = lerp(_colorBark.rgb, _colorBarkLight.rgb, grain);
                bark *= lerp(1.0 - _barkFibre * 0.6, 1.0 + _barkFibre * 0.25, fib);
                // Cheap curvature: the far side of a round stem is in its own
                // shadow long before the sun stops reaching it.
                bark *= lerp(0.55, 1.1, ndv);
                // Charred back along the burn. Wood that is being eaten away
                // goes *dark* first and glows second; adding light without
                // taking the albedo out bleaches the stump, which reads as bone.
                bark *= lerp(1.0, 0.18, saturate(witherEdge));

                /* ---- what is running inside it ---- */
                float f = fbm3(float3(i.growth.w * _seamBands,
                                      i.growth.x * _seamScale - _SandboxTime * _seamFlow,
                                      i.growth.z * 13.0));
                float d = abs(f);
                float seam = 1.0 - smoothstep(_seamWidth * 0.35, _seamWidth, d);
                float core = 1.0 - smoothstep(0.0, _seamWidth * 0.35, d);

                // Sap: a bright band climbing the stem, on its own phase per
                // tendril, so the nest pulses out of step with itself.
                float wave = frac(_SandboxTime * _sapSpeed + i.growth.z);
                float sap = exp(-pow((i.growth.x - wave) / max(_sapWidth, 1e-3), 2.0)) * _sapPulse;

                float3 glow = lerp(_colorSeam.rgb, _colorSeamCore.rgb, saturate(core));
                glow *= seam * _seamGlow * (1.0 + sap);

                /* ---- the growing tip ---- */
                // Bright while the front is still moving and all but out once
                // it stops: this is the bud, and a bud that keeps burning after
                // the stem has finished is a lamp on a stick.
                float behind = i.growth.y - i.growth.x;
                float front = 1.0 - smoothstep(0.0, max(_frontWidth, 1e-3), behind);
                front *= lerp(0.18, 1.0, 1.0 - smoothstep(0.9, 1.0, i.growth.y));
                glow += _colorFront.rgb * front * _frontGlow;

                /* ---- the sheath of light around the silhouette ---- */
                glow += _colorSeam.rgb * pow(1.0 - ndv, max(_vineRimPower, 0.05)) * _vineRim;

                /* ---- and the line the wither leaves as it eats back ---- */
                // Only the last of the band is hot: an ember is a *line*.
                float ember = pow(saturate(witherEdge), 3.0);
                glow += _colorWither.rgb * ember * _witherEdgeGlow;

                glow *= _vineGlow * _GlobalGlow;
                glow /= 1.0 + glow * 0.2;

                InputData data = (InputData)0;
                data.positionWS = i.world;
                data.normalWS = N;
                data.viewDirectionWS = V;
                data.shadowCoord = TransformWorldToShadowCoord(i.world);
                data.bakedGI = SampleSH(N);
                data.fogCoord = i.fog;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = bark;
                surface.metallic = 0;
                surface.smoothness = 1.0 - saturate(_barkRoughness);
                surface.emission = glow;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, i.fog);
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ElementalCommon.hlsl"
            #include "SourceNoise.hlsl"
            #include "GrowthPath.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 pos:POSITION; float2 vine:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float2 growth:TEXCOORD1; };

            V vert(A i)
            {
                V o;
                float vine = i.vine.x;
                float grow = vineGrowth(vine);
                float t = i.pos.x * grow;
                float3 here = vinePoint(vine, t);
                float3 tangent, n1, n2;
                vineFrame(vine, t, here, tangent, n1, n2);
                float angle = i.pos.y * GTAU;
                float3 nrm = n1 * cos(angle) + n2 * sin(angle);
                float3 world = here + nrm * vineRadius(vine, t, grow);
                o.world = world;
                o.growth = float2(t, vineDice(vine).w);
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
                // Shared with the colour pass, or a withered stem goes on
                // laying a whole shadow.
                float edge;
                if (growthWither(i.world, i.growth.x, i.growth.y, edge) < 0.0) discard;
                return 0;
            }
            ENDHLSL
        }
    }
}
