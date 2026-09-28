Shader "S2/Presentation/GoalFloor"
{
    Properties
    {
        _Tint("청록색 빛", Color) = (0.267,0.937,0.827,1)
        _Core("상아색 중심", Color) = (0.859,1,0.886,1)
        _PulsePeriod("밝기 주기 (초)", Float) = 2.8
        _ParticlePeriod("입자 주기 (초)", Float) = 3.4
        _Opacity("전체 밝기", Range(0,1)) = 1
        _PreviewTime("검증용 시간 (-1은 실시간)", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _Core;
                float _PulsePeriod, _ParticlePeriod, _Opacity, _PreviewTime;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float t = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float pulse = 0.5-0.5*cos(t*6.2831853/_PulsePeriod);
                // 메시 여백을 제외한 정확한 한 칸 경계는 중심에서 0.5칸이다.
                float2 p = (input.uv-0.5)*1.24;
                float distanceToEdge=abs(max(abs(p.x),abs(p.y))-0.5);
                float aa=max(fwidth(distanceToEdge),0.001);
                float edge=1-smoothstep(0.004,0.01+aa,distanceToEdge);
                float core=1-smoothstep(0.001,0.003+aa,distanceToEdge);
                float halo=exp(-distanceToEdge*75)*(0.075+0.055*pulse);
                float inside=1-smoothstep(0.48,0.5,max(abs(p.x),abs(p.y)));
                float fill=(0.018+0.024*pulse)*inside;
                float bands=pow(0.5+0.5*sin(p.y*18.85-t*2.2),8)*0.025*inside;
                float teal=edge*(0.4+0.2*pulse)+halo+fill+bands;
                float ivory=core*(0.22+0.17*pulse);
                // 고정된 칸 내부에서 일곱 입자만 낮게 떠오르게 한다.
                [unroll] for(int k=0;k<7;k++)
                {
                    float life=frac(t/_ParticlePeriod+k*0.147);
                    float2 pos=float2(-0.5+(16+fmod(k*31,80))/112.0+sin(t*0.7+k)*0.0134,
                        0.5-(35+fmod(k*23,66))/112.0+life*0.214);
                    float d=length(p-pos);
                    float radius=(k%3==0?1.4:0.85)/112.0;
                    float alpha=sin(life*3.14159265)*(0.3+0.2*pulse);
                    ivory+=(1-smoothstep(radius,radius+aa,d))*alpha;
                    teal+=exp(-d*150)*alpha*0.35;
                }
                half alpha=saturate(teal+ivory)*_Opacity;
                half3 rgb=(_Tint.rgb*teal+_Core.rgb*ivory)*_Opacity;
                return half4(min(rgb,alpha.xxx),alpha);
            }
            ENDHLSL
        }
    }
}
