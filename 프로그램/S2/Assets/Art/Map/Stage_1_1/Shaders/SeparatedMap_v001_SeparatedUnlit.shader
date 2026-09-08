Shader "S2/Map/SeparatedMapUnlit" {
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Opacity("Opacity",Float)=1 _Saturation("Saturation",Float)=1.2 _Gain("Brightness",Float)=1.12 _Midtone("Midtone",Float)=.94 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { Tags { "LightMode"="SRPDefaultUnlit" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _Color;float _Opacity;float _Saturation;float _Gain;float _Midtone;
 CBUFFER_END
 struct Attributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 // Unity 6의 스프라이트 색과 알파는 별도 렌더러 데이터에도 전달된다.
 Varyings vert(Attributes v){Varyings o;UNITY_SETUP_INSTANCE_ID(v);SetUpSpriteInstanceProperties();v.positionOS=UnityFlipSprite(v.positionOS,unity_SpriteProps.xy);o.positionCS=TransformObjectToHClip(v.positionOS);o.uv=v.uv;o.color=float4((_Color*unity_SpriteColor).rgb,1);return o;}
 half4 frag(Varyings i):SV_Target {half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);float luminance=dot(c.rgb,float3(.2126,.7152,.0722));
 // 검은 선과 투명도는 유지하고 중간 밝기 및 색 구분을 보강한다.
 c.rgb=pow(max(lerp(luminance.xxx,c.rgb,_Saturation),0),_Midtone)*_Gain;c*=i.color;c.a*=_Opacity;return c;}
 ENDHLSL
 } }
}


