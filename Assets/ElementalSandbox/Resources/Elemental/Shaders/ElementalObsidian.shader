// ObsidianMaterial.js — the ward's monoliths: volcanic glass with lava sealed
// in its fractures.
//
// The source is a flat-shaded MeshStandardMaterial (roughness glassRough,
// metalness 0) with the look injected at <emissivemap_fragment>. The vein is
// the zero crossing of an fbm field, read twice at two depths along the view
// ray so it sits *inside* the glass rather than painted on it — the same
// construction the meteor's lava seams use, because it is what a fracture looks
// like: meandering, forked, never a scratch. The per-instance seed the source
// carries as an attribute is derived here from the instance's translation.
Shader "Elemental/Obsidian"
{
 Properties{
  _ColorRock("Obsidian",Color)=(.11,.08,.09,1) _ColorChar("Char",Color)=(.03,.016,.023,1)
  _ColorVein("Vein",Color)=(1,.18,.06,1) _ColorVeinCore("Vein core",Color)=(1,.86,.65,1)
  _Opacity("Opacity",Range(0,1))=1 _Roughness("Roughness",Range(0,1))=.42
 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"} Cull Off
 Pass{Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
 #pragma multi_compile_fog
 #include "ElementalCommon.hlsl"
 #include "SourceNoise.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _ColorRock,_ColorChar,_ColorVein,_ColorVeinCore;
 float _Opacity,_Roughness;
 float _VeinScale,_VeinWidth,_VeinBranches,_VeinDepth,_VeinGlow,_VeinFlow,_VeinFlowSpeed;
 float _FlashY,_FlashWidth,_FlashGain,_FacetTint,_Cavity,_RimLight,_Fade,_Glow;
 CBUFFER_END

 // How much vein is at q, and how close to its middle. x = the vein, y = its
 // hot middle. A second, finer octave supplies the twigs.
 float2 veinField(float3 q)
 {
  float f1=fbm3(q*_VeinScale);
  float f2=fbm3(q*_VeinScale*2.7+11.3);
  float d=min(abs(f1),abs(f2)/max(_VeinBranches,.05));
  float body=1.0-smoothstep(_VeinWidth*.4,_VeinWidth,d);
  float core=1.0-smoothstep(0,_VeinWidth*.4,d);
  return float2(body,core);
 }

 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;float3 local:TEXCOORD1;float seed:TEXCOORD2;float fog:TEXCOORD3;};
 V vert(A i)
 {
  UNITY_SETUP_INSTANCE_ID(i);V o;
  o.local=i.positionOS.xyz;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.wp);
  // aSeed: one number per monolith, stable for its lifetime.
  o.seed=hash13(float3(UNITY_MATRIX_M._m03,UNITY_MATRIX_M._m13,UNITY_MATRIX_M._m23)*.37);
  o.fog=ComputeFogFactor(o.positionCS.z);return o;
 }

 half4 frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
 {
  clip(_Opacity-hash31(floor(i.wp*90))*.98);
  // flatShading: the facet normal, from screen derivatives.
  float3 N=normalize(cross(ddy(i.wp),ddx(i.wp)))*IS_FRONT_VFACE(front,1,-1);
  float3 V=GetWorldSpaceNormalizeViewDir(i.wp);
  float ndv=saturate(dot(N,V));
  float rim=pow(1.0-ndv,2.4);
  float seed=i.seed;

  /* --- the glass --- */
  float mottle=fbm3(i.local*3.6+seed*31.0)*.5+.5;
  float3 rock=lerp(_ColorRock.rgb,_ColorChar.rgb,smoothstep(.25,.9,mottle));
  float3 faceN=normalize(cross(ddx(i.local),ddy(i.local)));
  float facet=hash13(faceN*37.0+seed+.5);
  rock*=1.0+(facet-.5)*_FacetTint;
  float cavity=smoothstep(.35,.9,length(i.local.xz)*2.0);
  rock*=lerp(1.0-_Cavity,1.0,cavity);
  rock*=lerp(.5,1.15,ndv);

  /* --- what is trapped in it --- */
  float2 nearV=veinField(i.wp-V*_VeinDepth*.55);
  float2 farV=veinField(i.wp-V*_VeinDepth*1.5+float3(7.1,3.3,5.9));
  float veins=nearV.x+farV.x*.45;
  float core=nearV.y+farV.y*.3;
  veins*=lerp(.28,1.0,ndv);
  core*=lerp(.28,1.0,ndv);
  float pulse=snoise(i.wp*2.2+float3(0,_SandboxTime*_VeinFlowSpeed,0)+seed*7.0);
  veins*=lerp(1.0,.4+.8*(pulse*.5+.5),_VeinFlow);
  float wave=exp(-pow((i.wp.y-_FlashY)/max(_FlashWidth,.05),2.0))*_FlashGain;
  float3 glow=lerp(_ColorVein.rgb,_ColorVeinCore.rgb,saturate(core));
  glow*=veins*_VeinGlow*(1.0+wave);
  glow+=_ColorVein.rgb*rim*_RimLight*(.35+wave*.65);
  glow*=_Glow*_Fade;
  // Soft ceiling: every term above peaks at a grazing angle and they stack.
  glow/=1.0+glow*.22;

  InputData d=(InputData)0;d.positionWS=i.wp;d.normalWS=N;d.viewDirectionWS=V;d.shadowCoord=TransformWorldToShadowCoord(i.wp);d.fogCoord=i.fog;d.bakedGI=SampleSH(N);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
  SurfaceData s=(SurfaceData)0;s.albedo=rock;s.metallic=0;s.specular=.04;s.smoothness=saturate(1-_Roughness);s.normalTS=float3(0,0,1);s.occlusion=1;s.emission=glow;s.alpha=1;
  half4 col=UniversalFragmentPBR(d,s);
  return half4(MixFog(col.rgb,i.fog),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
