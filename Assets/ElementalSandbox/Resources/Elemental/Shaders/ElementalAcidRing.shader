// ToxicMistMaterial.js, createAcidRingMaterial — the ring the whole effect
// stands on, and the element that makes the AoE legible before anything else
// has read.
//
// A signed-distance annulus in metres, so the band keeps its physical width
// when the footprint is re-scaled. Three layers stack: a blown-out core one
// seam wide, a broad halo either side of it, and a wash spilling inward across
// the pool. The core is deliberately allowed to blow past 1 — that is what
// makes it read white against the green rather than as more pool.
Shader "Elemental/AcidRing"
{
 Properties{_ColorRing("Ring",Color)=(.62,1,.17,1) _ColorCore("Core",Color)=(.96,1,.84,1)}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Blend One One ZWrite Off Cull Off
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 #include "SourceNoise.hlsl"

 #define TAU 6.28318530718

 CBUFFER_START(UnityPerMaterial)
 float4 _ColorRing,_ColorCore;
 float _QuadSize,_Radius,_Width,_Core,_Halo,_HaloWidth,_Spill;
 float _Wobble,_WobbleScale,_Chevrons,_ChevronDepth,_Scroll;
 float _Sweep,_SweepSpeed,_SweepWidth,_Ticks,_Boil,_Seed,_Fade,_Opacity,_Glow;
 CBUFFER_END

 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.uv=i.uv;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}

 half4 frag(V i):SV_Target
 {
  float2 p=float2(i.uv.x-.5,.5-i.uv.y)*_QuadSize;
  float rad=length(p);
  float ang=atan2(p.y,p.x);
  float2 dir=rad>1e-4?p/rad:float2(1,0);

  // The far side of the ring is nearly edge-on: one pixel there covers tens of
  // centimetres of floor, where near the camera it covers one.
  float aa=fwidth(rad);
  // A perfect circle reads as UI, so the radius wanders with the bearing — but
  // out on that far arc a three-centimetre wander is a long way across the
  // screen and the ring picks up a notch. Damp it with the footprint: the
  // wobble is only worth having where it can be seen as a wobble.
  float wob=snoise(float3(dir*_WobbleScale,_Seed+_SandboxTime*.12));
  float R=_Radius*(1.0+wob*_Wobble*(1.0-smoothstep(.02,.11,aa)));
  float d=abs(rad-R);

  float surge=1.0+_Boil;

  // Floor the core at the pixel footprint and give back the brightness that
  // widening it cost — the same light over a wider band, which is what a thin
  // bright line at a grazing angle actually does.
  float w0=max(_Width,1e-3)*(1.0+_Boil*.35);
  float w=max(w0,aa*.9);
  float conserve=w0/w;

  float core=exp(-pow(d/w,2.0))*_Core*conserve;
  float halo=exp(-d/max(_HaloWidth,1e-3))*_Halo;
  // Inward only: the wash belongs to the pool, and spilling it outward makes
  // the footprint unreadable.
  float spill=smoothstep(R,R*.35,rad)*_Spill;

  // Energy running round the band.
  float chev=pow(.5+.5*cos(ang*_Chevrons-_SandboxTime*_Scroll*TAU),3.0);
  float ticks=pow(.5+.5*cos(ang*_Ticks),24.0);
  float head=pow(.5+.5*cos(ang-_SandboxTime*_SweepSpeed*TAU),1.0/max(_SweepWidth,1e-3));

  float band=core*(1.0-_ChevronDepth+_ChevronDepth*chev);
  band+=core*ticks*.8;
  band+=core*head*_Sweep;

  float bright=(band+halo*(.6+.4*chev))*surge;
  float3 color=lerp(_ColorRing.rgb,_ColorCore.rgb,saturate(core*.8));
  color=color*bright+_ColorRing.rgb*spill*surge*.6;

  float alpha=clamp(bright+spill,0,3)*_Fade*_Opacity;
  if(alpha<.004)discard;
  color*=_Glow;
  return half4(color,saturate(alpha));
 }
 ENDHLSL}
 }
}
