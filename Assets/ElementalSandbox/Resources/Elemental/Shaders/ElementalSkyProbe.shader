// The probe DynamicGI convolves the ambient SH from.
//
// It is never seen: the camera clears to environment.backgroundColor and the
// source keeps its flat backdrop too — "the HDR probe is still loaded, but only
// as (dim) image-based lighting", as Environment.js puts it. This exists purely
// so RenderSettings.skybox can hold the HDR for one UpdateEnvironment call.
//
// It lives here, under Resources, rather than reusing the built-in
// Skybox/Cubemap: nothing references a built-in shader as an asset, so the
// player is free to strip it, and losing it would silently flatten the lighting
// in a build the same way stripped instancing variants once lost the stone.
Shader "Elemental/SkyProbe"
{
 Properties{_Tex("Cubemap",Cube)="black"{}}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
 Cull Off ZWrite Off
 Pass{HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURECUBE(_Tex);SAMPLER(sampler_Tex);
 struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 dir:TEXCOORD0;};
 V vert(A i){V o;o.dir=i.positionOS.xyz;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
 half4 frag(V i):SV_Target{return half4(SAMPLE_TEXTURECUBE(_Tex,sampler_Tex,normalize(i.dir)).rgb,1);}
 ENDHLSL}
 }
}
