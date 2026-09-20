// Ported from createLeafMaterial in src/materials/GrowthVineMaterial.js.
// Layer 3 — the foliage. A green sheet that the light goes *through*.
Shader "Elemental/GrowthLeaf"
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
        _witherRise("witherRise",Float)=0.58
        _witherScale("witherScale",Float)=3.2
        _witherEdge("witherEdge",Float)=0.1
        _witherEdgeGlow("witherEdgeGlow",Float)=2.4
        _leafStart("leafStart",Float)=0.15
        _leafEnd("leafEnd",Float)=0.97
        _leafSize("leafSize",Float)=0.32
        _leafSizeJitter("leafSizeJitter",Float)=0.6
        _leafAspect("leafAspect",Float)=0.42
        _leafBias("leafBias",Float)=0.72
        _leafPoint("leafPoint",Float)=0.78
        _leafPitch("leafPitch",Float)=0.55
        _leafPitchJitter("leafPitchJitter",Float)=0.95
        _leafDroop("leafDroop",Float)=0.35
        _leafCup("leafCup",Float)=0.22
        _leafOpen("leafOpen",Float)=0.12
        _leafFlutter("leafFlutter",Float)=0.09
        _leafFlutterSpeed("leafFlutterSpeed",Float)=1.7
        _leafVeins("leafVeins",Float)=7
        _leafVeinWidth("leafVeinWidth",Float)=0.09
        _leafVeinSkew("leafVeinSkew",Float)=0.55
        _leafRibWidth("leafRibWidth",Float)=0.055
        _leafVeinGlow("leafVeinGlow",Float)=0.6
        _leafTranslucency("leafTranslucency",Float)=0.85
        _leafSheen("leafSheen",Float)=0.14
        _leafMottle("leafMottle",Float)=0.3
        _leafRoughness("leafRoughness",Float)=0.6
        _leafGlow("leafGlow",Float)=1
        _colorLeaf("colorLeaf",Color)=(0.31,0.61,0.27,1)
        _colorLeafTip("colorLeafTip",Color)=(0.66,0.91,0.42,1)
        _colorLeafDeep("colorLeafDeep",Color)=(0.11,0.27,0.15,1)
        _colorLeafVein("colorLeafVein",Color)=(0.62,1,0.71,1)
        _Centre("Centre",Vector)=(0,0,0,0)
        _Seed("Seed",Float)=0
        _Grow("Grow",Float)=0
        _Wither("Wither",Float)=0
        _Radius("Radius",Float)=4
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+6"}
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

            float _leafStart, _leafEnd, _leafSize, _leafSizeJitter, _leafAspect, _leafBias, _leafPoint;
            float _leafPitch, _leafPitchJitter, _leafDroop, _leafCup, _leafOpen, _leafFlutter, _leafFlutterSpeed;
            float _leafVeins, _leafVeinWidth, _leafVeinSkew, _leafRibWidth, _leafVeinGlow;
            float _leafTranslucency, _leafSheen, _leafMottle, _leafRoughness, _leafGlow;
            float4 _colorLeaf, _colorLeafTip, _colorLeafDeep, _colorLeafVein;
            float _GlobalGlow;

            // Half-width of the blade at u, in metres, for a leaf len long.
            float leafHalfWidth(float u, float len)
            {
                float shaped = pow(saturate(u), max(_leafBias, 0.05));
                return len * _leafAspect * pow(sin(shaped * GPI), max(_leafPoint, 0.05));
            }

            // One point on one blade. Evaluated three times per vertex so the
            // normal can be a proper cross product of the two surface
            // derivatives — a leaf that is drooping, cupped *and* fluttering
            // has no normal worth guessing at.
            float3 leafPoint(float u, float v, float3 base, float3 axis, float3 lateral, float3 up, float len)
            {
                float halfWidth = leafHalfWidth(u, len);
                // The blade sags along its own length, and the sag is what
                // separates foliage from a fan of blades.
                float sag = _leafDroop * len * u * u;
                float3 p = base + axis * (len * u) - up * sag;
                p += lateral * (v * halfWidth);
                // ... and it channels across, edges above the midrib.
                p += up * (_leafCup * halfWidth * v * v);
                return p;
            }

            struct A { float4 pos:POSITION; float2 leaf:TEXCOORD0; };
            struct V
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float3 normal:TEXCOORD1;
                float4 leaf:TEXCOORD2;  // u, v, seed, open
                float fog:TEXCOORD3;
            };

            V vert(A i)
            {
                V o;
                float id = i.leaf.x;
                float host = fmod(id, max(_vines, 1.0));
                float d0 = hash11(id * 1.37 + _Seed * 2.11);
                float d1 = hash11(id * 2.71 + _Seed * 3.37);
                float d2 = hash11(id * 3.91 + _Seed * 5.93);
                float d3 = hash11(id * 5.13 + _Seed * 7.71);

                float t = lerp(_leafStart, _leafEnd, d0);
                float grow = vineGrowth(host);

                // A leaf unfurls once the growth front has gone *past* where it
                // is clipped on — the whole reason the foliage reads as having
                // grown out of the stem rather than dressed onto it afterwards.
                float open = smoothstep(t, t + max(_leafOpen, 1e-3), grow);
                // And it curls up and drops as the nest withers, tip-first.
                open *= 1.0 - smoothstep(0.0, 1.0, saturate((_Wither - (1.0 - t) * 0.55) * 2.2));

                float3 anchor = vinePoint(host, t);
                float3 tangent, n1, n2;
                vineFrame(host, t, anchor, tangent, n1, n2);

                float roll = d1 * GTAU;
                float3 outward = n1 * cos(roll) + n2 * sin(roll);
                float3 base = anchor + outward * vineRadius(host, t, grow) * 0.85;

                // The stalk leaves the stem sideways and forward: pitch is how
                // far it is swung from square toward the tip of its tendril.
                float pitch = _leafPitch + (d3 - 0.5) * _leafPitchJitter;
                pitch += sin(_SandboxTime * _leafFlutterSpeed + d3 * GTAU) * _leafFlutter;

                float3 axis = normalize(outward * cos(pitch) + tangent * sin(pitch));
                float3 lateral = cross(axis, outward);
                if (dot(lateral, lateral) < 1e-10) lateral = cross(axis, float3(0, 1, 0));
                lateral = normalize(lateral);
                float3 up = normalize(cross(lateral, axis));

                float len = _leafSize * (1.0 - _leafSizeJitter * 0.5 + _leafSizeJitter * d2) * open;

                float u = i.pos.x, v = i.pos.y;
                float3 world = leafPoint(u, v, base, axis, lateral, up, len);

                // Two neighbours, mirrored at the edges so the tip and the rim
                // still have something to look at.
                float du = u < 0.98 ? 0.02 : -0.02;
                float dv = v < 0.98 ? 0.02 : -0.02;
                float3 pu = leafPoint(u + du, v, base, axis, lateral, up, len);
                float3 pv = leafPoint(u, v + dv, base, axis, lateral, up, len);
                float3 nrm = cross((pu - world) * sign(du), (pv - world) * sign(dv));
                nrm = dot(nrm, nrm) > 1e-14 ? normalize(nrm) : up;

                o.world = world;
                o.normal = nrm;
                o.pos = TransformWorldToHClip(world);
                o.leaf = float4(u, v, d2, open);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i):SV_Target
            {
                if (i.leaf.w < 0.004) discard;

                float u = saturate(i.leaf.x), v = i.leaf.y;
                float3 N = normalize(i.normal);
                float3 V = GetWorldSpaceNormalizeViewDir(i.world);
                Light key = GetMainLight();
                float3 L = normalize(key.direction);

                /* ---- the blade ---- */
                float mottle = fbm3(i.world * 7.0 + i.leaf.z * 41.0) * 0.5 + 0.5;
                float3 blade = lerp(_colorLeafDeep.rgb, _colorLeaf.rgb, smoothstep(0.0, 0.55, u));
                blade = lerp(blade, _colorLeafTip.rgb, smoothstep(0.55, 1.0, u));
                blade *= 1.0 + (mottle - 0.5) * _leafMottle;

                /* ---- the venation ---- */
                // The midrib runs the length of the blade and thins with it.
                float rib = 1.0 - smoothstep(0.0, max(_leafRibWidth, 1e-3), abs(v));
                // The laterals fan: skewing the band coordinate by |v| is what
                // tips them away from square and toward the tip.
                float lat = abs(frac(u * _leafVeins + abs(v) * _leafVeinSkew) - 0.5) * 2.0;
                lat = 1.0 - smoothstep(0.0, max(_leafVeinWidth, 1e-3), lat);
                // Nothing runs off the edge of the blade or past the tip.
                lat *= smoothstep(0.0, 0.12, u) * (1.0 - smoothstep(0.82, 1.0, u));
                lat *= 1.0 - smoothstep(0.55, 0.95, abs(v));

                float veins = clamp(rib + lat * 0.8, 0.0, 1.5);

                /* ---- light through it ---- */
                // Wrapped rather than clamped: the sheet is thin, so what is
                // behind it reaches the camera. The whole of the subsurface read.
                float back = saturate(dot(-N, L));
                float through = pow(back, 2.2) * _leafTranslucency;
                // A little specular sheen off the cuticle, so a wet leaf
                // catches the sky.
                float sheen = pow(saturate(dot(reflect(-V, N), L)), 24.0);

                float3 glow = _colorLeafVein.rgb * veins * _leafVeinGlow;
                glow += _colorLeafTip.rgb * through;
                glow += sheen * _leafSheen;
                glow *= _leafGlow * _GlobalGlow * saturate(i.leaf.w);
                glow /= 1.0 + glow * 0.25;

                InputData data = (InputData)0;
                data.positionWS = i.world;
                data.normalWS = N * sign(dot(N, V));
                data.viewDirectionWS = V;
                data.shadowCoord = TransformWorldToShadowCoord(i.world);
                data.bakedGI = SampleSH(data.normalWS);
                data.fogCoord = i.fog;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = blade;
                surface.metallic = 0;
                surface.smoothness = 1.0 - saturate(_leafRoughness);
                surface.emission = glow;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, i.fog);
                return color;
            }
            ENDHLSL
        }
    }
}
