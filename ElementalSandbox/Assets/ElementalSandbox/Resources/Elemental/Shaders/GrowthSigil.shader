// Ported from src/materials/NatureSigilMaterial.js.
// Layer 1 — the sigil cut into the floor, with its generated rune band.
Shader "Elemental/GrowthSigil"
{
    Properties
    {
        _sigilRailWidth("sigilRailWidth",Float)=0.032
        _sigilRailOuter("sigilRailOuter",Float)=1
        _sigilRailInner("sigilRailInner",Float)=0.84
        _sigilRailHub("sigilRailHub",Float)=0.2
        _sigilRailGlow("sigilRailGlow",Float)=1.7
        _sigilSpin("sigilSpin",Float)=0.014
        _sigilRunes("sigilRunes",Float)=46
        _sigilRuneBand("sigilRuneBand",Float)=0.32
        _sigilRuneSeat("sigilRuneSeat",Float)=0.92
        _sigilRuneWeight("sigilRuneWeight",Float)=0.05
        _sigilRuneStrokes("sigilRuneStrokes",Float)=0.52
        _sigilRuneSweep("sigilRuneSweep",Float)=1.5
        _sigilRuneSweepSpeed("sigilRuneSweepSpeed",Float)=0.13
        _sigilRuneSweepWidth("sigilRuneSweepWidth",Float)=0.09
        _sigilRuneFlicker("sigilRuneFlicker",Float)=0.22
        _sigilRuneGlow("sigilRuneGlow",Float)=2.3
        _sigilTicks("sigilTicks",Float)=0.75
        _sigilTickCount("sigilTickCount",Float)=72
        _sigilTickWidth("sigilTickWidth",Float)=0.32
        _sigilTickLength("sigilTickLength",Float)=0.055
        _sigilStar("sigilStar",Float)=1
        _sigilStarRadius("sigilStarRadius",Float)=0.66
        _sigilStarWidth("sigilStarWidth",Float)=0.028
        _sigilStarSpin("sigilStarSpin",Float)=-0.009
        _sigilFiligree("sigilFiligree",Float)=0.95
        _sigilFiligreeSeat("sigilFiligreeSeat",Float)=0.52
        _sigilFiligreeAmp("sigilFiligreeAmp",Float)=0.075
        _sigilFiligreeLobes("sigilFiligreeLobes",Float)=6
        _sigilFiligreeWidth("sigilFiligreeWidth",Float)=0.02
        _sigilFiligreeSpin("sigilFiligreeSpin",Float)=0.018
        _sigilPool("sigilPool",Float)=0.3
        _sigilPoolFalloff("sigilPoolFalloff",Float)=2.2
        _sigilGrain("sigilGrain",Float)=0.45
        _sigilGrainScale("sigilGrainScale",Float)=2.6
        _sigilOpacity("sigilOpacity",Float)=1
        _sigilGlow("sigilGlow",Float)=1.15
        _colorSigil("colorSigil",Color)=(0.37,0.88,0.54,1)
        _colorSigilCore("colorSigilCore",Color)=(0.9,1,0.94,1)
        _colorRune("colorRune",Color)=(0.54,1,0.69,1)
        _colorSigilPool("colorSigilPool",Color)=(0.18,0.56,0.35,1)
        _colorFront("colorFront",Color)=(0.85,1,0.69,1)
        _QuadSize("Quad size",Float)=10
        _Radius("Radius",Float)=4
        _Grown("Grown",Float)=4
        _Front("Front",Float)=0
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

            #define STAU 6.283185307179586
            #define SPI  3.141592653589793

            float _sigilRailWidth, _sigilRailOuter, _sigilRailInner, _sigilRailHub, _sigilRailGlow, _sigilSpin;
            float _sigilRunes, _sigilRuneBand, _sigilRuneSeat, _sigilRuneWeight, _sigilRuneStrokes;
            float _sigilRuneSweep, _sigilRuneSweepSpeed, _sigilRuneSweepWidth, _sigilRuneFlicker, _sigilRuneGlow;
            float _sigilTicks, _sigilTickCount, _sigilTickWidth, _sigilTickLength;
            float _sigilStar, _sigilStarRadius, _sigilStarWidth, _sigilStarSpin;
            float _sigilFiligree, _sigilFiligreeSeat, _sigilFiligreeAmp, _sigilFiligreeLobes, _sigilFiligreeWidth, _sigilFiligreeSpin;
            float _sigilPool, _sigilPoolFalloff, _sigilGrain, _sigilGrainScale, _sigilOpacity, _sigilGlow;
            float4 _colorSigil, _colorSigilCore, _colorRune, _colorSigilPool, _colorFront;
            float _QuadSize, _Radius, _Grown, _Front, _Seed, _Pulse, _Fade, _GlobalGlow;

            float gmod(float a, float b) { return a - b * floor(a / b); }

            // A band around a circle, antialiased and energy conserving. The
            // width is floored at the pixel footprint and the brightness scaled
            // back by however far it had to open — so a rail the camera is
            // looking along gets wider and dimmer instead of breaking into
            // sparks. This is the single most important function in the file.
            float rail(float r, float radius, float width, float aa)
            {
                float w = max(width, aa);
                return (1.0 - smoothstep(0.0, w, abs(r - radius))) * (width / w);
            }

            // Distance to the outline of a regular n-gon of apothem a, in polar.
            float polygonEdge(float r, float ang, float sides, float apothem, float rot)
            {
                float sector = STAU / max(sides, 3.0);
                float a = gmod(ang - rot + sector * 0.5, sector) - sector * 0.5;
                return abs(r * cos(a) - apothem);
            }

            // Distance from p to the segment ab. The rune alphabet is built out of it.
            float segmentD(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h);
            }

            // One generated glyph, in a cell running -0.5..0.5 on both axes.
            // Nine candidate strokes, each kept or dropped on its own hash of
            // the cell's id — so a glyph is a *subset* of an alphabet rather
            // than a symbol looked up from one. Two cells never carry the same
            // mark unless they draw the same subset, which at nine strokes is
            // unlikely enough to never be seen in a ring of forty.
            float glyph(float2 p, float id, float weight)
            {
                float keep = saturate(_sigilRuneStrokes);
                float d = 1e3;

                // The spine, and the two rails either side of it.
                if (hash11(id * 1.7 + 0.11) < keep + 0.25) d = min(d, segmentD(p, float2(0.0, -0.42), float2(0.0, 0.42)));
                if (hash11(id * 2.3 + 0.27) < keep) d = min(d, segmentD(p, float2(-0.3, -0.34), float2(-0.3, 0.2)));
                if (hash11(id * 3.1 + 0.43) < keep) d = min(d, segmentD(p, float2(0.3, -0.2), float2(0.3, 0.34)));
                // Cross bars.
                if (hash11(id * 4.7 + 0.59) < keep) d = min(d, segmentD(p, float2(-0.32, 0.24), float2(0.32, 0.24)));
                if (hash11(id * 5.3 + 0.71) < keep) d = min(d, segmentD(p, float2(-0.32, -0.24), float2(0.32, -0.24)));
                // Diagonals — the strokes that make it read as script rather than a grid.
                if (hash11(id * 6.1 + 0.83) < keep) d = min(d, segmentD(p, float2(-0.3, -0.3), float2(0.0, 0.05)));
                if (hash11(id * 7.9 + 0.97) < keep) d = min(d, segmentD(p, float2(0.3, 0.3), float2(0.0, -0.05)));
                if (hash11(id * 8.3 + 1.13) < keep) d = min(d, segmentD(p, float2(-0.28, 0.32), float2(0.06, -0.02)));
                // And a bowl, so some glyphs are round.
                if (hash11(id * 9.7 + 1.31) < keep * 0.7) d = min(d, abs(length(p - float2(0.0, -0.12)) - 0.17));

                return 1.0 - smoothstep(weight * 0.5, weight, d);
            }

            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            V vert(A i) { V o; o.uv = i.uv; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }

            half4 frag(V i):SV_Target
            {
                float2 p = float2(i.uv.x - 0.5, 0.5 - i.uv.y) * _QuadSize;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float t = _SandboxTime;

                float aa = fwidth(r) + 1e-4;
                float footprint = max(fwidth(p.x), fwidth(p.y));
                float detail = 1.0 - smoothstep(0.02, 0.16, footprint);

                float outer = _Radius * _sigilRailOuter;
                if (r > outer + aa * 6.0 + 0.5) discard;

                // The sigil is cut *out* of the floor as the growth races to
                // the boundary, so nothing is visible past the front.
                float open = 1.0 - smoothstep(_Grown - 0.35, _Grown + 0.15, r);
                if (open < 0.002) discard;

                float beat = 1.0 + _Pulse;

                /* ---- the rails ---- */
                float spin = t * _sigilSpin * STAU;
                float lines = 0.0;
                lines += rail(r, outer, _sigilRailWidth, aa) * _sigilRailGlow;
                lines += rail(r, _Radius * _sigilRailInner, _sigilRailWidth * 1.6, aa) * _sigilRailGlow;
                lines += rail(r, _Radius * _sigilRailHub, _sigilRailWidth * 0.9, aa) * _sigilRailGlow * 0.8;

                /* ---- the ticks around the outer rail ---- */
                float tickPhase = frac((ang + spin * 0.35) / STAU * max(_sigilTickCount, 1.0));
                float tickMask = 1.0 - smoothstep(_sigilTickWidth * 0.5, _sigilTickWidth, abs(tickPhase - 0.5) * 2.0);
                float tickBand = rail(r, outer - _Radius * _sigilTickLength * 0.5, _Radius * _sigilTickLength * 0.5, aa);
                lines += tickMask * tickBand * _sigilTicks * detail;

                /* ---- the star: a triangle and its inverse ---- */
                float apothem = _Radius * _sigilStarRadius * 0.5;
                float rot = t * _sigilStarSpin * STAU;
                float triA = polygonEdge(r, ang, 3.0, apothem, rot);
                float triB = polygonEdge(r, ang, 3.0, apothem, rot + SPI / 3.0);
                float star = 1.0 - smoothstep(0.0, max(_sigilStarWidth, aa), min(triA, triB));
                // Clipped to the disc the star is inscribed in, or the polygon's
                // edges run out to infinity along their own lines.
                star *= 1.0 - smoothstep(_Radius * _sigilStarRadius * 0.96, _Radius * _sigilStarRadius * 1.02, r);
                lines += star * _sigilStar;

                /* ---- the vine filigree ---- */
                // A pair of circles that *wander*: the radius breathes with the
                // angle, so the arcs weave instead of running true.
                float fa = ang + t * _sigilFiligreeSpin * STAU;
                float fb = ang - t * _sigilFiligreeSpin * STAU * 0.7;
                float seatA = _Radius * _sigilFiligreeSeat;
                float seatB = _Radius * _sigilFiligreeSeat * 0.78;
                float wobbleA = _sigilFiligreeAmp * _Radius * sin(fa * _sigilFiligreeLobes + _Seed);
                float wobbleB = _sigilFiligreeAmp * _Radius * 0.7 * sin(fb * (_sigilFiligreeLobes + 3.0) + _Seed * 2.3);
                float fil = rail(r, seatA + wobbleA, _sigilFiligreeWidth, aa);
                fil += rail(r, seatB + wobbleB, _sigilFiligreeWidth * 0.8, aa) * 0.8;
                lines += fil * _sigilFiligree;

                /* ---- the rune band ---- */
                float band = _Radius * _sigilRuneSeat;
                float bandHalf = _sigilRuneBand * 0.5;
                float runes = 0.0;
                if (abs(r - band) < bandHalf && detail > 0.01)
                {
                    float cells = max(floor(_sigilRunes), 1.0);
                    // The ring turns, and the glyphs turn with it — a band whose
                    // script slid through stationary cells would read as ticker tape.
                    float around = frac((ang + spin) / STAU) * cells;
                    float id = floor(around) + _Seed * 17.0;
                    float cellWidth = STAU * band / cells;
                    float2 q = float2((frac(around) - 0.5) * cellWidth / max(_sigilRuneBand, 1e-3), (r - band) / _sigilRuneBand);
                    runes = glyph(q, id, _sigilRuneWeight);

                    // A read head running round the ring, and a per-glyph
                    // stutter under it.
                    float head = frac((ang + spin) / STAU - t * _sigilRuneSweepSpeed);
                    head = 1.0 - smoothstep(0.0, max(_sigilRuneSweepWidth, 1e-3), min(head, 1.0 - head));
                    float flicker = 1.0 - _sigilRuneFlicker * hash11(floor(id) + floor(t * 6.0) * 0.37);
                    runes *= flicker * (1.0 + head * _sigilRuneSweep);
                    runes *= detail;
                }

                /* ---- the pool of light inside it all ---- */
                float pool = pow(saturate(1.0 - r / max(_Radius, 0.05)), max(_sigilPoolFalloff, 0.05)) * _sigilPool;
                float grain = (snoise01(float3(p * _sigilGrainScale, _Seed * 3.0 + t * 0.15)) - 0.5) * _sigilGrain;
                pool *= 1.0 + grain * detail;

                /* ---- the growth front racing out to the boundary ---- */
                float front = (1.0 - smoothstep(0.0, 0.55, abs(r - _Grown))) * _Front;

                /* ---- put it together ---- */
                float3 color = lerp(_colorSigil.rgb, _colorSigilCore.rgb, saturate(lines * 0.35)) * lines * beat;
                color += _colorRune.rgb * runes * _sigilRuneGlow * beat;
                color += _colorSigilPool.rgb * pool * beat;
                color += _colorFront.rgb * front * 2.2;

                float alpha = saturate(lines * 0.85 + runes * 0.9 + pool + front);
                alpha *= open * _Fade * _sigilOpacity;
                if (alpha < 0.004) discard;

                color *= _sigilGlow * _GlobalGlow;
                color /= 1.0 + color * 0.16;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
