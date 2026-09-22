// DustMotes.js — the ambient motes floating over the stage.
//
// One draw call: drift, curl, twinkle and volume wrapping all happen in the
// vertex shader, exactly as the source does, so the CPU cost per frame is a
// handful of uniform writes. The source draws GL points; Metal will not size
// points reliably, so each mote is a camera-facing quad whose world size is
// derived from the same pixel size the source asks for (_Size carries
// 2*tan(fov/2)/screenHeight, which makes the two match at any resolution).
Shader "Elemental/Dust"
{
 Properties{_Amount("Amount",Float)=.85 _Size("Pixel to world",Float)=.00002}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass{Tags{"LightMode"="UniversalForward"}
 Blend One One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _Amount,_Size;
 CBUFFER_END
 // snoise stand-in: the shared value noise, recentred to -1..1.
 float sn(float3 p){return noise3(p)*2-1;}
 float3 curlNoise(float3 p)
 {
  const float e=.12;float3 dx=float3(e,0,0),dy=float3(0,e,0),dz=float3(0,0,e);
  float x0=sn(p-dx),x1=sn(p+dx),y0=sn(p-dy),y1=sn(p+dy),z0=sn(p-dz),z1=sn(p+dz);
  float3 pb=p+float3(31.416,47.853,12.793);
  float bx0=sn(pb-dx),bx1=sn(pb+dx),by0=sn(pb-dy),by1=sn(pb+dy),bz0=sn(pb-dz),bz1=sn(pb+dz);
  float inv=1/(2*e);
  return normalize(cross(float3(x1-x0,y1-y0,z1-z0)*inv,float3(bx1-bx0,by1-by0,bz1-bz0)*inv)+1e-5);
 }
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;};
 struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;float seed:TEXCOORD2;};
 V vert(A i)
 {
  V o;float seed=i.seed.x;o.seed=seed;o.uv=i.uv;
  float t=_SandboxTime*(.05+seed*.06);
  float3 p=i.positionOS.xyz;
  // Buoyant rise, wrapped inside the volume the motes were seeded in.
  float h=i.seed.y;
  p.y=fmod(p.y+fmod(_SandboxTime*(.12+seed*.25),h),h);
  p+=curlNoise(p*.06+float3(0,t,0))*1.35;
  float3 wp=TransformObjectToWorld(p);
  float3 vp=TransformWorldToView(wp);
  float dist=-vp.z;
  float twinkle=.55+.45*sin(_SandboxTime*(1.1+seed*2.6)+seed*40);
  o.alpha=twinkle*smoothstep(90,12,dist)*smoothstep(.5,4,dist)*_Amount*.3;
  // gl_PointSize = 20 * (.35 + seed*.9) / dist, in pixels. The 1/dist and the
  // perspective divide cancel, so the source's mote is a fixed world size:
  // 20 * k * 2tan(fov/2) / screenHeight, which is what _Size carries.
  float radius=20*(.35+seed*.9)*_Size*.5;
  vp.xy+=(i.uv-.5)*2*radius;
  o.positionCS=TransformWViewToHClip(vp);
  return o;
 }
 half4 frag(V i):SV_Target
 {
  float2 uv=i.uv-.5;float d=length(uv);
  clip(.5-d);
  float mask=smoothstep(.5,.02,d);
  float3 tint=lerp(float3(1,.93,.78),float3(.78,.9,1),i.seed);
  float a=mask*i.alpha;
  clip(a-.002);
  return half4(tint*a,a);
 }
 ENDHLSL}
 }
}
