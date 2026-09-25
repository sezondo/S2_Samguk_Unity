Shader "S2/Presentation/CombatBackground"
{
    Properties
    {
        [PerRendererData] _MainTex("원화", 2D) = "white" {}
        _Color("색상", Color) = (1,1,1,1)
        _FocusAmount("집중 강도", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FocusAmount;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                Varyings output;
                output.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            // 프리멀티플라이한 샘플을 평균내 투명 여백의 검정 테두리를 방지한다.
            half4 SamplePremultiplied(float2 uv)
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                c.rgb *= c.a;
                return c;
            }
            half4 frag(Varyings input):SV_Target
            {
                // 텍스처 픽셀 수와 무관하게 대략 같은 화면 픽셀 너비로 흐리게 한다.
                float2 dx = ddx(input.uv) * (3.5 * _FocusAmount);
                float2 dy = ddy(input.uv) * (3.5 * _FocusAmount);
                half4 c = SamplePremultiplied(input.uv) * 4;
                c += (SamplePremultiplied(input.uv+dx)+SamplePremultiplied(input.uv-dx)
                    +SamplePremultiplied(input.uv+dy)+SamplePremultiplied(input.uv-dy))*2;
                c += SamplePremultiplied(input.uv+dx+dy)+SamplePremultiplied(input.uv+dx-dy)
                    +SamplePremultiplied(input.uv-dx+dy)+SamplePremultiplied(input.uv-dx-dy);
                c /= 16;
                c.rgb *= input.color.rgb * lerp(1, 0.45, _FocusAmount);
                return c * input.color.a;
            }
            ENDHLSL
        }
    }
}
