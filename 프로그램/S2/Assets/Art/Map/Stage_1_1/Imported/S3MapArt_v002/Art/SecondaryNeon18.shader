Shader "S3/Art02/SecondaryNeon18"
{
 Properties { [PerRendererData]_MainTex("그림",2D)="white"{} _RoadTex("도로",2D)="white"{} _Color("색",Color)=(1,1,1,1) _KeyEnabled("배경 제거",Float)=1 _Floor("바닥",Float)=0 _Saturation("채도",Float)=.85 _Emission("발광",Float)=.2 _CyanSignGain("청색 간판 밝기",Float)=.64 }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
 Pass {
 Tags {"LightMode"="Universal2D"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
 #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 TEXTURE2D(_RoadTex); SAMPLER(sampler_RoadTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_TexelSize; half4 _Color; float _KeyEnabled,_Floor,_Saturation,_Emission,_CyanSignGain; float _WallCount; float4 _WallRects[32];
 CBUFFER_END
 #if USE_SHAPE_LIGHT_TYPE_0
 SHAPE_LIGHT(0)
 #endif
 #if USE_SHAPE_LIGHT_TYPE_1
 SHAPE_LIGHT(1)
 #endif
 #if USE_SHAPE_LIGHT_TYPE_2
 SHAPE_LIGHT(2)
 #endif
 #if USE_SHAPE_LIGHT_TYPE_3
 SHAPE_LIGHT(3)
 #endif
 struct A {float3 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR;};
 struct V {float4 p:SV_POSITION; float2 uv:TEXCOORD0; float2 lightUV:TEXCOORD1; float2 world:TEXCOORD2; half4 c:COLOR;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p);o.uv=a.uv;o.world=TransformObjectToWorld(a.p).xy;o.lightUV=ComputeScreenPos(o.p/o.p.w).xy;o.c=a.c;return o;}
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
 half4 frag(V i):SV_Target{
 half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
// 확대 구간에서만 내부 선을 약하게 보강한다. 투명 외곽은 번짐을 피하도록 제외한다.
float2 texel=_MainTex_TexelSize.xy;
half4 left=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(texel.x,0));
half4 right=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(texel.x,0));
half4 down=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(0,texel.y));
half4 up=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(0,texel.y));
float magnified=1-smoothstep(.7,1.5,max(length(ddx(i.uv)*_MainTex_TexelSize.zw),length(ddy(i.uv)*_MainTex_TexelSize.zw)));
float opaque=smoothstep(.9,1,min(c.a,min(min(left.a,right.a),min(up.a,down.a))));
c.rgb=saturate(c.rgb+(c.rgb-(left.rgb+right.rgb+up.rgb+down.rgb)*.25)*.22*magnified*opaque);
 if(_KeyEnabled>.5){float k=smoothstep(.005,.08,min(c.r,c.b)-c.g);c.a*=1-k;c.rb=min(c.rb,c.gg+max(c.r-c.b,c.b-c.r));}
 // 밝은 청색 간판만 조절하고 어두운 지붕과 벽 재질은 유지한다.
 float sign=smoothstep(.30,.60,max(c.b,c.g))*smoothstep(.10,.28,c.b-c.r)*smoothstep(.06,.20,c.g-c.r);
 c.rgb*=lerp(1,_CyanSignGain,sign);
 half4 tint=i.c*_Color;
 if(_Floor>.5){
   tint=_Color;
   if(i.c.r<.05){
     c=SAMPLE_TEXTURE2D(_RoadTex,sampler_RoadTex,i.world*.32);
     float g=i.c.g;
     #ifndef UNITY_COLORSPACE_GAMMA
     g=g<=.0031308?g*12.92:1.055*pow(g,1/2.4)-.055;
     #endif
     int mask=(int)round(g*15);float2 f=frac(i.world);float ex=1,ey=1;
     if((mask&1)==0)ex=min(ex,1-f.x);if((mask&4)==0)ex=min(ex,f.x);
     if((mask&2)==0)ey=min(ey,1-f.y);if((mask&8)==0)ey=min(ey,f.y);
     float edge=min(ex,ey),along=ex<ey?i.world.y:i.world.x;
     half3 curb=SAMPLE_TEXTURE2D(_RoadTex,sampler_RoadTex,float2(along*.32,edge)).rgb;
     float joint=smoothstep(.01,.035,min(frac(along*2),1-frac(along*2)));
     curb=lerp(dot(curb,half3(.21,.72,.07)).xxx,curb,.3)*1.25*lerp(.45,1,joint);
     c.rgb*=half3(1.65,1.58,1.4);c.rgb=lerp(curb,c.rgb,smoothstep(.09,.12,edge));
     c.rgb*=lerp(.3,1,smoothstep(.008,.026,edge));
     c.rgb*=1-.35*(1-smoothstep(.115,.15,edge))*smoothstep(.09,.11,edge);
   } else c.rgb*=.70;
   float nearWall=10;
   for(int w=0;w<(int)_WallCount;w++){float4 r=_WallRects[w];float dx=max(abs(i.world.x-r.x)-r.z,0);float dy=i.world.y-r.y;float d=length(float2(dx,dy));nearWall=min(nearWall,d);}
   float wear=(1-smoothstep(.03,.65,nearWall));c.rgb*=1-wear*.42;
 }
 half gray=dot(c.rgb,half3(.2126,.7152,.0722));c.rgb=lerp(gray.xxx,c.rgb,_Saturation);c*=tint;
 SurfaceData2D surface;InputData2D input;
 InitializeSurfaceData(c.rgb,c.a,half4(1,1,1,1),surface);InitializeInputData(i.uv,i.lightUV,input);
 half4 lit=CombinedShapeLightShared(surface,input);
 float glow=max(smoothstep(.40,.85,c.b)*smoothstep(.12,.4,c.b-c.r),smoothstep(.5,.9,c.r)*smoothstep(.15,.5,c.r-c.b));
 lit.rgb+=c.rgb*glow*_Emission;return lit;
 }
 ENDHLSL
 }
 }
}
