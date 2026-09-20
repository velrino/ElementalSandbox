// Ported from createCascadeVolleyMaterial in src/materials/CascadeBladeMaterial.js.
// Layer 3b — the blades in the air. Same tube on the same grid, told where it
// is going instead of which way it points.
Shader "Elemental/CascadeVolley"
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
        _throwStrike("throwStrike",Float)=0.34
        _throwHold("throwHold",Float)=0.5
        _throwOverrun("throwOverrun",Float)=2.4
        _throwCurve("throwCurve",Float)=0.22
        _throwLoft("throwLoft",Float)=0.05
        _throwLength("throwLength",Float)=1.45
        _throwSmear("throwSmear",Float)=0.5
        _throwSpin("throwSpin",Float)=1.4
        _throwHeat("throwHeat",Float)=1.2
        _throwFlare("throwFlare",Float)=2.6
        _colorBladeBody("colorBladeBody",Color)=(0.03,0.14,0.18,1)
        _colorBladeFacet("colorBladeFacet",Color)=(0.12,0.56,0.57,1)
        _colorBladeBodyDeep("colorBladeBodyDeep",Color)=(0.11,0.07,0.25,1)
        _colorBladeFacetDeep("colorBladeFacetDeep",Color)=(0.37,0.29,0.74,1)
        _colorBladeEdge("colorBladeEdge",Color)=(0.25,0.89,1,1)
        _colorBladeVein("colorBladeVein",Color)=(0.66,0.55,1,1)
        _colorBladeHot("colorBladeHot",Color)=(0.56,0.95,1,1)
        _colorHeart("colorHeart",Color)=(0.5,0.97,0.9,1)
        _Centre("Centre",Vector)=(0,0,0,0)
        _Charge("Charge",Float)=0
        _Fade("Fade",Float)=1
        _Collapse("Collapse",Float)=0
        _Pulse("Pulse",Float)=0
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+11"}
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
            #include "CascadeBlade.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _throwStrike, _throwHold, _throwOverrun, _throwCurve, _throwLoft;
            float _throwLength, _throwSmear, _throwSpin, _throwHeat, _throwFlare;
            float _bladeRoughness, _bladeMetalness;

            UNITY_INSTANCING_BUFFER_START(Volley)
                UNITY_DEFINE_INSTANCED_PROP(float4, _VolleyFrom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _VolleyTo)
                UNITY_DEFINE_INSTANCED_PROP(float4, _VolleyState)
            UNITY_INSTANCING_BUFFER_END(Volley)

            float3 volleyPath(float3 a, float3 b, float3 ctrl, float q)
            {
                float m = 1.0 - q;
                return m * m * a + 2.0 * m * q * ctrl + q * q * b;
            }

            // Eaten from the root forward, which is the opposite end to the
            // crown's collapse and the right one here: what is left last is the
            // point, still travelling, so the blade reads as spending itself
            // into the cut rather than as a prop being switched off.
            float volleyCull(float t, float spend, out float burn)
            {
                burn = (1.0 - smoothstep(0.0, 0.12, t - spend)) * step(0.001, spend);
                return t - spend;
            }

            struct A { float4 pos:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float4 blade:TEXCOORD1;  // t, a, seed, tone
                float4 state:TEXCOORD2;  // live, spend, heat, fog
            };

            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;

                float3 from = UNITY_ACCESS_INSTANCED_PROP(Volley, _VolleyFrom).xyz;
                float3 to   = UNITY_ACCESS_INSTANCED_PROP(Volley, _VolleyTo).xyz;
                float4 st   = UNITY_ACCESS_INSTANCED_PROP(Volley, _VolleyState);

                float life = st.x, seed = st.y, curve = st.z, live = st.w;

                // The path is a quadratic through a control point pushed off
                // the line, so a flurry fans out and converges rather than
                // arriving as a bundle of parallel rods.
                float3 delta = to - from;
                float span = max(length(delta), 0.05);
                float3 dir = delta / span;
                float3 side = normalize(cross(dir, float3(0, 1, 0)) + float3(1e-4, 0, 0));
                float3 lift = cross(side, dir);
                float3 ctrl = lerp(from, to, 0.5) + side * (curve * _throwCurve * span) + lift * (_throwLoft * span);

                // Arrival: eased so it *lands* rather than stopping dead.
                float q = saturate(life / max(_throwStrike, 1e-3));
                q = 1.0 - pow(1.0 - q, 2.4);

                float3 here   = volleyPath(from, to, ctrl, q);
                float3 ahead  = volleyPath(from, to, ctrl, min(q + 0.02, 1.0));
                float3 behind = volleyPath(from, to, ctrl, max(q - 0.02, 0.0));
                float3 axis = normalize(ahead - behind + dir * 1e-4);

                // ... and out the other side. The overrun is what makes it a
                // cut rather than an impalement.
                float past = saturate((life - _throwStrike) / max(1.0 - _throwStrike, 1e-3));
                here += axis * (past * _throwOverrun);

                // Longer while it is fast: a smear the geometry does itself, so
                // it survives a paused frame where motion blur would not.
                float len = _throwLength * (1.0 + _throwSmear * (1.0 - saturate(life / max(_throwStrike, 1e-3))));
                float spend = saturate((life - _throwHold) / max(1.0 - _throwHold, 1e-3));
                len *= lerp(1.0, 0.72, spend);

                float roll = seed * 17.0 + life * _throwSpin * CTAU;
                // Rooted *behind* the path point, so the point of the blade is
                // the thing that arrives and the body is cut on the frame it does.
                float3 root = here - axis * len;

                float3 nrm;
                float t = i.pos.x, a = i.pos.y;
                float3 world = bladeVertex(root, axis, roll, len * live, t, a, nrm);

                o.world = world;
                o.pos = TransformWorldToHClip(world);
                o.blade = float4(t, a, seed, frac(seed * 2.7));
                // Hot the whole way and incandescent for the two frames it is
                // going through something.
                float heat = _throwHeat + _throwFlare * exp(-pow((life - _throwStrike) / 0.07, 2.0));
                o.state = float4(live, spend, heat, ComputeFogFactor(o.pos.z));
                return o;
            }

            half4 frag(V i):SV_Target
            {
                if (i.state.x < 0.004) discard;
                float burn;
                if (volleyCull(i.blade.x, i.state.y, burn) < 0.0) discard;

                float3 N = bladeFlatNormal(i.world);
                float3 V = GetWorldSpaceNormalizeViewDir(i.world);
                N *= sign(dot(N, V));

                float3 tint;
                float3 glow = bladeGlow(N, i.world, V, i.blade.x, i.blade.y,
                                        i.blade.z, i.blade.w, i.state.z, burn, tint);

                InputData data = (InputData)0;
                data.positionWS = i.world;
                data.normalWS = N;
                data.viewDirectionWS = V;
                data.shadowCoord = TransformWorldToShadowCoord(i.world);
                data.bakedGI = SampleSH(N);
                data.fogCoord = i.state.w;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = tint;
                surface.metallic = _bladeMetalness;
                surface.smoothness = 1.0 - saturate(_bladeRoughness);
                surface.emission = glow;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, i.state.w);
                return color;
            }
            ENDHLSL
        }
    }
}
