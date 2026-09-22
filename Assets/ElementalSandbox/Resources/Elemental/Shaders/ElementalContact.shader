// ContactShadows.js — the three passes that make the blob under the caster.
//
// The source renders the character's depth from below into a small target,
// blurs it twice and projects it on the floor. Pass 0 is its patched
// MeshDepthMaterial, passes 1 and 2 are the addon blur shaders with the same
// nine taps and weights, and pass 3 is the catcher plane.
//
// Passes 0-2 are driven by hand (a command buffer and two blits) and carry a
// LightMode URP does not collect, so attaching this material to the catcher's
// MeshRenderer draws the catcher alone.
Shader "Elemental/Contact"
{
 Properties{_MainTex("Source",2D)="black"{} _Height("Frustum height",Float)=3.2 _Darkness("Darkness",Float)=.8 _Blur("Blur",Float)=.0094 _Opacity("Opacity",Float)=.55}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_ST;float _Height,_Darkness,_Blur,_Opacity;
 CBUFFER_END
 struct VF{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 VF vertFull(uint id:SV_VertexID)
 {
  VF o;float2 uv=float2((id<<1)&2,id&2);o.uv=uv;o.positionCS=float4(uv*2-1,0,1);
  #if UNITY_UV_STARTS_AT_TOP
  o.positionCS.y=-o.positionCS.y;
  #endif
  return o;
 }
 half4 blur9(float2 uv,float2 step)
 {
  half4 sum=0;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-step*4)*.051;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-step*3)*.0918;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-step*2)*.12245;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-step)*.1531;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*.1633;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+step)*.1531;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+step*2)*.12245;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+step*3)*.0918;
  sum+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+step*4)*.051;
  return sum;
 }
 ENDHLSL

 // 0 — depth from below, written straight into alpha.
 Pass{Name "ContactDepth" Tags{"LightMode"="ElementalContactDepth"}
 ZWrite Off ZTest Always Cull Off Blend Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float d:TEXCOORD0;};
 V vert(A i){V o;float3 wp=TransformObjectToWorld(i.positionOS.xyz);o.d=saturate(-TransformWorldToView(wp).z/max(.001,_Height));o.positionCS=TransformWorldToHClip(wp);return o;}
 half4 frag(V i):SV_Target{return half4(0,0,0,(1-i.d)*_Darkness);}
 ENDHLSL}

 // 1 / 2 — the separable blur, run twice at two radii.
 Pass{Name "ContactBlurH" Tags{"LightMode"="ElementalContactBlurH"}
 ZWrite Off ZTest Always Cull Off Blend Off
 HLSLPROGRAM
 #pragma vertex vertFull
 #pragma fragment frag
 half4 frag(VF i):SV_Target{return blur9(i.uv,float2(_Blur,0));}
 ENDHLSL}

 Pass{Name "ContactBlurV" Tags{"LightMode"="ElementalContactBlurV"}
 ZWrite Off ZTest Always Cull Off Blend Off
 HLSLPROGRAM
 #pragma vertex vertFull
 #pragma fragment frag
 half4 frag(VF i):SV_Target{return blur9(i.uv,float2(0,_Blur));}
 ENDHLSL}

 // 3 — the catcher lying on the floor.
 Pass{Name "ContactCatcher" Tags{"LightMode"="UniversalForward"}
 ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.uv=i.uv;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
 half4 frag(V i):SV_Target{return half4(0,0,0,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*_Opacity);}
 ENDHLSL}
 }
}
