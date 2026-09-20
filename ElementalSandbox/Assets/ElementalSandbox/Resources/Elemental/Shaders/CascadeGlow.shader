// Ported from src/materials/CascadeGlowMaterial.js.
// Layer 0 — the ground glow the mark is cut into.
Shader "Elemental/CascadeGlow"
{
    Properties
    {
        _glowPool("glowPool",Float)=1
        _glowPoolFalloff("glowPoolFalloff",Float)=1.6
        _glowLip("glowLip",Float)=1
        _glowLipSeat("glowLipSeat",Float)=0.96
        _glowLipWidth("glowLipWidth",Float)=0.06
        _glowSpill("glowSpill",Float)=0.35
        _glowSpillReach("glowSpillReach",Float)=1.35
        _glowSpillFalloff("glowSpillFalloff",Float)=2.2
        _glowWobble("glowWobble",Float)=0.02
        _glowWobbleLobes("glowWobbleLobes",Float)=5
        _glowWobbleSpeed("glowWobbleSpeed",Float)=0.35
        _glowGrain("glowGrain",Float)=0.3
        _glowGrainScale("glowGrainScale",Float)=1.6
        _glowSweep("glowSweep",Float)=0.4
        _glowSweepSpeed("glowSweepSpeed",Float)=0.08
        _glowSweepWidth("glowSweepWidth",Float)=0.12
        _glowOpacity("glowOpacity",Float)=1
        _glowGlow("glowGlow",Float)=1
        _colorGlowCore("colorGlowCore",Color)=(0.5,1,0.95,1)
        _colorGlowPool("colorGlowPool",Color)=(0.12,0.83,0.78,1)
        _colorGlowRim("colorGlowRim",Color)=(0.68,1,0.96,1)
        _QuadSize("Quad size",Float)=12
        _Radius("Radius",Float)=4
        _Grown("Grown",Float)=4
        _Flare("Flare",Float)=0
        _Seed("Seed",Float)=0
        _Pulse("Pulse",Float)=0
        _Fade("Fade",Float)=1
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-6" "RenderType"="Transparent"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ElementalCommon.hlsl"
            #include "SourceNoise.hlsl"

            #define GTAU 6.283185307179586

            float _glowPool, _glowPoolFalloff, _glowLip, _glowLipSeat, _glowLipWidth;
            float _glowSpill, _glowSpillReach, _glowSpillFalloff;
            float _glowWobble, _glowWobbleLobes, _glowWobbleSpeed;
            float _glowGrain, _glowGrainScale;
            float _glowSweep, _glowSweepSpeed, _glowSweepWidth;
            float _glowOpacity, _glowGlow;
            float4 _colorGlowCore, _colorGlowPool, _colorGlowRim;
            float _QuadSize, _Radius, _Grown, _Flare, _Seed, _Pulse, _Fade, _GlobalGlow;

            // A band of live width, antialiased and energy conserving.
            float band(float d, float w, float aa)
            {
                float ww = max(w, aa);
                return (1.0 - smoothstep(0.0, ww, abs(d))) * (w / ww);
            }

            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.uv = i.uv;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                return o;
            }

            half4 frag(V i):SV_Target
            {
                float2 p = float2(i.uv.x - 0.5, 0.5 - i.uv.y) * _QuadSize;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float t = _SandboxTime;

                float aa = fwidth(r) + 1e-4;
                float footprint = max(fwidth(p.x), fwidth(p.y));
                float detail = 1.0 - smoothstep(0.02, 0.16, footprint);

                float outer = _Radius * _glowSpillReach;
                if (r > outer + 0.4) discard;

                float open = 1.0 - smoothstep(_Grown - 0.5, _Grown + 0.2, r);
                if (open < 0.002) discard;

                // The boundary is never quite a circle. Damped by the footprint,
                // or the wobble that reads as life up close reads as a serrated
                // edge far away.
                float wobble = sin(ang * _glowWobbleLobes + t * _glowWobbleSpeed + _Seed * 5.0) * _glowWobble * detail;
                float edge = _Radius * (1.0 + wobble);

                float beat = 1.0 + _Pulse + _Flare * 2.0;

                /* ---- the pool ---- */
                float pool = pow(saturate(1.0 - r / max(edge, 0.05)), max(_glowPoolFalloff, 0.05)) * _glowPool;

                /* ---- the lip just inside the boundary ---- */
                float lip = band(r - edge * _glowLipSeat, _glowLipWidth, aa) * _glowLip;

                /* ---- the spill past it ---- */
                float spill = pow(saturate(1.0 - r / max(outer, 0.05)), max(_glowSpillFalloff, 0.05)) * _glowSpill;

                /* ---- a read head turning round the pool ---- */
                // Slow, wide and faint: it is what keeps a static disc from
                // looking like a decal, and it is the only moving thing here.
                float head = frac(ang / GTAU - t * _glowSweepSpeed);
                head = 1.0 - smoothstep(0.0, max(_glowSweepWidth, 1e-3), min(head, 1.0 - head));
                float sweep = head * _glowSweep * pool;

                float grain = (snoise01(float3(p * _glowGrainScale, _Seed * 2.0 + t * 0.2)) - 0.5) * _glowGrain;
                pool *= 1.0 + grain * detail;

                float energy = pool + lip + spill + sweep;
                float3 color = lerp(_colorGlowPool.rgb, _colorGlowCore.rgb, saturate(pool * 1.6 + sweep)) * (pool + sweep);
                color += _colorGlowRim.rgb * lip;
                color += _colorGlowPool.rgb * spill;
                color *= beat;

                float alpha = saturate(energy) * open * _Fade * _glowOpacity;
                if (alpha < 0.004) discard;

                color *= _glowGlow * _GlobalGlow;
                color /= 1.0 + color * 0.18;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
