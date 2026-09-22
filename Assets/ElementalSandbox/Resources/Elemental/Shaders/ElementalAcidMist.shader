// ToxicMistMaterial.js — the raymarched column of toxic gas over the acid pool.
//
// The port ran this through the shared Elemental/Volume: 35 lines, one generic
// box march, five abilities sharing it, and two of the sixteen mist parameters
// actually read. The result was a soft gradient where the source has a plume.
// This is the source's own march, with the four things that give it form:
//
//  1. The proxy is only a scissor. Back faces, depth test off; the shape is
//     resolved analytically so the mesh's tessellation is irrelevant.
//  2. The span is analytic — cylinderSpan solves the ray against an upright
//     cylinder, which is correct with the camera inside the cloud.
//  3. It is clipped against the scene depth, so a body standing in the gas is
//     veiled by the gas in front of it and not the gas behind it. That line is
//     what makes this an aura rather than a decal.
//  4. It is lit from underneath, because the pool is the key light and it is
//     below the gas. Light it flat and it stops being smoke over a chemical
//     fire and becomes green fog.
Shader "Elemental/AcidMist"
{
 Properties{
  _ColorDeep("Deep",Color)=(.06,.18,.03,1) _ColorBody("Body",Color)=(.24,.55,.08,1)
  _ColorEdge("Edge",Color)=(.62,.95,.24,1) _ColorGlow("Ground glow",Color)=(.45,1,.12,1) _ColorLight("Key",Color)=(.5,.6,.34,1)
 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
 // Premultiplied, because acc is already an integral weighted by its own alpha.
 Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #include "ElementalCommon.hlsl"
 #include "SourceNoise.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

 #define MAX_STEPS 64

 CBUFFER_START(UnityPerMaterial)
 float4 _ColorDeep,_ColorBody,_ColorEdge,_ColorGlow,_ColorLight;
 float3 _Centre;
 float _Radius,_Height,_Steps,_Density,_Absorb,_Scale,_Detail,_Filament,_Threshold;
 float _Rise,_Stretch,_Twist,_Spin,_Edge,_Flare,_Falloff,_Skirt,_Lobe,_Tear;
 float _GroundGlow,_GroundFalloff,_Shadow,_ShadowStep,_Ambient,_Saturate;
 float _Boil,_Dissolve,_Seed,_Fade,_Opacity,_Glow,_Inner;
 CBUFFER_END
 float3 _ElementalLightDir;

 // Where the ray is inside the column, and how far up it is. The shape is a
 // cylinder that opens with height, so the radial test is against the local
 // radius: the cloud is a chimney, not a tube.
 float shellProfile(float3 q,out float h)
 {
  h=saturate(q.y/max(_Height,1e-3));
  float rr=_Radius*(1.0+_Flare*h);
  // The wall is not turned on a lathe. Its radius wanders with bearing and
  // height, weighted upward because the gas is held at the floor by the pool
  // and loose above it. Without this the cloud is a can of gas however good the
  // noise inside it is — the single thing that gives a volume away.
  float2 dir=normalize(q.xz+1e-5);
  float lobe=snoise(float3(dir*1.6,q.y*.4-_SandboxTime*_Rise*.35+_Seed));
  rr*=1.0+lobe*_Lobe*(.2+.8*h);
  float r=length(q.xz)/max(rr,1e-3);
  // Soft wall, and a skirt that keeps the gas hugging the floor just past the
  // boundary instead of stopping dead on it.
  float wall=smoothstep(1.0+_Skirt*(1.0-h),_Edge,r);
  // A self-cast leaves the caster standing in clear air: no gas inside _Inner.
  wall*=smoothstep(_Inner*.7,_Inner,r);
  return wall*pow(1.0-h,_Falloff);
 }

 // Three octaves, twisted about the axis. The shape of the gas.
 float mistNoise(float3 q,float h)
 {
  // The column turns as it climbs and drifts as a whole: a vortex, which is
  // what a rising plume off a hot floor does. Sampled after the twist so the
  // structures are welded to the gas rather than swimming through it.
  float turn=_Twist*h+_SandboxTime*_Spin;
  float2 xz=mul(rot2(turn),q.xz);
  // A low stretch squashes the sampling axis, which elongates the features
  // along it: a plume climbing rather than fog drifting.
  float3 np=float3(xz*_Scale,q.y*_Scale*_Stretch-_SandboxTime*_Rise+_Seed);
  float n=0,a=.5;
  [unroll]for(int i=0;i<3;i++){n+=a*snoise(np);np=np*2.03+float3(17.3,5.1,9.7);a*=.5;}
  n=n*.5+.5;
  // Ridged detail folded in: filaments and holes, so the cloud has wisps torn
  // out of it instead of being a uniform fog with a gradient on it.
  float fil=1.0-abs(snoise(float3(xz,q.y*.6)*_Scale*_Detail+_Seed*3.0));
  return lerp(n,fil,_Filament);
 }

 float densityAt(float3 p)
 {
  float3 q=p-_Centre;float h;
  float shell=shellProfile(q,h);
  if(shell<=.002)return 0;
  float n=mistNoise(q,h);
  // Carved rather than faded: below the threshold there is simply no gas, and
  // the threshold climbs, so the crown tears into separate wisps while the body
  // at the pool stays solid. A cloud with a lid is a cloud nobody believes.
  float d=smoothstep(_Threshold+_Tear*h,1.0,n)*shell;
  return d*_Density*(1.0+_Boil*(1.4-h));
 }

 // One cheap octave, for the shadow tap only.
 float densityCoarse(float3 p)
 {
  float3 q=p-_Centre;float h;
  float shell=shellProfile(q,h);
  if(shell<=.002)return 0;
  float turn=_Twist*h+_SandboxTime*_Spin;
  float2 xz=mul(rot2(turn),q.xz);
  float n=snoise(float3(xz*_Scale,q.y*_Scale*_Stretch-_SandboxTime*_Rise+_Seed))*.5+.5;
  return smoothstep(_Threshold+_Tear*h,1.0,n)*shell*_Density;
 }

 // Entry and exit distance through the column. Handles the camera being inside
 // it, and a ray running parallel to the axis.
 bool cylinderSpan(float3 ro,float3 rd,out float t0,out float t1)
 {
  t0=0;t1=0;
  float2 oc=ro.xz-_Centre.xz;
  // The widest the chimney ever gets, so the span always contains the shape.
  float rMax=_Radius*(1.0+max(_Flare,0)+max(_Skirt,0)+max(_Lobe,0));
  float a=dot(rd.xz,rd.xz),b=dot(oc,rd.xz),c=dot(oc,oc)-rMax*rMax;
  float tn=-1e9,tf=1e9;
  if(a<1e-7){if(c>0)return false;}
  else{float disc=b*b-a*c;if(disc<0)return false;float s=sqrt(disc);tn=(-b-s)/a;tf=(-b+s)/a;}
  float yb=_Centre.y,yt=_Centre.y+_Height;
  if(abs(rd.y)<1e-7){if(ro.y<yb||ro.y>yt)return false;}
  else{float k0=(yb-ro.y)/rd.y,k1=(yt-ro.y)/rd.y;tn=max(tn,min(k0,k1));tf=min(tf,max(k0,k1));}
  t0=max(tn,0);t1=tf;return t1>t0;
 }

 struct A{float4 positionOS:POSITION;};
 struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;};
 V vert(A i){V o;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.wp);return o;}

 half4 frag(V i):SV_Target
 {
  float3 ro=_WorldSpaceCameraPos;
  float3 rd=normalize(i.wp-ro);
  float t0,t1;
  if(!cylinderSpan(ro,rd,t0,t1))discard;

  // Stop the march at the opaque scene: the far end is cut at the depth
  // buffer, which is what veils a body standing in the gas correctly.
  float2 screenUV=i.positionCS.xy/_ScaledScreenParams.xy;
  float raw=SampleSceneDepth(screenUV);
  float3 scenePos=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
  // scenePos sits on this same ray, so its distance along rd needs no cosine.
  t1=min(t1,dot(scenePos-ro,rd));
  if(t1<=t0)discard;

  float steps=clamp(_Steps,4,MAX_STEPS);
  float dt=(t1-t0)/steps;
  // Jittered start. Without it the march bands into visible shells, and the
  // banding is the most obvious tell that a volume is stepped.
  float jitter=hash13(float3(i.positionCS.xy,_SandboxTime*60.0));
  float t=t0+dt*jitter;

  float3 acc=0;float trans=1;
  [loop]for(int s=0;s<MAX_STEPS;s++)
  {
   if(s>=steps||trans<.012)break;
   float3 p=ro+rd*t;
   float d=densityAt(p);
   if(d>.002)
   {
    // The pool is the key light, and it is underneath the gas.
    float lift=exp(-_GroundFalloff*(p.y-_Centre.y));
    float3 fromBelow=_ColorGlow.rgb*lift*_GroundGlow;
    // One tap toward the sun. A full shadow march would cost as much as the
    // volume itself for a term the eye only reads as "the top is darker".
    float above=densityCoarse(p+_ElementalLightDir*_ShadowStep);
    float3 fromAbove=_ColorLight.rgb*exp(-_Shadow*above);
    // Thin gas is bright and yellow, thick gas is deep and saturated — the
    // whole reason a cloud has form instead of being a silhouette.
    float3 body=lerp(_ColorEdge.rgb,_ColorDeep.rgb,saturate(d*_Saturate));
    body=lerp(body,_ColorBody.rgb,.5);
    float a=1.0-exp(-d*_Absorb*dt);
    acc+=body*(fromBelow+fromAbove+_Ambient)*a*trans;
    trans*=1.0-a;
   }
   t+=dt;
  }

  float alpha=(1.0-trans)*_Fade*_Opacity;
  // The collapse thins the gas from the top down: it sinks back into the pool
  // rather than blinking out.
  alpha*=1.0-saturate(_Dissolve);
  if(alpha<.004)discard;
  acc*=_Glow;
  return half4(acc,alpha);
 }
 ENDHLSL}
 }
}
