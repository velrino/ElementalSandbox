// InkVolumeMaterial.js — the pigment suspended in the water column.
//
// Structurally the acid gas's sibling, with three things that are its own: a
// funnel with an eye up the middle, differential winding (the turn rises toward
// the axis, which is what a vortex does — a constant turn is a spin, and a spun
// cylinder of noise looks exactly like one), and it is lit from *above*, since
// daylight comes down through water. It also stands back from whatever the tide
// is holding, so a body is seen through a parting in the pigment.
Shader "Elemental/InkVolume"
{
 Properties{
  _ColorDeep("Deep",Color)=(.016,.027,.04,1) _ColorBody("Body",Color)=(.07,.21,.24,1)
  _ColorEdge("Edge",Color)=(.31,.56,.57,1) _ColorLight("Daylight",Color)=(.75,.9,.88,1)
 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
 Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #include "ElementalCommon.hlsl"
 #include "SourceNoise.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

 #define MAX_STEPS 64
 #define MAX_CLEAR 4
 #define TAU 6.28318530718

 CBUFFER_START(UnityPerMaterial)
 float4 _ColorDeep,_ColorBody,_ColorEdge,_ColorLight;
 float3 _Centre;
 float _Radius,_Height,_Steps,_Density,_Absorb,_Scale,_Detail,_Filament,_Threshold;
 float _Rise,_Stretch,_Twist,_Spin,_Wind,_Funnel,_Edge,_Flare,_Skirt,_Falloff,_Lobe,_Tear;
 float _Light,_Shadow,_ShadowStep,_Ambient,_Saturate;
 float _Swell,_Drain,_Seed,_Fade,_Opacity;
 float _ClearCount,_Clear,_ClearFade;
 CBUFFER_END
 float3 _ElementalLightDir;
 // The bodies the tide has hold of: world centre in xyz, metres in w.
 float4 _Clears[MAX_CLEAR];

 // Two masks, not one: a wall that opens with height, and a hole up the middle
 // that closes with it. The hole is the throat the pool has opened on the floor
 // carried up into the volume — without it the ink fills the axis and the
 // vortex has no eye.
 float shellProfile(float3 q,out float h,out float rn)
 {
  h=saturate(q.y/max(_Height,1e-3));
  float rr=_Radius*(1.0+_Flare*h);
  // The wall is not turned on a lathe: its radius wanders with bearing and
  // height. Without this the volume is a can of ink however good the noise
  // inside it is, which is the one thing that gives a march away.
  float2 dir=normalize(q.xz+1e-5);
  float lobe=snoise(float3(dir*1.7,q.y*.35-_SandboxTime*_Rise*.4+_Seed));
  rr*=1.0+lobe*_Lobe*(.25+.75*h);
  rn=length(q.xz)/max(rr,1e-3);
  float wall=smoothstep(1.0+_Skirt*(1.0-h),_Edge,rn);
  // The eye of the vortex. Widest at the floor, closed over by the crown.
  float hole=_Funnel*(1.0-h*.85);
  float eye=smoothstep(hole*.35,hole,rn);
  return wall*eye*pow(1.0-h,_Falloff);
 }

 float inkNoise(float3 q,float h,float rn)
 {
  // Differential rotation: the turn rises toward the axis, which is what a
  // vortex does.
  float turn=_Twist*h+_SandboxTime*_Spin*TAU+_Wind/(.25+rn);
  float2 xz=mul(rot2(turn),q.xz);
  // A low stretch squashes the sampling axis, elongating features along it:
  // strands hanging rather than a cloud sitting.
  float3 np=float3(xz*_Scale,q.y*_Scale*_Stretch-_SandboxTime*_Rise+_Seed);
  float n=0,a=.5;
  [unroll]for(int i=0;i<3;i++){n+=a*snoise(np);np=np*2.07+float3(13.1,7.7,21.3);a*=.5;}
  n=n*.5+.5;
  // Ridged detail: ink in water is filament and void, never a uniform haze.
  float fil=1.0-abs(snoise(float3(xz,q.y*.55)*_Scale*_Detail+_Seed*3.0));
  return lerp(n,fil,_Filament);
 }

 float densityAt(float3 p)
 {
  float3 q=p-_Centre;float h,rn;
  float shell=shellProfile(q,h,rn);
  if(shell<=.002)return 0;
  float n=inkNoise(q,h,rn);
  // Carved rather than faded, and the threshold climbs, so the top tears into
  // separate strands while the body at the surface stays solid.
  float d=smoothstep(_Threshold+_Tear*h,1.0,n)*shell;
  // The swell pushes the whole volume, hardest at its feet; the drain pulls it
  // back down into the throat rather than fading it out on the spot.
  return d*_Density*(1.0+_Swell*(1.2-h))*(1.0-_Drain*h);
 }

 float densityCoarse(float3 p)
 {
  float3 q=p-_Centre;float h,rn;
  float shell=shellProfile(q,h,rn);
  if(shell<=.002)return 0;
  float turn=_Twist*h+_SandboxTime*_Spin*TAU+_Wind/(.25+rn);
  float2 xz=mul(rot2(turn),q.xz);
  float n=snoise(float3(xz*_Scale,q.y*_Scale*_Stretch-_SandboxTime*_Rise+_Seed))*.5+.5;
  return smoothstep(_Threshold+_Tear*h,1.0,n)*shell*_Density;
 }

 bool cylinderSpan(float3 ro,float3 rd,out float t0,out float t1)
 {
  t0=0;t1=0;
  float2 oc=ro.xz-_Centre.xz;
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

  float2 screenUV=i.positionCS.xy/_ScaledScreenParams.xy;
  float raw=SampleSceneDepth(screenUV);
  float3 scenePos=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
  t1=min(t1,dot(scenePos-ro,rd));
  if(t1<=t0)discard;

  // Stand the ink back from whatever the tide is holding. Solved once per ray
  // rather than per sample: which held body this ray passes through, how far
  // along and how squarely — none of that moves as the march advances.
  // openTo is the distance the ink has to be out of the way to, openBy how
  // completely; feathered, because a hard-edged hole in a volume reads as a
  // hole and nothing else.
  float openTo=0,openBy=0;
  int clears=(int)_ClearCount;
  [loop]for(int k=0;k<MAX_CLEAR;k++)
  {
   if(k>=clears)break;
   float3 toBody=_Clears[k].xyz-ro;
   float along=dot(toBody,rd);
   if(along<=0)continue;
   float miss=length(toBody-rd*along);
   float inside=1.0-smoothstep(_Clears[k].w*.45,_Clears[k].w*1.25,miss);
   if(inside<=0)continue;
   openTo=max(openTo,along);openBy=max(openBy,inside);
  }
  openBy*=_Clear;

  float steps=clamp(_Steps,4,MAX_STEPS);
  float dt=(t1-t0)/steps;
  float jitter=hash13(float3(i.positionCS.xy,_SandboxTime*60.0));
  float t=t0+dt*jitter;

  float3 acc=0;float trans=1;
  [loop]for(int s=0;s<MAX_STEPS;s++)
  {
   if(s>=steps||trans<.012)break;
   float3 p=ro+rd*t;
   float d=densityAt(p);
   // Only the ink in front of the body is moved. Behind it the volume is
   // untouched, so the corpse is seen through a parting in the pigment rather
   // than in a tube cut out of it.
   if(openBy>0)d*=1.0-openBy*(1.0-smoothstep(openTo-_ClearFade,openTo,t));
   if(d>.002)
   {
    // Daylight comes down through the water, so one tap toward the sun is the
    // right approximation — and it gives a strand a lit side and a shadow side
    // instead of a flat silhouette.
    float above=densityCoarse(p+_ElementalLightDir*_ShadowStep);
    float3 sun=_ColorLight.rgb*exp(-_Shadow*above)*_Light;
    // Thin ink is a teal wash, thick ink is black. That gradient is the whole
    // of the form: without it the volume is one silhouette.
    float3 body=lerp(_ColorEdge.rgb,_ColorDeep.rgb,saturate(d*_Saturate));
    body=lerp(body,_ColorBody.rgb,.5);
    float a=1.0-exp(-d*_Absorb*dt);
    acc+=body*(sun+_Ambient)*a*trans;
    trans*=1.0-a;
   }
   t+=dt;
  }

  float alpha=(1.0-trans)*_Fade*_Opacity;
  if(alpha<.004)discard;
  // Premultiplied, which is also what lets near-black ink darken the frame
  // instead of tinting it.
  return half4(acc,alpha);
 }
 ENDHLSL}
 }
}
