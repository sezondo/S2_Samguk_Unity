Shader "S2/Stage11/FixedFloor"
{
 Properties { _BaseTone21("골목 명도",Float)=1  _BaseScale21("골목 석재 축척",Float)=.62  [PerRendererData]_MainTex("그림",2D)="white"{} _RoadTex("도로",2D)="white"{} _BaseTex("골목",2D)="white"{} _RoadDistance("도로 경계",2D)="black"{} _Color("색",Color)=(1,1,1,1) _KeyEnabled("배경 제거",Float)=1 _Floor("바닥",Float)=0 _Saturation("채도",Float)=.85 _Emission("발광",Float)=.2 }
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
 TEXTURE2D(_BaseTex); SAMPLER(sampler_BaseTex);
 TEXTURE2D(_RoadDistance); SAMPLER(sampler_RoadDistance);
 CBUFFER_START(UnityPerMaterial)
   float _BaseScale21,_BaseTone21;  half4 _Color; float _KeyEnabled,_Floor,_Saturation,_Emission;        
 CBUFFER_END
static const float _WallCount=18;
static const float _ApronCount=0;
static const float _RepairCount=6;
static const float _WearCount=24;
static const float _CurbRampCount=8;
static const float4 _WallRects[32]={float4(-5.8,-1.7,4.5942173,0),float4(8.9,-7.5,6.849922,0),float4(8,2,6.13823652,0),float4(7.5,17,6.21526861,0),float4(22,-7,4.215727,0),float4(34,-2,4.65989876,0),float4(-11.2,19,1.96650434,0),float4(-4.05,15,4.4976573,0),float4(26,10,11.4101858,0),float4(4,26,8.345559,0),float4(-12.5,-3,3.57061,0),float4(10.9,-15,7.936905,0),float4(22.5,0,2.53914332,0),float4(38,0,3.89530373,0),float4(40,-14,3.51839638,0),float4(10.9,10.2,3.52557683,0),float4(-11.5,-9,9.730316,0),float4(40.6,-14,10.7506561,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0)};
static const float4 _ApronRects[12]={float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0)};
static const float4 _RepairRects[128]={float4(-3.1,11.4,2,0.55),float4(26.5,5.3,0.55,1),float4(5.29999971,-7.7,2.1,0.65),float4(14.0499992,9.25,1.8,0.85),float4(9.45,1.05,1.6,0.75),float4(14.05,6.6,1.24999988,1.1),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0)};
static const float4 _WearAreas[128]={float4(-5.79999971,-1.60000074,4.99999952,0.42),float4(8.900002,-6.704869,8,0.42),float4(9.1,1.3,1.1,0.55),float4(7.5,17.7,7.5,0.42),float4(22,-7.100001,5,0.42),float4(34,-1.4,4.25,0.42),float4(-11.2,15.0999994,2.25,0.42),float4(-4.05,12.9,5,0.42),float4(26,10.1,10.9080124,0.42),float4(4,23.2000027,10,0.42),float4(-12.5,-1.50000036,3.5,0.42),float4(10.9,-13.2999992,9.5,0.42),float4(27.5,6.1,0.309999466,0.42),float4(26.5,5.6,0.424999237,0.42),float4(22.5,1.6,2.25,0.42),float4(38,-2.10000014,4.5,0.42),float4(40,4.6,4,0.42),float4(-2.4,5.79999971,0.449999928,0.42),float4(10.9,9.700001,4.14999962,0.42),float4(-1.9,-1.99999988,0.5749999,0.42),float4(16,11.6,0.5750003,0.42),float4(24.9,4.79999971,0.575000763,0.42),float4(-11.500001,-8.799998,10.499999,0.42),float4(40.6,-10.75,12,0.42),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0)};
static const float4 _CurbRamps[16]={float4(-6.49999952,-1.99000072,0.900000036,0.875),float4(9.600001,-7.06986856,1.0200001,0.837500036),float4(6.9,1.61,1.0200001,1.025),float4(7.35,17.26,1.08,0.95),float4(22,-7.44000053,0.960000038,0.8),float4(36,-1.79,0.900000036,0.875),float4(40,4.235,0.99,0.837500036),float4(9.099999,9.335,0.960000038,0.837500036),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0)};
static const float4 _MapRect=float4(-28,-24,88,80);
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
 // 작은 돌 단위의 경계 마모만 사용해 넓은 바닥에 무작위 얼룩이 생기지 않게 한다.
 float hash09(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float apron09(float2 p){float a=0;for(int k=0;k<(int)_ApronCount;k++){float4 r=_ApronRects[k];float2 q=abs(p-r.xy)-r.zw;float d=length(max(q,0))+min(max(q.x,q.y),0);a=max(a,1-smoothstep(-.12,.30,d+(hash09(floor(p*5))-.5)*.14));}return a;}
 // 줄눈이 겹치지 않도록 하나의 연속 포장 좌표만 사용한다.
 half3 paving10(float2 p){return SAMPLE_TEXTURE2D(_RoadTex,sampler_RoadTex,p).rgb;}
 half4 frag(V i):SV_Target{
 half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
 if(_KeyEnabled>.5){float k=smoothstep(.005,.08,min(c.r,c.b)-c.g);c.a*=1-k;c.rb=min(c.rb,c.gg+max(c.r-c.b,c.b-c.r));}
 half4 tint=i.c*_Color;
 if(_Floor>.5){
   tint=_Color;
   // 서로 다른 타일맵도 동일한 월드 좌표와 경계를 읽어 사각 이음선을 없앤다.
   float2 uv=(i.world-_MapRect.xy)/_MapRect.zw;
   half3 layout=SAMPLE_TEXTURE2D(_RoadDistance,sampler_RoadDistance,uv).rgb;
   float distance=layout.r*2-1;float yard=layout.g;
   half3 baseStone=SAMPLE_TEXTURE2D(_BaseTex,sampler_BaseTex,i.world*_BaseScale21).rgb*_BaseTone21*.80;
   half3 roadStone=paving10(i.world*.30)*half3(.90,.92,.94);
   float grain=hash09(floor(i.world*15))-.5;
   float edge=smoothstep(-.065,.10,distance+grain*.025);
   // 작업면은 골목과 이어진 잔돌 포장으로 구분한다. 반복되는 세로 띠를 만들지 않는다.
   half3 yardStone=SAMPLE_TEXTURE2D(_BaseTex,sampler_BaseTex,i.world*_BaseScale21).rgb*_BaseTone21*half3(.86,.865,.87);
   float yardEdge=4*yard*(1-yard);roadStone=lerp(roadStone,yardStone,smoothstep(.2,.8,yard));roadStone*=1-yardEdge*.07;
   // 석재 단위로 경계를 끊어 보수한 구간만 작고 방향이 다른 돌을 사용한다.
   float repair=0;
   for(int k=0;k<(int)_RepairCount;k++){float4 r=_RepairRects[k];float2 q=abs(i.world-r.xy)/max(r.zw,.01);repair=max(repair,1-smoothstep(.45,1,length(q)));}
   // 보수 흔적은 기존 돌 줄눈을 유지한다. 다른 축척의 사각 텍스처를 덮지 않는다.
   roadStone*=1-repair*.075;
   c.rgb=lerp(baseStone,roadStone,edge);c.a=1;
   // 도로와 마당의 경계에 낮은 연석 상판·앞면·접촉 그림자를 나눠 표현한다.
   // 거리장에 붙여 그리므로 타일을 다시 조립해도 경계가 함께 이어진다.
   float yardDistance=layout.b*2-1;
   bool yardBoundary=abs(yardDistance)<abs(distance);
   float d=yardBoundary?yardDistance:-distance;
   float2 delta=float2(.12/_MapRect.z,.12/_MapRect.w);
   float3 dx=SAMPLE_TEXTURE2D(_RoadDistance,sampler_RoadDistance,uv+float2(delta.x,0)).rgb-SAMPLE_TEXTURE2D(_RoadDistance,sampler_RoadDistance,uv-float2(delta.x,0)).rgb;
   float3 dy=SAMPLE_TEXTURE2D(_RoadDistance,sampler_RoadDistance,uv+float2(0,delta.y)).rgb-SAMPLE_TEXTURE2D(_RoadDistance,sampler_RoadDistance,uv-float2(0,delta.y)).rgb;
   float2 n=normalize((yardBoundary?float2(dx.b,dy.b):-float2(dx.r,dy.r))+float2(.00001,.00001));
   float along=abs(n.x)>abs(n.y)?i.world.y:i.world.x;
   float block=floor(along*2.0);float variation=hash09(float2(block,23));
   float joint=smoothstep(.012,.038,min(frac(along*2.0),1-frac(along*2.0)));
   float chip=step(.84,variation)*(1-smoothstep(.025,.07,abs(frac(along*2.0)-.25)));
   float ramp=0;
   for(int k=0;k<(int)_CurbRampCount;k++){float4 r=_CurbRamps[k];float2 q=abs(i.world-r.xy)/max(r.zw,.01);ramp=max(ramp,1-smoothstep(.65,1.1,max(q.x,q.y)));}
   float height=1-ramp*.94;
   float wornD=d+chip*.025;
   float top=smoothstep(-.025,.005,wornD)*(1-smoothstep(.125,.14,wornD))*joint;
   half3 stone=paving10(i.world*.30)*half3(1.04,1.02,.98)*(1+variation*.08);
   stone=lerp(stone,half3(.085,.082,.076),.28); c.rgb=lerp(c.rgb,stone,top*.96);
   // 도로 쪽 그림자는 좁게 두고 출입구에서는 단차를 부드럽게 낮춘다.
   float face=(1-smoothstep(.055,.085,-wornD))*(1-smoothstep(-.025,-.005,wornD))*joint*height;
   float shadow=(1-smoothstep(.025,.105,abs(d+.085)))*height;
   c.rgb*=1-shadow*.20;
   c.rgb=lerp(c.rgb,stone*.45,face*.96);
   float lip=(1-smoothstep(.004,.015,abs(wornD-.012)))*joint*height;
   c.rgb+=stone*lip*.24;
   float grit=(1-smoothstep(.02,.11,abs(d+.13)))*smoothstep(.3,.8,hash09(floor(i.world*65)));
   c.rgb*=1-grit*.10;
   float nearWall=10;
   for(int w=0;w<(int)_WallCount;w++){float4 r=_WallRects[w];float dx=max(abs(i.world.x-r.x)-r.z,0);float dy=i.world.y-r.y;float d=length(float2(dx,dy));nearWall=min(nearWall,d);}
   // 기계 주변의 사용 흔적은 돌 줄눈과 기존 재질을 살리며 국소적으로만 누적한다.
   float used=0;for(int w=0;w<(int)_WearCount;w++){float4 r=_WearAreas[w];float d=length((i.world-r.xy)/max(r.zw,.01));used=max(used,1-smoothstep(.2,1,d));}
   float detail=saturate(dot(baseStone,half3(.3,.5,.2))*3);
   c.rgb*=1-used*(.10+.20*(1-detail));
   float wear=(1-smoothstep(.03,.65,nearWall));c.rgb*=1-wear*.18;
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





