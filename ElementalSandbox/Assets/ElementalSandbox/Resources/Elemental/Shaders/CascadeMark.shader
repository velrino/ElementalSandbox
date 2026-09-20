// Ported from src/materials/CascadeMarkMaterial.js.
// Layer 1 — the mark cut into the floor. Everything here is a signed distance
// field in metres from the centre.
Shader "Elemental/CascadeMark"
{
    Properties
    {
        _markLineWidth("markLineWidth",Float)=0.034
        _markLineGlow("markLineGlow",Float)=1.45
        _markPoints("markPoints",Float)=4
        _markStarOuter("markStarOuter",Float)=0.94
        _markStarSharp("markStarSharp",Float)=3.2
        _markStarSpin("markStarSpin",Float)=0.012
        _markInnerScale("markInnerScale",Float)=0.62
        _markInnerGain("markInnerGain",Float)=0.75
        _markDiamond("markDiamond",Float)=1
        _markDiamondSeat("markDiamondSeat",Float)=0.5
        _markDiamondAspect("markDiamondAspect",Float)=1
        _markDiamondSpin("markDiamondSpin",Float)=-0.008
        _markSpear("markSpear",Float)=1
        _markSpearFrom("markSpearFrom",Float)=0.36
        _markSpearTo("markSpearTo",Float)=0.9
        _markSpearWidth("markSpearWidth",Float)=0.055
        _markHooks("markHooks",Float)=1
        _markHookCount("markHookCount",Float)=4
        _markHookSeat("markHookSeat",Float)=0.26
        _markHookSweep("markHookSweep",Float)=0.8
        _markHookWidth("markHookWidth",Float)=0.05
        _markHookSpin("markHookSpin",Float)=0.022
        _markRibs("markRibs",Float)=0.5
        _markRibCount("markRibCount",Float)=9
        _markRibLength("markRibLength",Float)=0.16
        _markRibWidth("markRibWidth",Float)=0.22
        _markTicks("markTicks",Float)=0.35
        _markTickCount("markTickCount",Float)=32
        _markTickSeat("markTickSeat",Float)=0.72
        _markTickLength("markTickLength",Float)=0.07
        _markHub("markHub",Float)=1
        _markHubRing("markHubRing",Float)=0.1
        _markHubDot("markHubDot",Float)=0.05
        _markWash("markWash",Float)=0.32
        _markWashFalloff("markWashFalloff",Float)=1.6
        _markGrain("markGrain",Float)=0.45
        _markGrainScale("markGrainScale",Float)=2.4
        _markOpacity("markOpacity",Float)=1
        _markGlow("markGlow",Float)=0.9
        _colorMarkLine("colorMarkLine",Color)=(0.37,0.95,1,1)
        _colorMarkCore("colorMarkCore",Color)=(0.9,1,1,1)
        _colorMarkDeep("colorMarkDeep",Color)=(0.17,0.42,0.87,1)
        _colorMarkWash("colorMarkWash",Color)=(0.1,0.56,0.66,1)
        _colorFront("colorFront",Color)=(0.86,1,1,1)
        _QuadSize("Quad size",Float)=8
        _Radius("Radius",Float)=4
        _Grown("Grown",Float)=4
        _Front("Front",Float)=0
        _Flare("Flare",Float)=0
        _Seed("Seed",Float)=0
        _Pulse("Pulse",Float)=0
        _Fade("Fade",Float)=1
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent"}
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

            #define MTAU 6.283185307179586
            #define MPI  3.141592653589793

            float _markLineWidth, _markLineGlow;
            float _markPoints, _markStarOuter, _markStarSharp, _markStarSpin, _markInnerScale, _markInnerGain;
            float _markDiamond, _markDiamondSeat, _markDiamondAspect, _markDiamondSpin;
            float _markSpear, _markSpearFrom, _markSpearTo, _markSpearWidth;
            float _markHooks, _markHookCount, _markHookSeat, _markHookSweep, _markHookWidth, _markHookSpin;
            float _markRibs, _markRibCount, _markRibLength, _markRibWidth;
            float _markTicks, _markTickCount, _markTickSeat, _markTickLength;
            float _markHub, _markHubRing, _markHubDot;
            float _markWash, _markWashFalloff, _markGrain, _markGrainScale;
            float _markOpacity, _markGlow;
            float4 _colorMarkLine, _colorMarkCore, _colorMarkDeep, _colorMarkWash, _colorFront;
            float _QuadSize, _Radius, _Grown, _Front, _Flare, _Seed, _Pulse, _Fade, _GlobalGlow;

            // GLSL mod, which floors. HLSL fmod truncates, and every fold below
            // takes a negative angle at some point.
            float gmod(float a, float b) { return a - b * floor(a / b); }

            // A line of live width, antialiased and energy conserving. The width
            // is floored at the pixel footprint and the brightness scaled back by
            // however far it had to open — so a rib the camera is looking along
            // gets wider and dimmer instead of breaking into a dotted line. This
            // is the single most important function in the file.
            float stroke(float d, float w, float aa)
            {
                float ww = max(w, aa);
                return (1.0 - smoothstep(0.0, ww, abs(d))) * (w / ww);
            }

            float solid(float d, float aa) { return 1.0 - smoothstep(-aa, aa, d); }

            // Distance to a segment whose half-width runs from wa at a to wb at
            // b. Most of the mark is this: with wb at zero it is a spearhead,
            // with both ends equal a rib, at a tenth of the size a tick.
            float taper(float2 p, float2 a, float2 b, float wa, float wb)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h) - lerp(wa, wb, h);
            }

            // A circular arc swept from -half to +half about +x, tapering along
            // its own sweep. The curls in the middle of the reference mark are
            // barbed: thick where they leave the hub, closing to a point where
            // they end. A constant-width arc reads as a washer.
            float hookSDF(float2 p, float ra, float sweep, float wa, float wb)
            {
                float ang = atan2(p.y, p.x);
                float k = clamp(ang / max(sweep, 1e-4), -1.0, 1.0);
                if (abs(ang) <= sweep)
                {
                    float w = lerp(wa, wb, k * 0.5 + 0.5);
                    return abs(length(p) - ra) - w;
                }
                float e = sign(ang) * sweep;
                float2 tip = float2(cos(e), sin(e)) * ra;
                return length(p - tip) - (sign(ang) > 0.0 ? wb : wa);
            }

            // Exact SDF of an n-pointed star (after iq). The sharp argument runs
            // from 2 to the point count and is the wrong way round from what the
            // name suggests: 2 is the regular polygon and the point count is the
            // sharpest star. At 4 points, ~3.2 gives the long concave barbs the
            // reference sheet is built on; 2 gives a square, which is what a
            // mis-set value looks like.
            float starSDF(float2 p, float radius, float points, float sharp)
            {
                float n = max(points, 2.0);
                float m = clamp(sharp, 2.0, n);
                float an = MPI / n, en = MPI / m;
                float2 acs = float2(cos(an), sin(an));
                float2 ecs = float2(cos(en), sin(en));

                float bn = gmod(atan2(p.x, p.y), 2.0 * an) - an;
                float2 q = length(p) * float2(cos(bn), abs(sin(bn)));
                q -= radius * acs;
                q += ecs * clamp(-dot(q, ecs), 0.0, radius * acs.y / ecs.y);
                return length(q) * sign(q.x);
            }

            float ndot(float2 a, float2 b) { return a.x * b.x - a.y * b.y; }

            // Exact SDF of a rhombus with half-diagonals b (after iq).
            float rhombusSDF(float2 p, float2 b)
            {
                float2 q = abs(p);
                float h = clamp(ndot(b - 2.0 * q, b) / max(dot(b, b), 1e-6), -1.0, 1.0);
                float d = length(q - 0.5 * b * float2(1.0 - h, 1.0 + h));
                return d * sign(q.x * b.y + q.y * b.x - b.x * b.y);
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
                // Metres from the centre. Everything below is in metres.
                float2 p = float2(i.uv.x - 0.5, 0.5 - i.uv.y) * _QuadSize;
                float r = length(p);
                float t = _SandboxTime;

                float aa = fwidth(r) + 1e-4;
                // How much floor one pixel covers. Fine detail is faded out as
                // it outgrows its own features, which is what a mip chain does
                // for a texture.
                float footprint = max(fwidth(p.x), fwidth(p.y));
                float detail = 1.0 - smoothstep(0.02, 0.15, footprint);

                float reach = _Radius * _markStarOuter;
                if (r > reach + 0.6) discard;

                // The mark is cut outward, so nothing exists past the front.
                float open = 1.0 - smoothstep(_Grown - 0.28, _Grown + 0.1, r);
                if (open < 0.002) discard;

                float beat = 1.0 + _Pulse + _Flare * 1.6;
                float width = _markLineWidth * (1.0 + _Flare * 0.35);

                float lines = 0.0, cores = 0.0;

                /* ---- the star, and the smaller one inside it ---- */
                float spin = t * _markStarSpin * MTAU;
                float2 ps = mul(rot2(spin), p);
                float star = starSDF(ps, _Radius * _markStarOuter, _markPoints, _markStarSharp);
                lines += stroke(star, width, aa) * _markLineGlow;

                float2 pi = mul(rot2(-spin * 1.7 + MPI / max(_markPoints, 2.0)), p);
                float inner = starSDF(pi, _Radius * _markStarOuter * _markInnerScale, _markPoints, _markStarSharp + 0.5);
                lines += stroke(inner, width * 0.8, aa) * _markLineGlow * _markInnerGain;

                /* ---- the diamond that frames them ---- */
                float2 pd = mul(rot2(t * _markDiamondSpin * MTAU + MPI * 0.25), p);
                float seat = _Radius * _markDiamondSeat;
                float diamond = rhombusSDF(pd, float2(seat, seat * _markDiamondAspect));
                lines += stroke(diamond, width * 1.25, aa) * _markLineGlow * _markDiamond;

                /* ---- the ribs combed down each edge of it ---- */
                if (_markRibs > 0.001 && detail > 0.01)
                {
                    // Walked around the rhombus in its own frame: each rib is a
                    // short taper struck inward from a point on the edge, so
                    // they fan with the shape instead of radiating from the
                    // middle.
                    float ribs = max(floor(_markRibCount), 1.0);
                    float2 q = abs(pd);
                    float edge = q.x / max(seat, 1e-4) + q.y / max(seat * _markDiamondAspect, 1e-4);
                    float along = q.x / max(seat, 1e-4);
                    float cell = frac(along * ribs);
                    float bar = 1.0 - smoothstep(_markRibWidth * 0.5, _markRibWidth, abs(cell - 0.5) * 2.0);
                    float band = 1.0 - smoothstep(0.0, _markRibLength, abs(edge - 1.0));
                    lines += bar * band * _markRibs * detail;
                }

                /* ---- the spearheads on the star's points ---- */
                if (_markSpear > 0.001)
                {
                    float points = max(floor(_markPoints), 2.0);
                    float sector = MTAU / points;
                    // Folded the way starSDF folds — atan2(x, y), so the first
                    // point is on +Y. Measured from +X instead and the barbs
                    // come out on the star's waists, which reads as a compass
                    // rose rather than as a barbed mark.
                    float ang = atan2(ps.x, ps.y);
                    float local = gmod(ang + sector * 0.5, sector) - sector * 0.5;
                    float2 pf = float2(sin(local), cos(local)) * length(ps);
                    float d = taper(pf,
                                    float2(0.0, _Radius * _markSpearFrom),
                                    float2(0.0, _Radius * _markSpearTo),
                                    _Radius * _markSpearWidth, 0.0);
                    // Ramped along its own length rather than filled flat: a
                    // solid wedge on the floor reads as a paper cutout, and the
                    // gradient is what makes it a barb driven outward.
                    float alongS = saturate((length(ps) / max(_Radius, 0.05) - _markSpearFrom)
                                          / max(_markSpearTo - _markSpearFrom, 1e-3));
                    float head = solid(d, aa) * lerp(0.35, 1.0, alongS);
                    lines += head * _markSpear;
                    cores += head * _markSpear * alongS * 0.35;
                }

                /* ---- the knot of hooks in the middle ---- */
                if (_markHooks > 0.001)
                {
                    float count = max(floor(_markHookCount), 1.0);
                    float sector = MTAU / count;
                    float turn = t * _markHookSpin * MTAU;
                    float ang = atan2(p.y, p.x) - turn;
                    float local = gmod(ang + sector * 0.5, sector) - sector * 0.5;
                    float2 ph = float2(cos(local), sin(local)) * r;
                    float d = hookSDF(ph, _Radius * _markHookSeat, _markHookSweep,
                                      _Radius * _markHookWidth, _Radius * _markHookWidth * 0.06);
                    float curl = solid(d, aa);
                    lines += curl * _markHooks;
                    cores += curl * _markHooks * 0.28;
                }

                /* ---- ticks around the rim ---- */
                if (_markTicks > 0.001 && detail > 0.01)
                {
                    float count = max(floor(_markTickCount), 1.0);
                    float sector = MTAU / count;
                    float ang = atan2(p.x, p.y) + t * _markStarSpin * MTAU * 0.5;
                    float local = gmod(ang + sector * 0.5, sector) - sector * 0.5;
                    float2 pt = float2(cos(local), sin(local)) * r;
                    float d = taper(pt,
                                    float2(_Radius * _markTickSeat, 0.0),
                                    float2(_Radius * (_markTickSeat + _markTickLength), 0.0),
                                    width * 1.1, 0.0);
                    lines += solid(d, aa) * _markTicks * detail;
                }

                /* ---- the hub ---- */
                float hub = stroke(r - _Radius * _markHubRing, width * 1.1, aa) * _markLineGlow;
                hub += solid(r - _Radius * _markHubDot, aa) * 1.4;
                lines += hub * _markHub;
                cores += solid(r - _Radius * _markHubDot, aa) * _markHub;

                /* ---- the wash inside it all ---- */
                // Bounded by the star rather than by a circle, so the fill has
                // the mark's own shape and the barbs read as solid rather than
                // as outlines with a disc behind them.
                float body = solid(star, aa * 2.0);
                float wash = body * pow(saturate(1.0 - r / max(reach, 0.05)), max(_markWashFalloff, 0.05));
                float grain = (snoise01(float3(p * _markGrainScale, _Seed * 3.0 + t * 0.12)) - 0.5) * _markGrain;
                wash *= 1.0 + grain * detail;
                wash *= _markWash;

                /* ---- the front racing out to the boundary ---- */
                float front = (1.0 - smoothstep(0.0, 0.4, abs(r - _Grown))) * _Front;

                /* ---- put it together ---- */
                float3 color = lerp(_colorMarkLine.rgb, _colorMarkCore.rgb, saturate(lines * 0.16 + cores * 0.5)) * lines * beat;
                color += _colorMarkDeep.rgb * body * saturate(1.0 - lines) * _markWash * 0.35;
                color += _colorMarkWash.rgb * wash * beat;
                color += _colorFront.rgb * front * 2.4;

                float alpha = saturate(lines * 0.9 + wash + front);
                alpha *= open * _Fade * _markOpacity;
                if (alpha < 0.004) discard;

                color *= _markGlow * _GlobalGlow;
                // The soft ceiling every additive pass in this project ends on:
                // the terms above are independent and stack, and a spearhead on
                // the diamond in the wash sums past ten without it.
                color /= 1.0 + color * 0.16;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
