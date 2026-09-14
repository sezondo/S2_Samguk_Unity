Shader "S3/Art02/CoverGround19"
{
 Properties { _GrainScale21("석재 축척",Float)=.62  _Grain("기존 석재",2D)="gray"{} _Strength("접지 강도",Range(0,1))=.3 _Profile("접지 형태",Float)=0 }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 // 이미 조명 처리된 바닥을 곱셈으로 눌러 야간에도 밝은 스티커처럼 뜨지 않는다.
 Blend DstColor Zero Cull Off ZWrite Off
 Pass {
 Tags {"LightMode"="Universal2D"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_Grain); SAMPLER(sampler_Grain);
 CBUFFER_START(UnityPerMaterial)
 float _Strength, _Profile, _GrainScale21;
 CBUFFER_END
 struct A {float3 p:POSITION;float2 uv:TEXCOORD0;};
 struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.uv=i.uv;o.world=TransformObjectToWorld(i.p).xy;return o;}
 float noise19(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 half4 frag(V i):SV_Target {
 float2 q=abs((i.uv-.5)*2);
 float shape=1-smoothstep(.60,1,pow(pow(q.x,4)+pow(q.y,4),.25));
 float grain=dot(SAMPLE_TEXTURE2D(_Grain,sampler_Grain,i.world*_GrainScale21).rgb,float3(.3,.5,.2));
 float fine=noise19(floor(i.world*75));
 if(_Profile<.5)shape*=lerp(.85,1,fine);
 else if(_Profile<1.5){
   // 두 바퀴의 폭과 운반 방향을 유지하는 끊긴 자국이다.
   shape=(1-smoothstep(.045,.15,abs(q.x-.68)))*(1-smoothstep(.25,1,q.y));
   shape*=lerp(.35,1,noise19(floor(i.world*18)));
 }else if(_Profile<2.5){
   // 목재와 잔해의 발치에만 줄눈을 살린 작은 분진을 흩뜨린다.
   shape*=smoothstep(.26,.76,fine)*lerp(1,.45,saturate(grain*3));
 }else{
   // 누유는 원형 얼룩 대신 밑면에서 짧게 번진 불규칙한 윤곽으로 제한한다.
   shape*=smoothstep(.2,.55,noise19(floor(i.world*13)))*lerp(1,.65,saturate(grain*3));
 }
 return half4(1-shape*_Strength*half3(.93,.97,1),1);
 }
 ENDHLSL
 }
 }
}
