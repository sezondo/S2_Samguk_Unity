Shader "S2/Presentation/Effect"
{
    Properties
    {
        [PerRendererData] _MainTex("원화", 2D) = "white" {}
        _RevealMap("공개 순서", 2D) = "black" {}
        _JoinTex("궤적 접합 단면", 2D) = "black" {}
        _Color("색상", Color) = (1,1,1,1)
        _Opacity("불투명도", Range(0,1)) = 1
        _Progress("공개 진행", Range(0,1)) = 1
        _UseReveal("공개 마스크 사용", Float) = 0
        _Pulse("진행 맥동", Float) = 0
        _TileBody("몸통 반복", Float) = 0
        _BodyRepeat("몸통 반복 횟수", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_RevealMap); SAMPLER(sampler_RevealMap);
            TEXTURE2D(_JoinTex); SAMPLER(sampler_JoinTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Opacity, _Progress, _UseReveal, _Pulse, _TileBody, _BodyRepeat;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv;
                output.color=input.color*_Color;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                if (_TileBody>0.5) uv.x=frac(uv.x*_BodyRepeat);
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                color.rgb*=color.a;
                // 임의 길이로 잘린 몸통도 양 끝을 동일 단면에 수렴시켜 앞뒤 조각과 연결한다.
                if (_TileBody>0.5)
                {
                    half4 edge=SAMPLE_TEXTURE2D(_JoinTex,sampler_JoinTex,float2(0.5,uv.y));
                    edge.rgb*=edge.a;
                    float amount=smoothstep(0,0.035,min(input.uv.x,1-input.uv.x));
                    color=lerp(edge,color,amount);
                }
                float visible=1;
                float mask=SAMPLE_TEXTURE2D(_RevealMap,sampler_RevealMap,input.uv).r;
                if (_UseReveal>0.5)
                    visible=_Progress<=0 ? 0 : (_Progress>=1 ? 1 : 1-smoothstep(_Progress-0.02,_Progress+0.02,mask));
                color.rgb*=input.color.rgb*(1+_Pulse);
                color*=input.color.a*_Opacity*visible;
                return color;
            }
            ENDHLSL
        }
    }
}
