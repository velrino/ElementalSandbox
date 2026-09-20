Shader "Elemental/Horizon"
{
 Properties{_BaseColor("Ring",Color)=(1,.65,.22,1) _Glow("Glow",Float)=1 _Opacity("Opacity",Float)=1 _Lens("Lens",Float)=.06}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent"}Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass{HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _Glow,_Opacity,_Lens;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float r=length(p);clip(1-r);float a=atan2(p.y,p.x);float ring=exp(-abs(r-.44)*180)+exp(-abs(r-.455)*45)*.35;float3 col=_BaseColor.rgb*ring*(1.1+.6*cos(a-.5))*_Glow*3;float disc=1-smoothstep(.435,.445,r);float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;float lens=pow(saturate(1-r),3)*_Lens;float3 back=SampleSceneColor(saturate(uv+normalize(p+1e-5)*lens));return half4(col,saturate(disc+ring)*_Opacity);}
 ENDHLSL}
 }
}
