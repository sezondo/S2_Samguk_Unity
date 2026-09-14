Shader "S3/Art02/EntryStones21"
{
 Properties {_RoadTex("기존 도로 석재",2D)="white"{} }
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
 TEXTURE2D(_RoadTex); SAMPLER(sampler_RoadTex);
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

 half4 frag(V i):SV_Target {
 // 기존 도로의 돌 내부 질감을 사용하고, 각 돌의 외곽은 실제 메시로 끝낸다.
 // 도로와 같은 월드 좌표를 사용해 질감 늘어짐과 줄눈 중복을 피한다.
 half3 stone=SAMPLE_TEXTURE2D(_RoadTex,sampler_RoadTex,i.world*.30).rgb*half3(.90,.92,.94);
 stone*=i.c.rgb;
 float gray=dot(stone,half3(.2126,.7152,.0722));stone=lerp(gray.xxx,stone,.78);
 SurfaceData2D surface;InputData2D input;
 InitializeSurfaceData(stone,1,half4(1,1,1,1),surface);InitializeInputData(i.uv,i.lightUV,input);
 return CombinedShapeLightShared(surface,input);
 }
 ENDHLSL
 }
 }
}
