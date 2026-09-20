// Ported from src/materials/CascadeWispMaterial.js.
// Layer 2 — the wisps climbing out of the mark. A strip rather than a tube,
// because a wisp has no volume: it is billboarded about its own spine in the
// vertex stage, so it presents its full width from every angle and never shows
// an edge-on seam.
Shader "Elemental/CascadeWisp"
{
    Properties
    {
        _wisps("wisps",Float)=20
        _wispSeat("wispSeat",Float)=0.57
        _wispSeatJitter("wispSeatJitter",Float)=0.44
        _wispSpread("wispSpread",Float)=1.11
        _wispHeight("wispHeight",Float)=4.4
        _wispHeightJitter("wispHeightJitter",Float)=0.28
        _wispRise("wispRise",Float)=0.15
        _wispLength("wispLength",Float)=0.86
        _wispWander("wispWander",Float)=0.13
        _wispWanderScale("wispWanderScale",Float)=1.7
        _wispWanderSpeed("wispWanderSpeed",Float)=0.32
        _wispSwirl("wispSwirl",Float)=-1.25
        _wispDraw("wispDraw",Float)=0
        _wispDrawAt("wispDrawAt",Float)=0.43
        _wispWidth("wispWidth",Float)=0.055
        _wispWidthBias("wispWidthBias",Float)=0.45
        _wispIntensity("wispIntensity",Float)=1.44
        _wispSoftEdge("wispSoftEdge",Float)=3.85
        _wispErode("wispErode",Float)=0.72
        _wispErodeScale("wispErodeScale",Float)=3.1
        _wispErodeSpeed("wispErodeSpeed",Float)=0.87
        _wispHeadFade("wispHeadFade",Float)=0.01
        _wispTailFade("wispTailFade",Float)=0.12
        _wispSoftFade("wispSoftFade",Float)=0
        _wispOpacity("wispOpacity",Float)=0.85
        _wispGlow("wispGlow",Float)=1
        _colorWispRoot("colorWispRoot",Color)=(0.37,0.92,0.85,1)
        _colorWispBody("colorWispBody",Color)=(0.17,0.78,0.74,1)
        _colorWispTip("colorWispTip",Color)=(0.07,0.35,0.42,1)
        _Centre("Centre",Vector)=(0,0,0,0)
        _Radius("Radius",Float)=4
        _Grow("Grow",Float)=1
        _Seed("Seed",Float)=0
        _Charge("Charge",Float)=0
        _Fade("Fade",Float)=1
        _Pulse("Pulse",Float)=0
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ElementalCommon.hlsl"
            #include "SourceNoise.hlsl"

            #define WTAU 6.283185307179586
            #define WPI  3.141592653589793

            float _wisps, _wispSeat, _wispSeatJitter, _wispSpread;
            float _wispHeight, _wispHeightJitter, _wispRise, _wispLength;
            float _wispWander, _wispWanderScale, _wispWanderSpeed, _wispSwirl;
            float _wispDraw, _wispDrawAt, _wispWidth, _wispWidthBias;
            float _wispIntensity, _wispSoftEdge, _wispErode, _wispErodeScale, _wispErodeSpeed;
            float _wispHeadFade, _wispTailFade, _wispSoftFade, _wispOpacity, _wispGlow;
            float4 _colorWispRoot, _colorWispBody, _colorWispTip;
            float4 _Centre;
            float _Radius, _Grow, _Seed, _Charge, _Fade, _Pulse, _GlobalGlow;

            // The spine, as a function of how far up the climb it is. Every
            // argument is per-wisp dice; nothing is stored. Called three times
            // per vertex so the tangent can be taken by finite difference — a
            // wisp that billboards about a straight vertical instead of its own
            // curve twists visibly wherever the S is steepest.
            float3 wispSpine(float c, float bearing, float seat, float height, float dice)
            {
                float k = saturate(c);

                // Drawn in toward the axis over the top stretch of the climb:
                // this is the term that ties the layer to the crown.
                float pull = smoothstep(clamp(_wispDrawAt, 0.0, 0.98), 1.0, k);
                float radius = seat * lerp(1.0, saturate(_wispDraw), pull);
                float turn = bearing + k * _wispSwirl;

                float3 here = _Centre.xyz + float3(cos(turn), 0, sin(turn)) * radius;
                here.y += k * height;

                // Two octaves of wander, the second at a third of the amplitude
                // and three times the rate — one sine is a wave, two are a wisp.
                float t = _SandboxTime * _wispWanderSpeed;
                float amp = _wispWander * smoothstep(0.0, 0.25, k);
                here.x += snoise(float3(k * _wispWanderScale, t, dice * 11.0)) * amp;
                here.z += snoise(float3(k * _wispWanderScale + 19.7, t, dice * 11.0 + 3.3)) * amp;
                here.x += snoise(float3(k * _wispWanderScale * 3.0, t * 1.7, dice * 5.0)) * amp * 0.33;
                here.z += snoise(float3(k * _wispWanderScale * 3.0 + 7.1, t * 1.7, dice * 5.0 + 2.1)) * amp * 0.33;

                return here;
            }

            struct A
            {
                float4 pos:POSITION;    // (t along climb, v across ribbon, 0)
                float2 wisp:TEXCOORD0;  // x = wisp index
            };
            struct V
            {
                float4 pos:SV_POSITION;
                float4 ride:TEXCOORD0;  // climb, across, along, seed
                float2 depth:TEXCOORD1; // viewZ, unused
            };

            V vert(A i)
            {
                V o;
                float id = i.wisp.x;

                // The mesh carries the capacity; the live count is a setting
                // inside it. Anything past the count folds to a degenerate
                // point rather than wrapping back onto wisp zero.
                if (id >= _wisps)
                {
                    o.pos = float4(0, 0, -2, 1);
                    o.ride = 0;
                    o.depth = 0;
                    return o;
                }

                float d0 = hash11(id * 1.37 + _Seed * 0.7);
                float d1 = hash11(id * 2.71 + _Seed * 1.9);
                float d2 = hash11(id * 3.93 + _Seed * 2.7);

                float count = max(_wisps, 1.0);
                float bearing = (id + (d0 - 0.5) * _wispSpread) / count * WTAU + _Seed;
                float seat = _Radius * _wispSeat * lerp(1.0 - _wispSeatJitter, 1.0, d1);
                float height = _wispHeight * lerp(1.0 - _wispHeightJitter, 1.0 + _wispHeightJitter, d2) * max(_Grow, 0.0);

                // One loop per wisp, offset by its own dice so the layer never
                // pulses. The window runs from below the floor to past the top,
                // and the ends are faded in the fragment stage.
                float span = 1.0 + _wispLength;
                float base = frac(_SandboxTime * _wispRise + d0) * span - _wispLength;
                float climb = base + i.pos.x * _wispLength;

                float3 here   = wispSpine(climb, bearing, seat, height, d0);
                float3 ahead  = wispSpine(climb + 0.02, bearing, seat, height, d0);
                float3 behind = wispSpine(climb - 0.02, bearing, seat, height, d0);
                float3 tangent = normalize(ahead - behind + float3(0, 1e-4, 0));

                float3 view = normalize(_WorldSpaceCameraPos - here);
                float3 across = cross(tangent, view);
                float len = length(across);
                // Dead on the spine the ribbon has no plane to open in. Fall
                // back to anything perpendicular rather than collapsing.
                across = len > 1e-4 ? across / len : normalize(cross(tangent, float3(1, 0, 0)));

                // Widest low and closing as it climbs — smoke opens as it
                // rises, but a wisp is what is left of it, and what is left thins.
                float k = saturate(climb);
                float profile = pow(1.0 - k, max(_wispWidthBias, 0.05)) * sin(k * WPI * 0.85 + 0.25);
                float w = _wispWidth * height * max(profile, 0.0);

                float3 world = here + across * (i.pos.y * w);

                o.ride = float4(climb, i.pos.y, i.pos.x, d0);
                float4 mv = mul(GetWorldToViewMatrix(), float4(world, 1.0));
                o.depth = float2(mv.z, 0);
                o.pos = TransformWorldToHClip(world);
                return o;
            }

            half4 frag(V i):SV_Target
            {
                float climb = i.ride.x;
                // Outside its own climb the wisp does not exist — this is what
                // the ends of the loop look like, and it costs one smoothstep
                // rather than a branch.
                float live = smoothstep(0.0, _wispTailFade, climb) * (1.0 - smoothstep(1.0 - _wispHeadFade, 1.0, climb));
                if (live < 0.004) discard;

                // Across the ribbon: soft on both sides, never a hard edge —
                // this layer has no silhouette, it is the one thing in the
                // ability that is only light.
                float across = pow(saturate(1.0 - abs(i.ride.y)), max(_wispSoftEdge, 0.05));

                // Eaten along its length, in its own space, so the break-up
                // travels with it rather than the wisp sliding through a field.
                float erode = snoise01(float3(i.ride.z * _wispErodeScale,
                                              climb * _wispErodeScale * 0.5 - _SandboxTime * _wispErodeSpeed,
                                              i.ride.w * 23.0));
                erode = lerp(1.0, erode, saturate(_wispErode));

                float energy = across * erode * live * (1.0 + _Pulse * 0.5 + _Charge * 0.6);

                float3 color = lerp(_colorWispRoot.rgb, _colorWispBody.rgb, smoothstep(0.0, 0.4, climb));
                color = lerp(color, _colorWispTip.rgb, smoothstep(0.35, 1.0, climb));
                color *= energy * _wispIntensity * _Fade;

                float alpha = saturate(energy) * _Fade * _wispOpacity;

                if (_wispSoftFade > 1e-4)
                {
                    float2 screen = i.pos.xy / _ScaledScreenParams.xy;
                    float raw = SampleSceneDepth(screen);
                    float sceneView = LinearEyeDepth(raw, _ZBufferParams);
                    alpha *= saturate((sceneView + i.depth.x) / _wispSoftFade);
                }
                if (alpha < 0.003) discard;

                color *= _wispGlow * _GlobalGlow;
                color /= 1.0 + color * 0.12;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
