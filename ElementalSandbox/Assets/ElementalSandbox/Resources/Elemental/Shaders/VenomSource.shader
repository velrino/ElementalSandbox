Shader "Elemental/VenomSource" { Properties { _birthGlow("birthGlow",Float)=0.9
_cleave("cleave",Float)=0.35
_cleaveScale("cleaveScale",Float)=5
_colorCore("colorCore",Color)=(1,1,1,1)
_colorDeep("colorDeep",Color)=(1,1,1,1)
_colorGem("colorGem",Color)=(1,1,1,1)
_colorGemRim("colorGemRim",Color)=(1,1,1,1)
_colorGemTip("colorGemTip",Color)=(1,1,1,1)
_colorVenom("colorVenom",Color)=(1,1,1,1)
_coreBleed("coreBleed",Float)=0.4
_coreBleedRadius("coreBleedRadius",Float)=3
_depthTint("depthTint",Float)=1.1
_dispersion("dispersion",Float)=0.6
_edgeGlow("edgeGlow",Float)=0.5
_facetSharp("facetSharp",Float)=0.72
_fresnel("fresnel",Float)=2
_fresnelPower("fresnelPower",Float)=2.5
_gemGlow("gemGlow",Float)=0.68
_gemOpacity("gemOpacity",Float)=0.97
_gemRoughness("gemRoughness",Float)=0.12
_glint("glint",Float)=0.6
_glintScale("glintScale",Float)=30
_glintSpeed("glintSpeed",Float)=0.6
_tipFrost("tipFrost",Float)=0.5
_tipStart("tipStart",Float)=0.55
_venomBase("venomBase",Float)=0.25
_venomFlow("venomFlow",Float)=0.5
_venomGlow("venomGlow",Float)=1.15
_venomScale("venomScale",Float)=3.8
_venomSharp("venomSharp",Float)=5.5
 _Opacity("Opacity",Float)=1 _Age("Age",Float)=0 _GlobalGlow("Global glow",Float)=1 _Core("Core",Vector)=(0,0,0,0)
 } SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20"} Cull Off Blend SrcAlpha OneMinusSrcAlpha
Pass { Tags {"LightMode"="UniversalForward"} HLSLPROGRAM

#pragma vertex vert
#pragma fragment frag
#pragma target 4.5
#pragma multi_compile_instancing
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "ElementalCommon.hlsl"
#include "SourceNoise.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
struct A { float4 pos:POSITION; float3 normal:NORMAL; float2 face:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; float2 face:TEXCOORD3; float3 state:TEXCOORD4; };
UNITY_INSTANCING_BUFFER_START(States)
 UNITY_DEFINE_INSTANCED_PROP(float4, _EruptionState)
UNITY_INSTANCING_BUFFER_END(States)
V vert(A i) { UNITY_SETUP_INSTANCE_ID(i); V o; o.world=TransformObjectToWorld(i.pos.xyz); o.pos=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(i.normal);o.local=i.pos.xyz;o.face=i.face;o.state=UNITY_ACCESS_INSTANCED_PROP(States,_EruptionState).xyz;return o; }
half4 Shade(V i,float3 normal,float3 albedo,float rough,float ao,float3 emission,float alpha) {
 InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=normalize(normal);d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=SampleSH(d.normalWS);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.pos);d.shadowMask=1;
 SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.metallic=0;s.specular=.04;s.smoothness=1-saturate(rough);s.normalTS=float3(0,0,1);s.occlusion=ao;s.emission=emission;s.alpha=alpha;
 return UniversalFragmentPBR(d,s);
}

CBUFFER_START(UnityPerMaterial)
float _birthGlow;
float _cleave;
float _cleaveScale;
float4 _colorCore;
float4 _colorDeep;
float4 _colorGem;
float4 _colorGemRim;
float4 _colorGemTip;
float4 _colorVenom;
float _coreBleed;
float _coreBleedRadius;
float _depthTint;
float _dispersion;
float _edgeGlow;
float _facetSharp;
float _fresnel;
float _fresnelPower;
float _gemGlow;
float _gemOpacity;
float _gemRoughness;
float _glint;
float _glintScale;
float _glintSpeed;
float _tipFrost;
float _tipStart;
float _venomBase;
float _venomFlow;
float _venomGlow;
float _venomScale;
float _venomSharp;
float _Opacity,_Age,_GlobalGlow;float4 _Core;
CBUFFER_END

half4 frag(V i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {
 float3 n=normalize(i.normal)*IS_FRONT_VFACE(front,1,-1), view=GetWorldSpaceNormalizeViewDir(i.world);
 float ndv=saturate(dot(n,view)),thickness=saturate(ndv*_depthTint),rim=pow(1-ndv,_fresnelPower),fres=rim*_fresnel,up=saturate(i.local.y),seed=i.state.x;
 float cleave=smoothstep(.52,.97,ridged(i.world*_cleaveScale+seed*41,4));
 float3 vp=i.local*float3(_venomScale*2.2,_venomScale,_venomScale*2.2);vp.y-=_SandboxTime*_venomFlow+i.state.z*6;
 float fluid=pow(saturate(ridged(vp+seed*17,4)),_venomSharp)*lerp(1,_venomBase,up);fluid=saturate(fluid*(.55+1.1*cleave));
 float3 body=lerp(_colorGem.rgb,_colorDeep.rgb,thickness);body=lerp(body,_colorGemRim.rgb,cleave*_cleave*.35);
 float frost=smoothstep(_tipStart,1,up)*(.55+.45*fbm3(i.local*11+seed*5));body=lerp(body,_colorGemTip.rgb,saturate(frost)*_tipFrost);body*=lerp(1,.5+.95*ndv,_facetSharp);
 float3 spread=float3(pow(1-ndv,_fresnelPower*(1-.30*_dispersion)),rim,pow(1-ndv,_fresnelPower*(1+.38*_dispersion)));
 float glint=pow(saturate(snoise(i.world*_glintScale+float3(0,_SandboxTime*_glintSpeed,0)+seed*23)),16)*smoothstep(0,.7,fres+.25);
 float3 toCore=_Core.xyz-i.world;float reach=1-smoothstep(0,max(_coreBleedRadius,.05),length(toCore));float coreLit=reach*reach*(.35+.65*saturate(dot(n,normalize(toCore+.0001))));
 float3 glow=_colorGemRim.rgb*spread*_edgeGlow+_colorVenom.rgb*fluid*_venomGlow+_colorGemRim.rgb*glint*_glint*1.4+_colorCore.rgb*coreLit*_coreBleed+_colorVenom.rgb*i.state.y*_birthGlow;
 glow*=_gemGlow*_GlobalGlow;glow/=1+glow*.42;
 float alpha=saturate(_gemOpacity*(.8+.3*fres)+fluid*.28+frost*.14)*_Opacity;
 clip(i.world.y+.005);
 return Shade(i,n,body,_gemRoughness,1,glow,alpha);
}

ENDHLSL
}
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
UsePass "Universal Render Pipeline/Lit/DepthOnly"
}
}