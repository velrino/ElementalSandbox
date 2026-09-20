// Ported from GROWTH_PATH in src/materials/GrowthVineMaterial.js.
// The tendril centre line every growth layer is hung off: the tube, the leaves
// that sit on it and the wither that eats it back all read this.
#ifndef GROWTH_PATH_INCLUDED
#define GROWTH_PATH_INCLUDED

#define GTAU 6.283185307179586
#define GPI  3.141592653589793

float4 _Centre;
float _Seed, _Grow, _Wither, _Radius;

float _vines, _vineStagger, _vineSeat, _vineSpread, _vineHeight, _vineHeightJitter;
float _vineRise, _vineBelly, _vineLean, _vineTwist;
float _vineCurlAt, _vineCurlTurns, _vineCurlPinch, _vineCurlLift;
float _vineWander, _vineWanderScale, _vineSway, _vineSwaySpeed;
float _vineThick, _vineTaper, _vineKnots, _vineKnotScale;

// One tendril's dice: bearing scatter, height, handedness, phase. Four hashes
// of the instance index and the cast's seed, and they are the whole of a stem's
// identity — which is why re-seeding a cast reshuffles the nest without
// touching a single control.
float4 vineDice(float vine)
{
    return float4(
        hash11(vine * 7.13 + _Seed * 3.11),
        hash11(vine * 3.71 + _Seed * 5.77 + 11.3),
        hash11(vine * 11.9 + _Seed * 2.33 + 27.1),
        hash11(vine * 5.17 + _Seed * 9.71 + 41.9));
}

// How far *this* stem has grown. The nest has to come up ragged: fifteen
// tendrils that leave the floor on the same frame read as one object opening,
// which is exactly what a summon must not look like.
float vineGrowth(float vine)
{
    float lag = clamp(vineDice(vine).w * _vineStagger, 0.0, 0.85);
    return saturate((_Grow - lag) / max(1.0 - lag, 1e-3));
}

// A point on a tendril's centre line. t is 0 at the foot, 1 at the tip.
float3 vinePoint(float vine, float t)
{
    float4 d = vineDice(vine);
    float u = saturate(t);

    float bearing = ((vine + (d.x - 0.5) * _vineSpread) / max(_vines, 1.0)) * GTAU;
    float hand = d.z < 0.5 ? -1.0 : 1.0;

    float foot = _Radius * _vineSeat * (0.75 + 0.5 * d.y);
    float height = _vineHeight * (1.0 - _vineHeightJitter * 0.5 + _vineHeightJitter * d.y);

    // How far out the stem is at this height: planted out at the ring, bowed
    // past it at the waist, drawn back in under the bloom. The bow is what
    // makes the nest read as a *cage* rather than as a cone of sticks.
    float radial = lerp(foot, foot * _vineLean, smoothstep(0.0, 1.0, u));
    radial += foot * _vineBelly * sin(u * GPI);

    float twist = bearing + hand * _vineTwist * u * GTAU;

    // The tip curls, and that one term is what says "grown" instead of
    // "extruded": the last stretch spirals inward and lifts as it tightens.
    float c = smoothstep(_vineCurlAt, 1.0, u);
    twist += hand * _vineCurlTurns * c * c * GTAU;
    radial *= lerp(1.0, _vineCurlPinch, c);

    float y = height * pow(u, max(_vineRise, 0.05)) + height * _vineCurlLift * c;
    float3 p = _Centre.xyz + float3(cos(twist) * radial, y, sin(twist) * radial);

    // Wander (baked into the stem) and sway (alive), both weighted up the stem
    // so the foot stays planted in the floor and only the tip moves.
    float phase = d.w * 31.7;
    p.x += snoise(float3(u * _vineWanderScale, phase, _Seed)) * _vineWander * u;
    p.z += snoise(float3(u * _vineWanderScale + 17.1, phase, _Seed + 5.3)) * _vineWander * u;
    p.x += sin(_SandboxTime * _vineSwaySpeed + phase) * _vineSway * u * u;
    p.z += sin(_SandboxTime * _vineSwaySpeed * 0.73 + phase * 1.7) * _vineSway * u * u;

    return p;
}

// Half-width of the stem at t, metres. grow is the stem's own front: the taper
// that draws a finished tendril to a point has to follow the front while it is
// still climbing, or a half-grown stem ends in a flat disc hanging in the air.
float vineRadius(float vine, float t, float grow)
{
    float4 d = vineDice(vine);
    float u = saturate(t);

    float r = _vineThick * (0.72 + 0.56 * d.y) * lerp(1.0, _vineTaper, pow(u, 0.75));
    // Knots. A stem is not a machined rod, and the lumps are most of what sells
    // the wood at the silhouette.
    r *= 1.0 + _vineKnots * snoise(float3(u * _vineKnotScale, d.w * 19.0, _Seed));
    // Written forwards (edge0 < edge1) rather than as a reversed smoothstep:
    // reversed edges are undefined, and this is evaluated with grow at zero on
    // the frame before a cast starts.
    r *= 1.0 - smoothstep(grow - 0.07, grow, u);
    return max(r, 1e-5);
}

// The stem's local frame at t. The reference axis is the stem's own outward
// radial, taken from the point itself — a smooth function of t by construction.
// That matters more than it looks: crossing the tangent with world up *flips*
// somewhere up a tendril that passes through vertical, and a frame that flips
// between two rows of a tube twists every quad between them into a bow tie.
void vineFrame(float vine, float t, float3 here, out float3 tangent, out float3 n1, out float3 n2)
{
    float step_ = 0.012;
    float ahead = t + step_;
    float flip = 1.0;
    if (ahead > 1.0) { ahead = t - step_; flip = -1.0; }

    tangent = (vinePoint(vine, ahead) - here) * flip;
    tangent = dot(tangent, tangent) > 1e-12 ? normalize(tangent) : float3(0, 1, 0);

    float3 outward = here - float3(_Centre.x, here.y, _Centre.z);
    if (dot(outward, outward) < 1e-8) outward = float3(1, 0, 0);
    outward = normalize(outward);

    n2 = cross(tangent, outward);
    if (dot(n2, n2) < 1e-10) n2 = cross(tangent, float3(0, 0, 1));
    n2 = normalize(n2);
    n1 = normalize(cross(n2, tangent));
}

/* ---------------------------------------------------------------- */
/* the wither                                                        */
/* ---------------------------------------------------------------- */

float _witherRise, _witherScale, _witherEdge, _witherEdgeGlow;

// Opaque the whole way rather than a fading alpha, so the wood never has to
// sort against the membrane of leaves around it — and so the burn edge can be
// emissive instead of transparent. It eats from the *tip* down, which is the
// order a plant actually dies in and, more usefully, the order that keeps the
// silhouette legible longest: the nest thins before it shortens.
float growthWither(float3 world, float t, float seed, out float edge)
{
    edge = 0.0;
    if (_Wither <= 0.0) return 1.0;
    float n = fbm3(world * _witherScale + seed * 37.0) * 0.5 + 0.5;
    float keep = lerp(n, 1.0 - t, saturate(_witherRise));
    edge = 1.0 - smoothstep(0.0, max(_witherEdge, 1e-3), keep - _Wither);
    return keep - _Wither;
}

#endif
