Shader "Elemental/Volume"
{
 Properties { _BaseColor("Body",Color)=(.3,.6,.1,1) _HotColor("Light",Color)=(.8,1,.3,1) _Mode("Mode",Float)=0 _Density("Density",Float)=2.9 _Opacity("Opacity",Float)=1 _Glow("Glow",Float)=1 _Seed("Seed",Float)=0 }
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"} Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
 Pass{HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_HotColor;float _Mode,_Density,_Opacity,_Glow,_Seed;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 wp:TEXCOORD0;};
 V vert(A i){V o;o.wp=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.wp);return o;}
 half4 frag(V i):SV_Target{
 float3 ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=normalize(TransformWorldToObject(i.wp)-ro);float3 inv=1/(rd+1e-7);float3 t0=(-.5-ro)*inv,t1=(.5-ro)*inv;float3 mn=min(t0,t1),mx=max(t0,t1);float nearT=max(0,max(mn.x,max(mn.y,mn.z))),farT=min(mx.x,min(mx.y,mx.z));if(farT<=nearT)return 0;
 float2 screen=i.positionCS.xy/_ScaledScreenParams.xy;float depth=SampleSceneDepth(screen);
 #if !UNITY_REVERSED_Z
 depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
 #endif
 float3 scene=ComputeWorldSpacePosition(screen,depth,UNITY_MATRIX_I_VP);float sceneT=dot(TransformWorldToObject(scene)-ro,rd);farT=min(farT,sceneT);if(farT<=nearT)return 0;
 float stepSize=(farT-nearT)/32;float trans=1;float3 color=0;float t=_SandboxTime;
 [loop]for(int j=0;j<32;j++){
 float3 p=ro+rd*(nearT+(j+.5)*stepSize);float3 q=p*2;float r=length(q.xz);float fall=saturate(1-r*r)*smoothstep(1,.55,abs(q.y));
 if(_Mode> .5&&_Mode<1.5){float a=atan2(q.z,q.x)+t*.35/(.25+r)+q.y*1.4;q.xz=float2(cos(a),sin(a))*r;fall=saturate(1-dot(q,q));fall*=smoothstep(.1,.32,r);}
 float3 drift=float3(0,-t*.35,_Seed);float n=fbm(q*3.8+drift)+noise3(q*9+drift)*.17;float den=max(0,n-.36)*_Density*fall;
 if(_Mode>1.5){den*=.6;}
 float alpha=1-exp(-den*stepSize*14*_Opacity);float light=saturate(.5-q.y*.45+noise3(q*3+float3(0,.8,0))*.3);float3 c=lerp(_BaseColor.rgb*.14,_BaseColor.rgb,light)+_HotColor.rgb*pow(saturate(1-r),4)*_Glow*.7;
 if(_Mode>.5&&_Mode<1.5)c=lerp(_BaseColor.rgb*.35,_HotColor.rgb,pow(saturate(1-r),3))*(.5+light)*_Glow;
 color+=trans*alpha*c;trans*=1-alpha;if(trans<.02)break;
 }return half4(color,1-trans);
 }
 ENDHLSL}
 }
}
