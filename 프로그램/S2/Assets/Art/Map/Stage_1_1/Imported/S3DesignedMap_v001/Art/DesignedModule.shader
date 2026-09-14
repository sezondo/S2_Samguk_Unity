Shader "S3/Designed/Unlit"
{
 Properties {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _KeyEnabled("Background key",Float)=1
  _ShadowStrength("Contact shadow",Range(0,1))=.28
  _Saturation("채도",Range(0,1))=1
  _HighlightCompression("밝은 반사 억제",Range(0,1))=0
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags { "LightMode"="SRPDefaultUnlit" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _Color; float _KeyEnabled; float _ShadowStrength; float _Saturation; float _HighlightCompression;
   CBUFFER_END
   struct Attributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   Varyings vert(Attributes v){Varyings o;UNITY_SETUP_INSTANCE_ID(v);o.positionCS=TransformObjectToHClip(v.positionOS);o.uv=v.uv;o.color=_Color*v.color;return o;}
   half4 Key(half4 c){if(_KeyEnabled>.5){float key=min(c.r,c.b)-c.g;c.a*=1-smoothstep(.04,.24,key);float spill=max(0,key);c.r-=spill;c.b-=spill;}return c;}
   half4 frag(Varyings i):SV_Target {
    half4 c=Key(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv));
    // 배경 제거 뒤 명암과 채도를 조절해 분홍 키 판정에는 영향을 주지 않는다.
    half gray=dot(c.rgb,half3(.2126,.7152,.0722));
    c.rgb=lerp(gray.xxx,c.rgb,_Saturation);
    c.rgb=c.rgb/(1+_HighlightCompression*c.rgb);
    // 그림자를 별도 오브젝트로 남기지 않아 타일을 지우거나 옮길 때 함께 따라간다.
    half shadow=Key(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(-.008,.012))).a*_ShadowStrength;
    half a=c.a+(1-c.a)*shadow;
    return half4(c.rgb*c.a/max(a,.0001),a)*i.color;
   }
   ENDHLSL
  }
 }
}
