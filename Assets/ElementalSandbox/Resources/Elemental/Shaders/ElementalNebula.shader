// AstralNebulaMaterial.js — the sheared violet nebula wrapped round the void.
//
// Like the acid gas, the port ran this through the shared Elemental/Volume and
// lost everything that gives it structure: the spiral arms, the golden spears,
// the doppler beaming and the oblate envelope. The comments kept from the
// source are the ones that explain why a term is shaped the way it is — most of
// them are about the rotation axis, where every angular term is singular and
// aliases into a bright vertical bar straight down the middle of the hole.
Shader "Elemental/Nebula"
{
 Properties{
  _ColorEdge("Edge",Color)=(.18,.08,.34,1) _ColorBody("Body",Color)=(.55,.27,.94,1)
  _ColorHot("Hot",Color)=(1,.69,.23,1) _ColorCore("Core",Color)=(1,.95,.82,1)
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

 #define MAX_STEPS 72
 #define TAU 6.28318530718
 // The proxy is a polyhedron and the field can wander a little past _Radius, so
 // the analytic bound is generous. Cheap: an empty span costs one profile test.
 #define REACH_MARGIN 1.14

 CBUFFER_START(UnityPerMaterial)
 float4 _ColorEdge,_ColorBody,_ColorHot,_ColorCore;
 float3 _Centre;
 float _Radius,_Hole,_Steps,_Density,_Absorb,_Emissive,_Scale,_Detail,_Filament,_Threshold;
 float _Cavity,_Edge,_Flatten,_Arms,_ArmSharp,_ArmWeight,_Wind,_Twist,_Spin,_Rise;
 float _Spikes,_SpikeSharp,_SpikeReach,_SpikeGlow,_HeatFalloff,_Beam;
 float _Churn,_Drain,_Seed,_Fade,_Opacity;
 CBUFFER_END

 // rn — distance from the middle in oblate units, 1 at the outer wall.
 // bearing — its angle in the frame the gas is turning in, which is what makes
 //   a plain angular ripple come out as a spiral.
 // up — how far off the disc plane it is, 0..1.
 float shellProfile(float3 q,out float rn,out float bearing,out float up)
 {
  float squash=max(_Flatten,.05);
  float3 qf=float3(q.x,q.y/squash,q.z);
  rn=length(qf)/max(_Radius,1e-3);
  // The true distance as well, because the eye below has to be a sphere even
  // though the envelope is a disc.
  float sphereN=length(q)/max(_Radius,1e-3);
  up=saturate(abs(q.y)/max(_Radius*squash,1e-3));

  // Draining pulls the outer wall in toward the eye rather than fading the
  // cloud where it hangs: the gas goes down the hole, it does not evaporate.
  float outer=lerp(1.0,_Cavity*1.3,_Drain);
  float wall=1.0-smoothstep(outer*_Edge,outer,rn);
  // The eye, and the one part of this shape that is a sphere rather than a
  // disc. Measured in the oblate metric the void is squashed, so above and
  // below the hole the gas comes to within half a metre of it and every grazing
  // ray picks some up — which the beaming then draws as a bright vertical bar
  // down the middle of the shadow. The inner edge is floored strictly outside
  // the hole whatever the editor says, and the ramp out of it is long, because
  // a short one paints a hard circle round the hole.
  float holeN=_Hole/max(_Radius,1e-3);
  float inner=max(_Cavity*.55,holeN*1.25);
  float eye=smoothstep(inner,max(_Cavity*1.15,inner+.06),sphereN);

  // The shear that makes the arms spiral. The constant under _Wind stops the
  // winding rate blowing up toward the middle; drop it and the arms wrap
  // several times inside the inner third and resolve as concentric rings — a
  // vinyl record, not a galaxy.
  float turn=_SandboxTime*_Spin*TAU+_Wind/(.45+rn*1.2)+_Twist*(q.y/max(_Radius,1e-3));
  bearing=atan2(q.z,q.x)-turn;
  return wall*eye;
 }

 void sampleGas(float3 p,out float d,out float heat,out float3 orbit)
 {
  d=0;heat=0;orbit=float3(0,0,1);
  float3 q=p-_Centre;
  float rn,bearing,up;
  float shell=shellProfile(q,rn,bearing,up);
  if(shell<=.002)return;

  // How far this parcel is from the rotation axis, 0 on it. Every angular term
  // below is singular there — bearing, arms, spears and orbit are all built on
  // atan2(q.z,q.x), undefined on the axis and swinging through a full turn in
  // the millimetres either side. Left alone they alias into a dead-straight
  // bright column standing on the hole. Each fade is also simply true: a parcel
  // on the axis is not in an arm, not on a ray, and not going round anything.
  float axis=smoothstep(0,.22,length(q.xz)/max(_Radius,1e-3));

  // The sampling frame turns with the gas, so the structure is carried round
  // rather than the cloud sliding through a field pinned to the world. It has
  // to be the same turn the arms were measured in, twist included.
  float turn=_SandboxTime*_Spin*TAU+_Wind/(.45+rn*1.2)+_Twist*(q.y/max(_Radius,1e-3));
  float2 xz=mul(rot2(turn),q.xz);

  float3 np=float3(xz*_Scale,q.y*_Scale*1.35-_SandboxTime*_Rise+_Seed);
  float cloud=0,amp=.5;
  [unroll]for(int i=0;i<3;i++){cloud+=amp*snoise(np);np=np*2.11+float3(17.3,5.9,23.7);amp*=.5;}
  cloud=cloud*.5+.5;

  // Ridged detail. Gas thrown out of something is filament and void, never a
  // uniform haze, and this is the octave that carries the violence.
  float fil=1.0-abs(snoise(float3(xz,q.y*.7)*_Scale*_Detail+_Seed*3.0));
  float n=lerp(cloud,fil,_Filament);

  // The arms, gating the density, evaluated in the rotating frame so the radial
  // shear bends them into spirals. Toward the axis the gate fades to the value
  // it averages, not to 1: the gate is at most 1, so an axis exempted from it
  // sits permanently at the arms' peak while everything around it oscillates,
  // which draws a pale column standing on the hole.
  float armGate=pow(.5+.5*cos(bearing*_Arms),_ArmSharp);
  float armMean=1.0/(1.0+_ArmSharp);
  float arms=lerp(armMean,armGate,axis);
  n*=lerp(1.0,arms,_ArmWeight);

  float carved=smoothstep(_Threshold,1.0,n);

  // The golden spears, measured on the unwound bearing. The arms are wound by a
  // rate that varies with radius, which curls them into spirals; a spear on the
  // same frame would curl with them and stop being a spear. Turned slowly and
  // rigidly instead, these stay straight — and straight is what makes them read
  // as ejecta seen end-on rather than as more swirl.
  float raw=atan2(q.z,q.x)-_SandboxTime*_Spin*TAU*.35+_Seed;
  float ray=pow(abs(cos(raw*_Spikes*.5)),_SpikeSharp);
  ray*=1.0-smoothstep(_SpikeReach*.25,_SpikeReach,rn);
  // Strongest in the disc plane, because they are thrown along the equator.
  ray*=(1.0-up*.8)*axis;

  // The spears are gas, not a tint on it: they thicken the cloud where they
  // run, which stops them reading as a lens flare pasted over it.
  d=(carved+ray*_SpikeGlow*.8)*shell*_Density*(1.0+_Churn*.5);

  // Radius sets the ceiling; density decides how much of it this parcel gets.
  // The density term is the important half — without it every wisp near the
  // middle is gold and the cloud comes out as one cream disc. With it the gold
  // lives in the dense cores and the thin gas stays violet.
  float radial=pow(1.0-saturate(rn),_HeatFalloff);
  heat=radial*(.18+.82*saturate(carved*1.6));
  heat=clamp(heat+ray*_SpikeGlow,0,2);

  // Which way this parcel travels: a circular orbit about the vertical. Faded
  // around the axis on a much wider radius than the rest, because this one
  // flips sign across it — and most of the light on a ray comes from the dense
  // gas nearest the hole, so the flip would draw a hard vertical edge splitting
  // the blast rather than a soft bright/dim gradient.
  float beamFade=smoothstep(.15,.62,length(q.xz)/max(_Radius,1e-3));
  orbit=normalize(float3(-q.z,0,q.x)+1e-5)*beamFade;
 }

 // Entry and exit through a sphere. Correct with the camera inside.
 bool sphereSpan(float3 ro,float3 rd,float3 ce,float rad,out float t0,out float t1)
 {
  t0=0;t1=0;
  float3 oc=ro-ce;
  float b=dot(oc,rd),c=dot(oc,oc)-rad*rad,disc=b*b-c;
  if(disc<0)return false;
  float s=sqrt(disc);t0=-b-s;t1=-b+s;return t1>0;
 }

 struct A{float4 positionOS:POSITION;};
 struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;};
 V vert(A i){V o;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.wp);return o;}

 half4 frag(V i):SV_Target
 {
  float3 ro=_WorldSpaceCameraPos;
  float3 rd=normalize(i.wp-ro);
  float ta,tb;
  if(!sphereSpan(ro,rd,_Centre,_Radius*REACH_MARGIN,ta,tb))discard;
  float t0=max(ta,0),t1=tb;

  // The hole eats whatever is behind it. Analytic rather than a depth test,
  // because the horizon is a transparent billboard with no depth of its own.
  float h0,h1;
  if(sphereSpan(ro,rd,_Centre,_Hole,h0,h1))
  {
   if(h0<=0)discard;   // the camera is inside the horizon
   t1=min(t1,h0);
  }

  // And the opaque scene clips it like anything else.
  float2 screenUV=i.positionCS.xy/_ScaledScreenParams.xy;
  float raw=SampleSceneDepth(screenUV);
  float3 scenePos=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
  t1=min(t1,dot(scenePos-ro,rd));
  if(t1<=t0)discard;

  float steps=clamp(_Steps,6,MAX_STEPS);
  float dt=(t1-t0)/steps;
  float jitter=hash13(float3(i.positionCS.xy,_SandboxTime*60.0));
  float t=t0+dt*jitter;

  float3 acc=0;float trans=1;
  [loop]for(int s=0;s<MAX_STEPS;s++)
  {
   if(s>=steps||trans<.012)break;
   float3 p=ro+rd*t;
   float d,heat;float3 orbit;
   sampleGas(p,d,heat,orbit);
   if(d>.002)
   {
    // Doppler beaming: the limb sweeping toward the camera is brighter than the
    // limb sweeping away. It is the term that says fast.
    float beam=1.0+_Beam*dot(orbit,-rd);
    float3 col=lerp(_ColorEdge.rgb,_ColorBody.rgb,smoothstep(0,.30,heat));
    col=lerp(col,_ColorHot.rgb,smoothstep(.38,.88,heat));
    // White is reserved for the very hottest cores. Widen this and the middle
    // of the blast blows out into a cream disc with no hue left to correct.
    col=lerp(col,_ColorCore.rgb,smoothstep(1.0,1.75,heat));
    // The floor matters as much as the peak: lower, and the coldest gas
    // contributes almost nothing but still carries its alpha, so the outer
    // cloud becomes a ring of black smoke — occlusion with no light in it.
    float emit=_Emissive*(.32+heat*heat*2.4)*max(beam,.05);
    float a=1.0-exp(-d*_Absorb*dt);
    acc+=col*emit*a*trans;
    trans*=1.0-a;
   }
   t+=dt;
  }

  float alpha=(1.0-trans)*_Fade*_Opacity;
  if(alpha<.004)discard;
  acc*=_Fade;
  return half4(acc,alpha);
 }
 ENDHLSL}
 }
}
