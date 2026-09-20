Shader "Elemental/Ground"
{
 Properties{_BaseColor("Floor",Color)=(.01,.014,.02,1) _Tint("Tint",Color)=(.017,.024,.035,1) _MainTex("Rock",2D)="white"{} _Textured("Use texture",Float)=0 _Pool("Pool",Float)=.8}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass{Tags{"LightMode"="UniversalForward"}HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "ElementalCommon.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_Tint;float _Textured,_Pool;
 CBUFFER_END
 float4 _ElementalLights[4];float4 _ElementalLightColors[4];
 struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;float fog:TEXCOORD1;};
 V vert(A i){V o;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.wp);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 half4 frag(V i):SV_Target{float n=fbm(i.wp*.018);float3 base=lerp(_BaseColor.rgb,_Tint.rgb,n*.5);base*=1+(fbm(i.wp*.09+11)-.5)*.05;float3 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.wp.xz/12).rgb; base=lerp(base,tex*.25,_Textured);float pool=lerp(1,1-smoothstep(5,40,length(i.wp.xz)),_Pool);base*=lerp(.18,1,pool);Light l=GetMainLight(TransformWorldToShadowCoord(i.wp));float3 col=base*(SampleSH(float3(0,1,0))+l.color*(saturate(l.direction.y)*l.shadowAttenuation+.08));for(int k=0;k<4;k++){float dist=distance(i.wp,_ElementalLights[k].xyz);float f=pow(saturate(1-dist/max(.01,_ElementalLights[k].w)),2);col+=base*_ElementalLightColors[k].rgb*f*5;}return half4(MixFog(col,i.fog),1);}
 ENDHLSL}
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
