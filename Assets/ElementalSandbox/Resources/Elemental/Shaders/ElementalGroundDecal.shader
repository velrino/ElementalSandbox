// GroundDecals.js — the marks a cast leaves on the floor.
//
// Eight kinds behind one fragment, selected by _Type, exactly as the source
// compiles one ShaderMaterial per DecalType. The first port had no decal
// system at all, and the rift's DUSTRING marks — seeded every 1/scarRate
// metres along the travelling front, at the cast, and at the impact — are the
// broad pale wash the source's frames carry across the whole floor. Muting the
// source's particles left that wash in place; this is where it comes from.
Shader "Elemental/GroundDecal"
{
 Properties{
  _Type("Type",Float)=4 _Age("Age 0..1",Float)=0 _Seed("Seed",Float)=0 _Intensity("Intensity",Float)=1
  _Width("Width",Float)=.12 _Radius("Radius metres",Float)=2 _ColorA("A",Color)=(1,1,1,1) _ColorB("B",Color)=(1,1,1,1) _Additive("Additive",Float)=0 [HideInInspector]_SrcBlend("Src",Float)=5 [HideInInspector]_DstBlend("Dst",Float)=10
 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent"}
 ZWrite Off Cull Off
 // NormalBlending for the dust, foam and frost; the hot kinds are additive.
 // The pair is set per material from SourceDecals.
 Blend [_SrcBlend] [_DstBlend]
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 #include "SourceNoise.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _ColorA,_ColorB;float _Type,_Age,_Seed,_Intensity,_Width,_Radius,_Additive;
 CBUFFER_END
 float3 _ElementalLightDir;
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 light:TEXCOORD1;};
 V vert(A i)
 {
  V o;o.uv=i.uv;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
  // The sun, in the decal's own frame, for the frost's relief.
  float3 ax=normalize(UNITY_MATRIX_M._m00_m10_m20),ay=normalize(UNITY_MATRIX_M._m01_m11_m21),az=normalize(UNITY_MATRIX_M._m02_m12_m22);
  float3 L=normalize(_ElementalLightDir);o.light=normalize(float3(dot(L,ax),dot(L,ay),-dot(L,az)));
  return o;
 }
 float snowDepth(float2 q,float seed,float sharpness)
 {
  float drift=fbm3(float3(q*.85,seed))*.5+.5;
  float2 cell=voronoi2(q*(1.4+sharpness*.9)+seed*7.0);
  float slabs=smoothstep(0,.55,cell.x)*.30+cell.y*.12;
  float grain=snoise01(float3(q*(7.0+sharpness*5.0),seed*3.0))*.15;
  return drift*.60+slabs+grain;
 }
 half4 frag(V i):SV_Target
 {
  float2 c=(i.uv-.5)*2.0;float d=length(c);
  if(d>1.0)discard;
  float alpha=0;float3 color=_ColorA.rgb;float t=_SandboxTime;float age=_Age;
  float fadeOut=1.0-smoothstep(.55,1.0,age);
  int type=(int)(_Type+.5);
  if(type==0){ /* SCORCH */
   float n=fbm3(float3(c*2.4,_Seed*13.0));float burn=smoothstep(1.0,.15,d+n*.45);
   float embers=pow(max(0,snoise(float3(c*6.0,_Seed*9.0+t*.35))),4.0);
   alpha=burn*(.85*fadeOut);color=lerp(_ColorA.rgb,_ColorB.rgb,embers*(1.0-age));color+=embers*_ColorB.rgb*2.5*(1.0-smoothstep(0,.6,age));
  }else if(type==1){ /* RIPPLE */
   float radius=lerp(.05,1.0,sqrt(age));float ring=smoothstep(_Width,0,abs(d-radius));float inner=smoothstep(radius,radius-.35,d)*.22;
   float wobble=.75+.25*snoise(float3(c*5.0,_Seed*4.0+t));alpha=(ring*wobble+inner)*fadeOut;color=lerp(_ColorA.rgb,_ColorB.rgb,ring);
  }else if(type==2){ /* CRACK */
   float ang=atan2(c.y,c.x);float branch=ridged(float3(cos(ang),sin(ang),_Seed*5.0)*2.6,4);
   float spread=smoothstep(0,.45,age);float radial=smoothstep(spread,spread*.35,d);
   float crack=smoothstep(.55-_Width*.35,.85,branch)*radial;float glow=crack*(1.0-smoothstep(.1,.8,age));
   alpha=saturate(crack*.95*fadeOut);color=lerp(_ColorA.rgb,_ColorB.rgb,glow);color+=_ColorB.rgb*glow*1.8;
  }else if(type==3){ /* SHOCKWAVE */
   float radius=pow(age,.55);float ring=smoothstep(_Width,0,abs(d-radius));alpha=ring*(1.0-age)*.9;color=lerp(_ColorA.rgb,_ColorB.rgb,ring);
  }else if(type==4){ /* DUSTRING */
   float radius=lerp(.1,1.0,pow(age,.4));float n=fbm3(float3(c*3.1,_Seed*7.0+t*.2));
   float puff=smoothstep(radius,radius*.35,d)*(.6+n*.5);alpha=puff*(1.0-age)*.7;color=lerp(_ColorA.rgb,_ColorB.rgb,n*.5+.5);
  }else if(type==6){ /* FROST */
   float seed=_Seed*37.0;float sharp=clamp(_Width,.05,4.0);float2 q=c*max(.35,_Radius);
   float2 warp=float2(fbm3(float3(q*.55,seed)),fbm3(float3(q*.55,seed+5.7)))*.45;
   float lobes=fbm3(float3(q*.8+warp,seed+13.0));float grow=pow(age,.30);float reach=d*(1.0-lobes*.40);
   float cover=smoothstep(grow,grow-.38,reach);if(cover<.004)discard;
   float e=.16;float h=snowDepth(q,seed,sharp),hx=snowDepth(q+float2(e,0),seed,sharp),hy=snowDepth(q+float2(0,e),seed,sharp);
   float3 nrm=normalize(float3((h-hx)/e*.30,1.0,(h-hy)/e*.30));float lambert=saturate(dot(nrm,normalize(i.light)));
   float shade=.36+.64*pow(lambert,.8);float lie=smoothstep(.10,.52,cover*(.34+.78*h));
   alpha=lie*fadeOut*.95;color=lerp(_ColorB.rgb*.55,lerp(_ColorA.rgb,1.0,.45),shade);
   float glint=smoothstep(.90,1.0,snoise01(float3(q*9.0,floor(t*7.0)*.37+seed)));color+=glint*pow(lambert,2.0)*1.5*(1.0-smoothstep(0,.7,age));
   float lip=smoothstep(.10,0,abs(reach-grow))*(1.0-smoothstep(0,.5,age));color=lerp(color,lerp(_ColorB.rgb,1.0,.6),lip*.55);alpha=saturate(alpha+lip*cover*.25*fadeOut);
  }else if(type==5){ /* FOAM */
   float front=lerp(.12,1.0,pow(age,.42));float n=fbm3(float3(c*2.6,_Seed*17.0+t*.12));float ang=atan2(c.y,c.x);
   float fingers=.68+.32*snoise(float3(cos(ang),sin(ang),_Seed*3.0)*3.5);float edge=d+n*.26;float reach=front*fingers;
   float sheet=smoothstep(reach,reach-.3,edge);float drain=smoothstep(reach*age*.9-.05,reach*age+.3,edge+n*.12);
   float foam=sheet*lerp(1.0,drain,smoothstep(.12,.85,age));float2 cell=voronoi2(c*9.0+_Seed*30.0);float bubbles=smoothstep(.55,.04,cell.x);
   float rim=smoothstep(.09,0,abs(edge-reach))*(1.0-age);float wet=sheet*(1.0-smoothstep(.3,1.0,age));
   float mask=saturate(foam*(.5+.65*bubbles)+rim*.9);alpha=saturate(wet*.5+mask)*fadeOut;color=lerp(_ColorA.rgb,_ColorB.rgb,saturate(mask*1.3));
  }else{ /* ARC */
   float warp=fbm3(float3(c*1.7,_Seed*3.0))*.5;float fil=ridged(float3(c*(2.4+_Width*4.0)+warp,_Seed*11.0),4);
   float veins=smoothstep(.70,.96,fil);float grow=pow(age,.35);float edge=d+fbm3(float3(c*2.2,_Seed*5.0))*.25;
   float front=smoothstep(grow,grow*.15,edge);float hot=veins*front*(1.0-smoothstep(0,.45,age));
   alpha=saturate(veins*front*1.1)*fadeOut;color=lerp(_ColorA.rgb,_ColorB.rgb,saturate(veins*1.4));color+=_ColorB.rgb*hot*1.8;
  }
  alpha*=_Intensity;
  if(alpha<.004)discard;
  // Additive kinds: One One with the colour premultiplied by alpha.
  return _Additive>.5?half4(color*alpha,alpha):half4(color,alpha);
 }
 ENDHLSL}
 }
}
