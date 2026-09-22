// ParticleSystem.js, SMOKE shape — the aerosol both eruptions throw.
//
// The rift's dust is created with `lit: true` and `softFade: 1.6`: a cheap
// wrapped diffuse against the key light so a puff has a lit side and a shadow
// side ("a genuine torus of lit, non-additive smoke"), and a long soft-particle
// fade so a cloud hugging the floor is not cut off by it. The first port had
// neither — unlit sprites and a 0.6 m fade — and its dust read as flat grey
// stamps. _Lit and _SoftFade carry the two per-system choices.
Shader "Elemental/SourceSmoke" {
Properties { _Opacity("Opacity",Float)=.08 _SoftFade("Soft fade metres",Float)=1.6 _Lit("Lit",Float)=1 }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "ElementalCommon.hlsl"
#include "SourceNoise.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
float _Opacity,_SoftFade,_Lit;
float3 _ElementalLightDir;
// TEXCOORD0.z is the renderer's StableRandomX stream: the source's per-particle
// vSeed, so each puff gets its own edge rather than one shared with everything
// born on the same frame.
struct A {float4 pos:POSITION;float3 uv:TEXCOORD0;float4 color:COLOR;};
struct V {float4 pos:SV_POSITION;float3 uv:TEXCOORD0;float4 color:COLOR;float3 world:TEXCOORD1;};
V vert(A i){V o;o.world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(o.world);o.uv=i.uv;o.color=i.color;return o;}
half4 frag(V i):SV_Target {
 // shapeMask, SMOKE: a plateau at .9 whose edge is pushed in and out by one
 // fbm octave. The port's radial-times-noise-gate opened holes through the
 // body of every puff, which is where most of its alpha went.
 float2 p=i.uv.xy*2-1;float d=length(p);float seed=i.uv.z;
 float n=fbm3(float3(p*1.6,seed*21.0+_SandboxTime*.25));
 float e=saturate((1.0-(d+n*.42))/.95);float mask=e*e*(3.0-2.0*e)*.9;
 float noise=n*.5+.5;
 float alpha=mask*i.color.a*_Opacity;
 float sceneDepth=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.pos)),_ZBufferParams);float eye=-TransformWorldToView(i.world).z;alpha*=saturate((sceneDepth-eye)/max(_SoftFade,.0001));
 float3 color=i.color.rgb*(.75+noise*.5);
 // vNormalish = (quad xy, .75), in view space: a shallow dome standing on the
 // billboard, so the side of the puff facing the sun is the lit one.
 float3 nrm=normalize(mul((float3x3)UNITY_MATRIX_I_V,normalize(float3(p,.75))));
 float ndl=dot(nrm,normalize(_ElementalLightDir))*.5+.5;
 color*=lerp(1.0,lerp(.45,1.25,ndl),saturate(_Lit));
 return half4(color,alpha);
}
ENDHLSL }
}}
