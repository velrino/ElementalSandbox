#ifndef ELEMENTAL_COMMON
#define ELEMENTAL_COMMON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float _SandboxTime;
float hash31(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
float noise3(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash31(i),hash31(i+float3(1,0,0)),f.x),lerp(hash31(i+float3(0,1,0)),hash31(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash31(i+float3(0,0,1)),hash31(i+float3(1,0,1)),f.x),lerp(hash31(i+float3(0,1,1)),hash31(i+1),f.x),f.y),f.z);}
float fbm(float3 p){float n=0,a=.5;[unroll]for(int i=0;i<4;i++){n+=a*noise3(p);p=p*2.03+float3(11.3,7.1,3.8);a*=.5;}return n;}
float2 rotate2(float2 p,float a){float s=sin(a),c=cos(a);return float2(c*p.x-s*p.y,s*p.x+c*p.y);}
float lineSDF(float2 p,float2 a,float2 b){float2 v=b-a;return length(p-a-v*saturate(dot(p-a,v)/max(dot(v,v),.0001)));}

#endif
