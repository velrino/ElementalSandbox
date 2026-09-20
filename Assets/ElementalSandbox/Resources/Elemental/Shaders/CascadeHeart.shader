// Ported from createCascadeHeartMaterial in src/materials/CascadeBladeMaterial.js.
// Layer 3c — the heart the blades converge on.
Shader "Elemental/CascadeHeart"
{
    Properties
    {
        _heartBoil("heartBoil",Float)=0.13
        _heartBoilScale("heartBoilScale",Float)=2.4
        _heartFill("heartFill",Float)=1.6
        _heartRim("heartRim",Float)=0.9
        _heartRimPower("heartRimPower",Float)=2.2
        _heartFilament("heartFilament",Float)=1
        _heartFilamentScale("heartFilamentScale",Float)=4.2
        _heartFilamentSpeed("heartFilamentSpeed",Float)=0.55
        _heartIntensity("heartIntensity",Float)=0.5
        _heartChargeGain("heartChargeGain",Float)=1.5
        _heartSoftFade("heartSoftFade",Float)=0.4
        _colorHeartCore("colorHeartCore",Color)=(1,1,1,1)
        _colorHeart("colorHeart",Color)=(0.5,0.97,0.9,1)
        _colorHeartEdge("colorHeartEdge",Color)=(0.06,0.44,0.47,1)
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

            float _heartBoil, _heartBoilScale, _heartFill, _heartRim, _heartRimPower;
            float _heartFilament, _heartFilamentScale, _heartFilamentSpeed;
            float _heartIntensity, _heartChargeGain, _heartSoftFade;
            float4 _colorHeartCore, _colorHeart, _colorHeartEdge;
            float _Seed, _Charge, _Fade, _Pulse, _GlobalGlow;

            struct A { float4 pos:POSITION; float3 normal:NORMAL; };
            struct V
            {
                float4 pos:SV_POSITION;
                float3 normal:TEXCOORD0;
                float3 view:TEXCOORD1;
                float viewZ:TEXCOORD2;
            };

            V vert(A i)
            {
                V o;
                float3 n = normalize(i.normal);
                // The silhouette churns rather than sitting still: a sphere
                // with a clean outline at this size reads as a bead, and what
                // the panel wants is something with pressure in it.
                float d = snoise(n * _heartBoilScale + float3(0, _SandboxTime * 0.7, _Seed * 5.0));
                float3 here = i.pos.xyz * (1.0 + d * _heartBoil * (1.0 + _Charge * 0.6));

                float3 world = TransformObjectToWorld(here);
                o.normal = TransformObjectToWorldNormal(n);
                o.view = normalize(_WorldSpaceCameraPos - world);
                float4 mv = mul(GetWorldToViewMatrix(), float4(world, 1.0));
                o.viewZ = mv.z;
                o.pos = TransformWorldToHClip(world);
                return o;
            }

            half4 frag(V i):SV_Target
            {
                float ndv = saturate(dot(normalize(i.normal), normalize(i.view)));
                // Weighted toward the axis: the path a view ray takes through a
                // shell is longest looking straight at the middle, so that is
                // where the white goes.
                float core = pow(ndv, max(_heartFill, 0.05));
                float rim = pow(1.0 - ndv, max(_heartRimPower, 0.05)) * _heartRim;

                float3 fp = normalize(i.normal) * _heartFilamentScale
                          + float3(0, _SandboxTime * _heartFilamentSpeed, _Seed * 9.0);
                float threads = pow(saturate(ridged(fp, 4)), 3.0) * _heartFilament;

                float wound = 1.0 + _Charge * _heartChargeGain + _Pulse * 0.3;
                float energy = (core + rim + threads) * wound;

                float3 color = lerp(_colorHeartEdge.rgb, _colorHeart.rgb, saturate(core * 1.4));
                color = lerp(color, _colorHeartCore.rgb, saturate(pow(core, 2.2) + _Charge * 0.4));
                color *= energy * _heartIntensity * _Fade;

                float alpha = saturate(energy * 0.85) * _Fade;
                if (_heartSoftFade > 1e-4)
                {
                    float2 screen = i.pos.xy / _ScaledScreenParams.xy;
                    float sceneView = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                    alpha *= saturate((sceneView + i.viewZ) / _heartSoftFade);
                }
                if (alpha < 0.004) discard;

                color *= _GlobalGlow;
                color /= 1.0 + color * 0.1;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
