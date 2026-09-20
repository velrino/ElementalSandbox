Shader "Elemental/Energy"
{
 Properties { _BaseColor("Color",Color)=(.4,1,.8,1) _HotColor("Core",Color)=(1,1,1,1) _Mode("Mode",Float)=0 _Glow("Glow",Float)=1 _Opacity("Opacity",Float)=1 _Phase("Phase",Float)=0 _Seed("Seed",Float)=0 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha One ZWrite Off Cull Off
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "ElementalCommon.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_HotColor;float _Mode,_Glow,_Opacity,_Phase,_Seed;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 wp:TEXCOORD1;float3 normal:TEXCOORD2;float4 color:COLOR;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.wp=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.color=i.color;return o;}
 half4 frag(V i):SV_Target{
 float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x),t=_SandboxTime;float energy=0,core=0,alpha=1;
 if(_Mode<.5){float ring=exp(-abs(r-.88)*130);float halo=exp(-abs(r-.88)*24)*.18;float ticks=step(.83,r)*step(r,.99)*pow(saturate(cos(a*32+t*.2)),35);energy=ring+halo+ticks*.6+max(0,1-r)*.04;alpha=1-smoothstep(.98,1,r);}
 else if(_Mode<1.5){float n=fbm(float3(p*7,_Seed+t*.2));float ridge=pow(1-abs(n*2-1),12);float cracks=pow(1-abs(noise3(float3(p*15,t*.12))*2-1),20);energy=(ridge*.7+cracks*.5)*smoothstep(1,.75,r);energy+=exp(-abs(r-.93)*100)*.6;core=energy*.3;}
 else if(_Mode<2.5){float2 q=rotate2(p,t*.07);float star=abs(abs(q.x)+abs(q.y)-.72);float inner=abs(abs(q.x)+abs(q.y)-.34);float rings=exp(-abs(r-.85)*180)+exp(-abs(r-.69)*160)*.7;float mark=exp(-star*160)+exp(-inner*170)*.65;float spokes=pow(saturate(cos(a*4)),90)*smoothstep(.9,.3,r);float ticks=pow(saturate(cos(a*48)),40)*step(.91,r)*step(r,.99);energy=rings+mark+spokes*.7+ticks*.6+max(0,1-r)*.04;}
 else if(_Mode<3.5){float n=fbm(float3(i.uv.x*12,i.uv.y*5-t*1.2,_Seed));float stripes=pow(saturate(sin(i.uv.x*140+n*3)),18);float bands=exp(-abs(i.uv.y-.12)*80)+exp(-abs(i.uv.y-.85)*80);float rune=step(.72,noise3(float3(floor(i.uv.x*80),floor(i.uv.y*16),_Seed)))*pow(saturate(sin(i.uv.x*250)),6);energy=(n*.16+stripes*.2+bands*.9+rune*.7)*smoothstep(0,.08,i.uv.y)*smoothstep(1,.85,i.uv.y);}
 else if(_Mode<4.5){float u=abs(i.uv.x*2-1);energy=pow(saturate(1-u),2)*(.4+fbm(float3(i.wp.xz*2,i.wp.y*.2-t*3)));core=pow(saturate(1-u),16)*2;energy*=smoothstep(1,.7,i.uv.y);}
 else if(_Mode<5.5){energy=exp(-r*r*5)*smoothstep(1,.7,r);core=exp(-r*r*24);}
 else if(_Mode<6.5){energy=pow(saturate(1-abs(i.uv.y*2-1)),1.5)*(.7+.3*sin(i.uv.x*60-t*5));}
 else{float star=pow(saturate(1-abs(p.x)),30)*pow(saturate(1-abs(p.y)),2)+pow(saturate(1-abs(p.y)),45)*pow(saturate(1-abs(p.x)),2);energy=star+exp(-r*r*20)*.2;core=exp(-r*r*130)*2;}
 float3 col=(_BaseColor.rgb*energy+_HotColor.rgb*core)*_Glow;return half4(col*i.color.rgb,alpha*_Opacity*i.color.a);}
 ENDHLSL}
 }
}
