// The stand-in for the source's patched MeshStandardMaterials: Obsidian (the
// ward's monoliths), VoidShard and RendShard (the shards), the dummy, and the
// serpent. Each source material starts from a real PBR base with its own
// roughness and metalness, flat-shaded where the sheet asks for facets, and
// then injects a look on top through onBeforeCompile.
//
// The first port kept the five looks and dropped the base: it lit everything
// with lambert*.75+.1 plus the SH probe — no specular, no environment
// reflection, no roughness. That is why every solid here read as painted
// plastic next to the source. The five mode blocks below are unchanged; what
// is under them is now UniversalFragmentPBR, fed the source's own roughness,
// metalness and flat-shading through _Roughness, _Metallic and _Flat.
Shader "Elemental/Surface"
{
 Properties {
  _BaseColor("Body",Color)=(.12,.08,.2,1) _EdgeColor("Edge",Color)=(.7,.5,1,1) _HotColor("Internal energy",Color)=(.4,1,.1,1)
  _Glow("Glow",Float)=1 _Mode("Mode",Float)=0 _Opacity("Opacity",Range(0,1))=1 _Age("Age",Float)=0 _MainTex("Stone",2D)="white"{}
  _Roughness("Roughness",Range(0,1))=.6 _Metallic("Metallic",Range(0,1))=0 _Flat("Flat shading",Float)=0
 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"} Cull Off
 Pass { Tags {"LightMode"="UniversalForward"}
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
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_EdgeColor,_HotColor;float _Glow,_Mode,_Opacity,_Age,_Roughness,_Metallic,_Flat;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;float3 normal:TEXCOORD1;float3 local:TEXCOORD2;float fog:TEXCOORD3;};
 V vert(A i){UNITY_SETUP_INSTANCE_ID(i);V o;o.local=i.positionOS.xyz;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.wp);o.normal=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 half4 frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target{
 clip(_Opacity-hash31(floor(i.wp*90))*.98);
 // flatShading: three derives the face normal from screen derivatives, and
 // every view-dependent term in these looks reads that facet normal.
 float3 n=_Flat>.5?normalize(cross(ddy(i.wp),ddx(i.wp))):normalize(i.normal);
 n*=IS_FRONT_VFACE(front,1,-1);
 float3 v=GetWorldSpaceNormalizeViewDir(i.wp);float rim=pow(1-saturate(abs(dot(n,v))),2.6);float3 base=_BaseColor.rgb;float3 emit=0;float t=_SandboxTime;
 float rough=_Roughness;
 if(_Mode<.5){float vein=pow(saturate(1-abs(fbm(i.wp*3.4-float3(0,t*.55,0))*2-1)),14);float frost=smoothstep(.55,1,i.local.y)*.3;base=lerp(base,_EdgeColor.rgb,rim*.65+frost);emit=_HotColor.rgb*vein*(1-saturate(i.local.y)*.65)*_Glow*.3+_EdgeColor.rgb*rim*.5*_Glow;}
 else if(_Mode<1.5){float3 w=pow(abs(n),4);w/=max(dot(w,1),.001);float3 stone=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.wp.yz*.6).rgb*w.x+SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.wp.xz*.6).rgb*w.y+SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.wp.xy*.6).rgb*w.z;base*=stone*1.6;base*=lerp(.4,1,saturate(i.local.y*4));base=lerp(base,_EdgeColor.rgb,saturate(n.y)*saturate(_Age/2.2)*.4);}
 else if(_Mode<2.5){float vein=pow(1-abs(noise3(i.wp*8)*2-1),18);emit=_HotColor.rgb*(vein*.8+rim*.2)*_Glow;}
 else if(_Mode<3.5){float grid=pow(1-abs(sin(i.wp.y*45+sin(i.wp.z*20))),18)+pow(1-abs(sin(i.wp.z*28)),20);base*=.3;emit=(_EdgeColor.rgb*rim*2+_HotColor.rgb*grid*.45)*_Glow;}
 else{emit=_EdgeColor.rgb*rim*_Glow+_HotColor.rgb*pow(noise3(i.wp*11-t*.2),12)*_Glow;}

 InputData d=(InputData)0;d.positionWS=i.wp;d.normalWS=n;d.viewDirectionWS=v;d.shadowCoord=TransformWorldToShadowCoord(i.wp);d.fogCoord=i.fog;d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
 SurfaceData s=(SurfaceData)0;s.albedo=base;s.metallic=_Metallic;s.specular=.04;s.smoothness=saturate(1-rough);s.normalTS=float3(0,0,1);s.occlusion=1;s.emission=emit;s.alpha=1;
 half4 col=UniversalFragmentPBR(d,s);
 return half4(MixFog(col.rgb,i.fog),1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
