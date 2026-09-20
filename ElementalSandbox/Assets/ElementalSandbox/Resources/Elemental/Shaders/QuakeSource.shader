Shader "Elemental/QuakeSource" { Properties { _breakPale("breakPale",Float)=0.5
_coatDelay("coatDelay",Float)=0.35
_coatTime("coatTime",Float)=2.2
_colorDamp("colorDamp",Color)=(1,1,1,1)
_colorDustCoat("colorDustCoat",Color)=(1,1,1,1)
_colorStone("colorStone",Color)=(1,1,1,1)
_colorStoneDeep("colorStoneDeep",Color)=(1,1,1,1)
_colorStoneGrade("colorStoneGrade",Color)=(1,1,1,1)
_damp("damp",Float)=0.6
_dampHeight("dampHeight",Float)=0.24
_dustCoat("dustCoat",Float)=0.5
_dustCoatScale("dustCoatScale",Float)=1.2
_dustCoatSharp("dustCoatSharp",Float)=1.5
_grime("grime",Float)=0.55
_normalScale("normalScale",Float)=1.2
_stoneAO("stoneAO",Float)=1
_stoneDesat("stoneDesat",Float)=0.35
_stoneGrade("stoneGrade",Float)=0.4
_stoneRough("stoneRough",Float)=1
_stoneRoughFloor("stoneRoughFloor",Float)=0.34
_texAmount("texAmount",Float)=1
_texScale("texScale",Float)=2.6
 _Opacity("Opacity",Float)=1 _Age("Age",Float)=0 _GlobalGlow("Global glow",Float)=1 _Core("Core",Vector)=(0,0,0,0)
 _AlbedoMap("Albedo",2D)="white"{} _NormalMap("Normal",2D)="bump"{} _RoughMap("Roughness",2D)="white"{} _AOMap("AO",2D)="white"{}
 } SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry"} Cull Off 
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
float _breakPale;
float _coatDelay;
float _coatTime;
float4 _colorDamp;
float4 _colorDustCoat;
float4 _colorStone;
float4 _colorStoneDeep;
float4 _colorStoneGrade;
float _damp;
float _dampHeight;
float _dustCoat;
float _dustCoatScale;
float _dustCoatSharp;
float _grime;
float _normalScale;
float _stoneAO;
float _stoneDesat;
float _stoneGrade;
float _stoneRough;
float _stoneRoughFloor;
float _texAmount;
float _texScale;
float _Opacity,_Age,_GlobalGlow;float4 _Core;
CBUFFER_END

TEXTURE2D(_AlbedoMap); SAMPLER(sampler_AlbedoMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_RoughMap); SAMPLER(sampler_RoughMap);
TEXTURE2D(_AOMap); SAMPLER(sampler_AOMap);
#define TRI(tex,sm,p,w) (SAMPLE_TEXTURE2D(tex,sm,p.yz)*w.x+SAMPLE_TEXTURE2D(tex,sm,p.zx)*w.y+SAMPLE_TEXTURE2D(tex,sm,p.xy)*w.z)
half4 frag(V i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {
 clip(i.world.y+.005);clip(_Opacity-hash13(floor(i.world*90))*.98);
 float3 n=normalize(i.normal)*IS_FRONT_VFACE(front,1,-1),w=pow(abs(n),6);w/=max(dot(w,float3(1,1,1)),.0001);float3 p=i.world/max(.05,_texScale);
 float3 sampled=TRI(_AlbedoMap,sampler_AlbedoMap,p,w).rgb;
 float3 fallback=lerp(_colorStoneDeep.rgb,_colorStone.rgb,smoothstep(.25,.8,fbm3(i.world*.6+i.state.x*13)*.5+.5));
 float3 albedo=lerp(fallback,sampled,_texAmount);
 float rough=lerp(.92,TRI(_RoughMap,sampler_RoughMap,p,w).r,_texAmount),ao=lerp(1,TRI(_AOMap,sampler_AOMap,p,w).r,_texAmount);
 float3 nx=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.yz)),ny=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.zx)),nz=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.xy));
 nx.xy*=_normalScale;ny.xy*=_normalScale;nz.xy*=_normalScale;
 nx=float3(nx.xy+n.zy,abs(nx.z)*n.x);ny=float3(ny.xy+n.xz,abs(ny.z)*n.y);nz=float3(nz.xy+n.xy,abs(nz.z)*n.z);
 float3 normal=normalize(lerp(n,normalize(nx.zyx*w.x+ny.xzy*w.y+nz.xyz*w.z),_texAmount));
 float lum=dot(albedo,float3(.299,.587,.114));albedo=lerp(albedo,lum.xxx,_stoneDesat);float3 tint=_colorStoneGrade.rgb/max(.0001,dot(_colorStoneGrade.rgb,float3(.299,.587,.114)));albedo=lerp(albedo,albedo*tint,_stoneGrade);
 albedo*=.78+.44*frac(i.state.x*.618+.31);
 float fresh=saturate(i.face.x);float grey=dot(albedo,float3(.333,.333,.333));float3 pale=lerp(albedo,grey.xxx,.45)*1.35;albedo=lerp(albedo,pale,fresh*_breakPale);
 float streak=fbm3(float3(i.world.xz*2.6,i.world.y*.6))*.5+.5;albedo*=1-(1-fresh)*_grime*streak*.45;
 float damp=1-smoothstep(0,max(.02,_dampHeight),i.face.y);albedo=lerp(albedo,_colorDamp.rgb,damp*_damp);rough*=lerp(1,.78,damp*_damp);ao*=lerp(1,.55,damp*_damp);
 float coat=_dustCoat*saturate((_Age-_coatDelay)/max(.01,_coatTime));float coverage=saturate(pow(saturate(n.y),max(.05,_dustCoatSharp))*coat*(.45+.85*(fbm3(i.world*_dustCoatScale)*.5+.5)));
 albedo=lerp(albedo,_colorDustCoat.rgb,coverage*.88);rough=lerp(max(_stoneRoughFloor,rough*_stoneRough),1,coverage);normal=normalize(lerp(normal,n,coverage));ao=lerp(1,ao,_stoneAO);
 return Shade(i,normal,albedo,rough,ao,0,1);
}

ENDHLSL
}
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
UsePass "Universal Render Pipeline/Lit/DepthOnly"
}
}