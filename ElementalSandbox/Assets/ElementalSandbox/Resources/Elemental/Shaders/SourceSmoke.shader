Shader "Elemental/SourceSmoke" {
Properties { _Opacity("Opacity",Float)=.08 }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "ElementalCommon.hlsl"
#include "SourceNoise.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
float _Opacity;
struct A {float4 pos:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float3 world:TEXCOORD1;};
V vert(A i){V o;o.world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(o.world);o.uv=i.uv;o.color=i.color;return o;}
half4 frag(V i):SV_Target {
 float2 p=i.uv*2-1;float radial=1-smoothstep(.2,1,length(p));float noise=fbm4(float3(p*2.7+float2(i.color.r*13,i.color.b*19),_SandboxTime*.2))*.5+.5;
 float alpha=radial*smoothstep(.18,.72,noise)*i.color.a*_Opacity;
 float sceneDepth=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.pos)),_ZBufferParams);float eye=-TransformWorldToView(i.world).z;alpha*=saturate((sceneDepth-eye)/.6);
 return half4(i.color.rgb*(.75+noise*.5),alpha);
}
ENDHLSL }
}}
