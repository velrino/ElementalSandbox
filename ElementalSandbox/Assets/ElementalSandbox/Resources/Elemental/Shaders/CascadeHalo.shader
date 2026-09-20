// Ported from createCascadeHaloMaterial in src/materials/CascadeBladeMaterial.js.
// Layer 3d — the light the burst throws onto the air behind it.
Shader "Elemental/CascadeHalo"
{
    Properties
    {
        _haloGlow("haloGlow",Float)=0.22
        _haloFalloff("haloFalloff",Float)=2.6
        _haloRays("haloRays",Float)=0.3
        _haloRayCount("haloRayCount",Float)=16
        _haloRaySharp("haloRaySharp",Float)=6
        _haloRaySpin("haloRaySpin",Float)=0.02
        _haloRingSeat("haloRingSeat",Float)=0.62
        _haloRingWidth("haloRingWidth",Float)=0.05
        _colorHaloInner("colorHaloInner",Color)=(0.62,1,0.94,1)
        _colorHaloOuter("colorHaloOuter",Color)=(0.09,0.42,0.49,1)
        _Size("Size",Float)=2
        _Seed("Seed",Float)=0
        _Charge("Charge",Float)=0
        _Fade("Fade",Float)=1
        _Pulse("Pulse",Float)=0
        _GlobalGlow("Global glow",Float)=1
    }

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-1" "RenderType"="Transparent"}
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

            #define HTAU 6.283185307179586

            float _haloGlow, _haloFalloff, _haloRays, _haloRayCount, _haloRaySharp, _haloRaySpin;
            float _haloRingSeat, _haloRingWidth;
            float4 _colorHaloInner, _colorHaloOuter;
            float _Size, _Seed, _Charge, _Fade, _Pulse, _GlobalGlow;

            struct A { float4 pos:POSITION; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.uv = i.pos.xy;
                // Billboarded in view space: the quad's own orientation is
                // thrown away and the corners are offset in the camera's plane,
                // so the halo faces the lens from any angle without the ability
                // having to aim it.
                float4 mv = mul(GetWorldToViewMatrix(), float4(TransformObjectToWorld(float3(0, 0, 0)), 1.0));
                mv.xy += i.pos.xy * _Size;
                o.pos = mul(GetViewToHClipMatrix(), mv);
                return o;
            }

            half4 frag(V i):SV_Target
            {
                float r = length(i.uv) * 2.0;
                if (r > 1.0) discard;

                float body = pow(saturate(1.0 - r), max(_haloFalloff, 0.05));

                // Spokes combed out of the burst. Not evenly bright: a hash per
                // spoke keeps the fan from reading as a machined gear.
                float ang = atan2(i.uv.y, i.uv.x) / HTAU + _SandboxTime * _haloRaySpin;
                float cell = ang * max(_haloRayCount, 1.0);
                float spoke = abs(frac(cell) - 0.5) * 2.0;
                spoke = pow(saturate(1.0 - spoke), max(_haloRaySharp, 0.5));
                spoke *= 0.55 + 0.45 * hash11(floor(cell) + _Seed * 3.0);
                spoke *= smoothstep(1.0, 0.25, r) * smoothstep(0.0, 0.16, r) * _haloRays;

                float ring = 1.0 - smoothstep(0.0, max(_haloRingWidth, 1e-3), abs(r - clamp(_haloRingSeat, 0.05, 0.98)));

                float energy = (body + spoke + ring * 0.7) * (1.0 + _Charge * 1.1 + _Pulse * 0.4);
                float3 color = lerp(_colorHaloOuter.rgb, _colorHaloInner.rgb, saturate(body * 1.6 + spoke));
                color *= energy * _haloGlow * _Fade;

                float alpha = saturate(energy) * _Fade;
                if (alpha < 0.004) discard;

                color *= _GlobalGlow;
                color /= 1.0 + color * 0.14;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
