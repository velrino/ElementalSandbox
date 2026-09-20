// The crown's vertex stage, shared by the colour and shadow passes.
// Included inside each pass rather than from HLSLINCLUDE, so the instancing
// buffer is declared after that pass' #pragma multi_compile_instancing.
#ifndef CASCADE_CROWN_VERTEX_INCLUDED
#define CASCADE_CROWN_VERTEX_INCLUDED

#include "CascadeBlade.hlsl"

float _Inner, _crownSwell;

UNITY_INSTANCING_BUFFER_START(Crown)
    UNITY_DEFINE_INSTANCED_PROP(float4, _CrownDir)
    UNITY_DEFINE_INSTANCED_PROP(float4, _CrownShape)
UNITY_INSTANCING_BUFFER_END(Crown)

// position.x is t along the blade, position.y is a once around the section —
// the mesh carries no metres at all.
void crownVertex(float2 grid, float4 dir4, float4 shape,
                 out float3 world, out float3 nrm,
                 out float t, out float a, out float seed,
                 out float tone, out float live)
{
    float len  = shape.x;
    float roll = shape.y;
    live       = saturate(shape.z);
    tone       = shape.w;

    float3 axis = normalize(dir4.xyz + float3(0, 1e-5, 0));

    // A blade extends out of the heart rather than fading in, and it keeps
    // every one of its samples while it does — the whole buffer compresses
    // into however much of it has grown.
    float reach = len * live;
    // The seat breathes, so the crown opens and closes on the mark's pulse
    // instead of hanging at a fixed radius.
    float seat = _Inner * (1.0 + _Pulse * _crownSwell) * lerp(1.0, 0.55, saturate(_Collapse));
    float3 root = _Centre.xyz + axis * seat;

    t = grid.x;
    a = grid.y;
    world = bladeVertex(root, axis, roll, reach, t, a, nrm);
    seed = frac(roll * 0.15915494 + tone * 3.7);
}

// How a blade is eaten back as the mark collapses. From the point down rather
// than from the root up: a crown that dissolves at its roots leaves forty
// splinters hanging in the air with nothing holding them, while one eaten from
// the tips is visibly being drawn back into the thing that made it. Shared with
// the shadow pass, or a blade half gone goes on laying a whole shadow.
float crownCull(float t, out float burn)
{
    float edge = 1.0 - saturate(_Collapse) * 1.06;
    burn = (1.0 - smoothstep(0.0, 0.09, edge - t)) * step(0.001, _Collapse);
    return edge - t;
}

#endif
