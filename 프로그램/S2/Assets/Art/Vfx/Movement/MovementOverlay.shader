Shader "S2/Presentation/MovementOverlay"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv; output.color=input.color;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                // 선 중심은 또렷하게 유지하고 바깥쪽만 픽셀 크기에 맞춰 부드럽게 처리한다.
                float d=abs(input.uv.y), aa=max(fwidth(d), .025);
                float core=1-smoothstep(.45-aa,.45+aa,d);
                float halo=(1-smoothstep(.4,1,d))*.12;
                return half4(input.color.rgb,input.color.a*saturate(core+halo));
            }
            ENDHLSL
        }
    }
}
