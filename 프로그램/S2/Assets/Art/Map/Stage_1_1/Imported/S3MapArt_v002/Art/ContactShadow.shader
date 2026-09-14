Shader "S3/Art02/ContactShadow"
{
 Properties { _Color("접지 그림자",Color)=(0.015,0.019,0.024,.38) }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
  Pass
  {
   Tags {"LightMode"="Universal2D"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float3 p:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;
   CBUFFER_END
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target {float d=length((i.uv-.5)*2);return half4(_Color.rgb,_Color.a*(1-smoothstep(.35,1,d)));}
   ENDHLSL
  }
 }
}
