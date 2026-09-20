Shader "Elemental/Distortion"
{
 SubShader{Tags{"RenderPipeline"="UniversalPipeline"}ZWrite Off ZTest Always Cull Off
 Pass{HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
 float4 _DistortCenters[4];float4 _DistortParams[4];float _SandboxTime;
 half4 Frag(Varyings i):SV_Target{
 float2 uv=i.texcoord,offset=0;float aspect=_ScaledScreenParams.x/_ScaledScreenParams.y;
 [unroll]for(int j=0;j<4;j++){
 float4 wp=_DistortCenters[j],param=_DistortParams[j];if(wp.w<.001||param.z<.001)continue;
 float4 clip=TransformWorldToHClip(wp.xyz);if(clip.w<=0)continue;float4 sp=ComputeScreenPos(clip);float2 center=sp.xy/sp.w;float radius=max(.001,abs(UNITY_MATRIX_P._m11)*wp.w/clip.w*.5);float2 d=(uv-center)*float2(aspect,1);float r=length(d)/radius;float2 dir=d/max(length(d),.0001)/float2(aspect,1);
 if(param.x>.5){float lens=exp(-pow((r-.9)*1.7,2))*smoothstep(.48,.65,r);offset+=dir*lens*.022*param.z*param.w;}
 else{float front=frac(param.y*.5)*2;float wave=exp(-pow((r-front)*14,2))*sin((r-front)*25);offset+=dir*wave*.008*param.z*param.w;float mask=saturate(1-r)*.0015;offset+=float2(sin(uv.y*70+_SandboxTime*3),cos(uv.x*60-_SandboxTime*2))*mask*param.z*param.w;}
 }
 return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv+offset));
 }
 ENDHLSL}
 }
}
