// Ported from src/materials/CascadeBladeMaterial.js (BLADE_SHAPE + BLADE_LOOK).
// Preserve the source equations. Property names match the source setting keys
// so SourceCascade can bind all 299 cascade values without a lookup table.
#ifndef CASCADE_BLADE_INCLUDED
#define CASCADE_BLADE_INCLUDED

#define CTAU 6.283185307179586
#define CPI  3.141592653589793

/* ---------------------------------------------------------------- */
/* the shape                                                         */
/* ---------------------------------------------------------------- */

float _bladeWaist, _bladeRootPower, _bladeTipPower;
float _bladeWidth, _bladeThick, _bladeEdge, _bladeBow, _bladeTwist;

// Half-width at t, as a fraction of the blade's length. Two powers meeting at
// the waist: a fast swell off the root and a long draw out to the point. Both
// ends reach exactly zero, so the blade closes on a genuine point at either
// end rather than on a flat cap the camera can catch.
float bladeWidth(float t)
{
    float waist = clamp(_bladeWaist, 0.02, 0.95);
    float rise = pow(saturate(t / waist), max(_bladeRootPower, 0.05));
    float fall = pow(saturate((1.0 - t) / (1.0 - waist)), max(_bladeTipPower, 0.05));
    return rise * fall;
}

// The cross-section, in the (n1, n2) plane of the blade's own frame. A lens,
// not a circle: the thickness is pinched to nothing at the two angles where
// the width is greatest, so what comes out has a sharp edge down each side and
// a spine ridge along each face — and a view straight down the edge shows a
// line rather than a tube. This is the whole difference between a crystal and
// a carrot.
float2 bladeSection(float a)
{
    float ang = a * CTAU;
    float x = cos(ang);
    float y = sin(ang);
    float pinch = pow(1.0 - abs(x), max(_bladeEdge, 0.05));
    return float2(x, y * clamp(_bladeThick, 0.02, 4.0) * pinch);
}

// A stable pair of axes across the heading, rolled by the blade's own dice.
void bladeFrame(float3 axis, float roll, out float3 n1, out float3 n2)
{
    float3 guide = abs(axis.y) < 0.92 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 e1 = normalize(cross(axis, guide));
    float3 e2 = cross(axis, e1);
    float cr = cos(roll), sr = sin(roll);
    n1 = e1 * cr + e2 * sr;
    n2 = e2 * cr - e1 * sr;
}

// One vertex of a blade rooted at root, pointing along axis, len metres long.
// The twist is applied to the section frame only and the bow to the spine
// only, so a blade can wind about itself without its curve winding with it.
float3 bladeVertex(float3 root, float3 axis, float roll, float len, float t, float a, out float3 nrm)
{
    float3 b1, b2;
    bladeFrame(axis, roll, b1, b2);

    float tw = t * _bladeTwist;
    float ct = cos(tw), st = sin(tw);
    float3 n1 = b1 * ct + b2 * st;
    float3 n2 = b2 * ct - b1 * st;

    float3 spine = root + axis * (t * len) + b1 * (sin(t * CPI) * _bladeBow * len);
    float w = bladeWidth(t) * _bladeWidth * len;

    float2 s0 = bladeSection(a);
    float2 s1 = bladeSection(a + 0.008);
    float2 tangent = s1 - s0;
    float2 flatN = normalize(float2(tangent.y, -tangent.x) + 1e-6);
    nrm = normalize(n1 * flatN.x + n2 * flatN.y);

    return spine + (n1 * s0.x + n2 * s0.y) * w;
}

/* ---------------------------------------------------------------- */
/* the look                                                          */
/* ---------------------------------------------------------------- */

float4 _colorBladeBody, _colorBladeFacet, _colorBladeBodyDeep, _colorBladeFacetDeep;
float4 _colorBladeEdge, _colorBladeVein, _colorBladeHot, _colorHeart;

float _bladeEdgeGlow, _bladeEdgePower, _bladeRim, _bladeRimPower;
float _bladeTipGlow, _bladeTipStart;
float _bladeVein, _bladeVeinScale, _bladeVeinBands, _bladeVeinSharp;
float _bladeHeartBleed, _bladeHeartReach, _bladeChargeGain, _bladeBurnGlow, _bladeGlow;

float _Charge, _Fade, _Collapse, _Pulse, _GlobalGlow;
float4 _Centre;

// Everything a blade emits, from the resolved face normal. Written once and
// called from both the crown and the volley, so the two cannot drift apart.
// burn: 0..1 nothing → 1 on the line the blade is being eaten back to.
float3 bladeGlow(float3 N, float3 worldPos, float3 viewDir,
                 float bladeT, float bladeA, float seed, float tone, float heat,
                 float burn, out float3 tint)
{
    float ndv = saturate(dot(N, viewDir));
    float rim = pow(1.0 - ndv, max(_bladeRimPower, 0.05));

    /* --- the body: two stones dealt across the crown --------------- */
    float3 body  = lerp(_colorBladeBody.rgb,  _colorBladeBodyDeep.rgb,  tone);
    float3 facet = lerp(_colorBladeFacet.rgb, _colorBladeFacetDeep.rgb, tone);
    tint = lerp(body, facet, ndv * 0.85);

    /* --- the two sharp edges --------------------------------------- */
    // The section's width axis is where the blade closes to a line, so the
    // edge term is the same cosine the geometry was built on. It is what
    // draws the silhouette against a dark stage, and the reason the crown
    // reads as cut glass rather than as a pile of cones.
    float edge = pow(abs(cos(bladeA * CTAU)), max(_bladeEdgePower, 0.5));
    edge *= smoothstep(0.02, 0.2, bladeT);

    /* --- the flaws the mark runs in -------------------------------- */
    // Blade-local, so the veins belong to this blade and turn with it rather
    // than sliding through a field pinned to the world.
    float3 vp = float3(bladeT * _bladeVeinScale, bladeA * _bladeVeinBands, seed * 13.0);
    float vein = pow(saturate(ridged(vp, 4)), max(_bladeVeinSharp, 0.2));
    vein *= smoothstep(0.05, 0.4, bladeT);

    /* --- lit by the heart it grew out of ---------------------------- */
    float3 toHeart = _Centre.xyz - worldPos;
    float reach = 1.0 - smoothstep(0.0, max(_bladeHeartReach, 0.05), length(toHeart));
    float facing = saturate(dot(N, normalize(toHeart + 1e-4)));
    float heart = reach * reach * (0.25 + 0.75 * facing);

    /* --- the point --------------------------------------------------- */
    float tip = smoothstep(clamp(_bladeTipStart, 0.0, 0.98), 1.0, bladeT);

    float wound = 1.0 + _Charge * _bladeChargeGain + _Pulse * 0.35 + heat;

    float3 glow = _colorBladeEdge.rgb * edge * _bladeEdgeGlow;
    glow += _colorBladeEdge.rgb * rim * _bladeRim;
    glow += _colorBladeVein.rgb * vein * _bladeVein;
    glow += _colorHeart.rgb * heart * _bladeHeartBleed;
    glow += _colorBladeHot.rgb * tip * _bladeTipGlow;
    glow *= wound;
    glow += _colorBladeHot.rgb * burn * _bladeBurnGlow;
    glow *= _bladeGlow * _Fade * _GlobalGlow;

    // Soft ceiling, and it has to be a hard one. Every term above peaks
    // somewhere on the silhouette and they stack: unrolled, a facet turned
    // edge-on sums past ten, the blade goes white, and the whole burst is a
    // star-shaped hole in the frame wearing the bloom pass. What the reference
    // sheet shows is a *dark* blade with a lit edge, so the ceiling is set
    // where a blade can still take the sun on its facets and only the edge is
    // allowed to run away.
    return glow / (1.0 + glow * 1.6);
}

// The faceted normal. The source material is flatShaded, which makes three
// throw the interpolated normal away and rebuild it per face. Eight facets
// each taking the sun on their own is what reads as *cut*, so rebuild it the
// same way here rather than interpolating the analytic one.
float3 bladeFlatNormal(float3 worldPos)
{
    return normalize(cross(ddy(worldPos), ddx(worldPos)));
}

#endif
