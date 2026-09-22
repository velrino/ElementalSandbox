// The circle a self cast traces before it rises.
//
// A signed-distance ring on a floor quad, revealed by bearing from where the
// caster faces up to _Progress of a full turn, with a flare on the head that
// runs just ahead of the seed. The guard rings get this beat from their
// eruption front; the summons get it here, and only once the ring has closed
// does the effect open.
Shader "Elemental/SweepArc"
{
 Properties{_Color("Colour",Color)=(.5,1,.3,1) _Progress("Progress",Float)=0 _Phase("Phase",Float)=0 _Width("Width metres",Float)=.09 _QuadSize("Quad size",Float)=10 _Radius("Radius metres",Float)=4 _Glow("Glow",Float)=1.6 _Fade("Fade",Float)=1}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Blend One One ZWrite Off Cull Off
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 #define TAU 6.28318530718
 CBUFFER_START(UnityPerMaterial)
 float4 _Color;float _Progress,_Phase,_Width,_QuadSize,_Radius,_Glow,_Fade;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.uv=i.uv;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
 half4 frag(V i):SV_Target
 {
  // Floor(): the quad is rotated +90 about X, so quad +Y maps to world -Z.
  float2 p=float2(i.uv.x-.5,.5-i.uv.y)*_QuadSize;
  float rad=length(p);float ang=atan2(p.y,p.x);
  // Bearing from the phase, 0..1 round the ring, in the sweep's own turn.
  float turn=frac((ang-_Phase)/TAU);
  float head=_Progress;
  if(turn>head)discard;
  float d=abs(rad-_Radius);
  float aa=max(fwidth(rad),.003);
  float w=max(_Width,aa);
  float core=exp(-pow(d/w,2.0));
  float halo=exp(-d/(w*4.0))*.35;
  // The trail dims behind the head; the head flares.
  float behind=saturate((head-turn)*3.0);
  float trail=lerp(1.0,.45,behind);
  float flare=exp(-pow((head-turn)*14.0,2.0))*1.6;
  float3 col=_Color.rgb*(core*(trail+flare)+halo*trail)*_Glow*_Fade;
  if(dot(col,1.0)<.004)discard;
  return half4(col,1);
 }
 ENDHLSL}
 }
}
