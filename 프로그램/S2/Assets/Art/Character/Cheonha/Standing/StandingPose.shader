Shader "S2/Presentation/StandingPose"
{
    Properties
    {
        _MainTex("확정 원화",2D)="white"{}
        _Color("표시 색상",Color)=(1,1,1,1)
        _FocusAmount("전투 배경 흐림",Range(0,1))=0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;float _FocusAmount;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS);o.uv=v.uv;return o;}
            // 투명 외곽을 프리멀티플라이하여 흐림에 흰 테두리가 섞이지 않게 한다.
            half4 SampleArt(float2 uv){half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);c.rgb*=c.a;return c;}
            half4 frag(Varyings i):SV_Target
            {
                float2 dx=ddx(i.uv)*(3.5*_FocusAmount),dy=ddy(i.uv)*(3.5*_FocusAmount);
                half4 c=SampleArt(i.uv)*4;
                c+=(SampleArt(i.uv+dx)+SampleArt(i.uv-dx)+SampleArt(i.uv+dy)+SampleArt(i.uv-dy))*2;
                c+=SampleArt(i.uv+dx+dy)+SampleArt(i.uv+dx-dy)+SampleArt(i.uv-dx+dy)+SampleArt(i.uv-dx-dy);
                c/=16;c.rgb*=_Color.rgb*lerp(1,.45,_FocusAmount);return c*_Color.a;
            }
            ENDHLSL
        }
    }
}
