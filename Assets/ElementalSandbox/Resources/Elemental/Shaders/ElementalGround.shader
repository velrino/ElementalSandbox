// Ground.js — the stage floor.
//
// The source is a MeshStandardMaterial with a patched map/roughness chunk, so
// it gets the full lighting rig: sun and shadows, the image-based probe, and
// the specular break-up that `floorSheen` drives. This runs the same three
// steps over URP's BRDF rather than a hand-rolled lambert, which is what the
// stone was missing — with no environment term a dark floor stays dead flat
// whatever the lights do.
Shader "Elemental/Ground"
{
 Properties{
  _BaseColor("Floor",Color)=(.01,.014,.02,1) _Tint("Tint",Color)=(.017,.024,.035,1)
  _MainTex("Rock",2D)="white"{} _NormalTex("Rock normal",2D)="bump"{} _RoughTex("Rock roughness",2D)="white"{} _AoTex("Rock AO",2D)="white"{}
  _Textured("Use texture",Float)=0 _Pool("Pool",Float)=.8 _Sheen("Sheen",Float)=.34
  _Roughness("Roughness",Float)=.88 _NormalScale("Normal scale",Float)=.85 _TexTint("Texture tint",Float)=.4 _TexScale("Metres per tile",Float)=12
 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
 #pragma multi_compile_fog
 #include "ElementalCommon.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 TEXTURE2D(_NormalTex);SAMPLER(sampler_NormalTex);
 TEXTURE2D(_RoughTex);SAMPLER(sampler_RoughTex);
 TEXTURE2D(_AoTex);SAMPLER(sampler_AoTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_Tint;float _Textured,_Pool,_Sheen,_Roughness,_NormalScale,_TexTint,_TexScale;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;float3 wn:TEXCOORD1;float fog:TEXCOORD2;};
 V vert(A i){V o;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.wn=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.wp);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 half4 frag(V i):SV_Target
 {
  float3 wp=i.wp;float2 uv=wp.xz/max(.1,_TexScale);
  float3 albedo;float rough=_Roughness,ao=1;float3 normal=normalize(i.wn);
  if(_Textured>.5)
  {
   // The stone albedo graded toward the stage tint without losing its value:
   // normalising the tint to unit luminance shifts hue and leaves brightness
   // to the light pool below.
   float3 stone=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
   float3 tint=_Tint.rgb;float tl=max(1e-4,dot(tint,float3(.299,.587,.114)));
   albedo=lerp(stone,stone*(tint/tl),saturate(_TexTint));
   rough*=SAMPLE_TEXTURE2D(_RoughTex,sampler_RoughTex,uv).g;
   ao=SAMPLE_TEXTURE2D(_AoTex,sampler_AoTex,uv).g;
   // The plane is flat and axis-aligned, so tangent space is world XZ.
   float3 n=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,uv),_NormalScale);
   normal=normalize(float3(n.x,n.z,n.y));
  }
  else
  {
   // No texture: the original procedural dark stone. Broad, smooth variation
   // with a warmer wash drifting through it — anything higher frequency reads
   // as gravel and fights the clean look.
   float macro=fbm(wp*.018);
   float3 base=lerp(_BaseColor.rgb,_Tint.rgb,smoothstep(-.5,.6,macro*2-1)*.5);
   base*=1+fbm(wp*.09+11)*.05;
   base*=1+(noise3(wp*.7)-.5)*.06;
   albedo=base;
  }
  // Radial light pool: the stage centre stays readable and the floor sinks
  // toward the backdrop long before the plane's edge.
  float pool=lerp(1,smoothstep(40,5,length(wp.xz)),saturate(_Pool));
  albedo*=lerp(.18,1,pool);
  // Break the sheen up: broad patches of smoother stone catch the key light
  // and the elemental glows, the rest stays matte.
  float polish=smoothstep(.3,.85,fbm(wp*.06+3));
  rough*=lerp(1,.45,polish*saturate(_Sheen));

  InputData data=(InputData)0;
  data.positionWS=wp;
  data.normalWS=normal;
  data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(wp);
  data.shadowCoord=TransformWorldToShadowCoord(wp);
  data.fogCoord=i.fog;
  data.bakedGI=SampleSH(normal);
  data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
  data.shadowMask=half4(1,1,1,1);

  SurfaceData surface=(SurfaceData)0;
  surface.albedo=albedo;
  surface.smoothness=saturate(1-rough);
  surface.occlusion=ao;
  surface.alpha=1;
  half4 lit=UniversalFragmentPBR(data,surface);

  // The casts' lights arrive as URP additional lights through the BRDF above,
  // the same way they reach the stone, the character and the targets.
  return half4(MixFog(lit.rgb,i.fog),1);
 }
 ENDHLSL}
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
