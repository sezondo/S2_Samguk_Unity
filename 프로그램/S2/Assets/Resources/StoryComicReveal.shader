Shader "S2/UI/StoryComicReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _RevealAlpha ("Current Cut Alpha", Range(0,1)) = 1
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect;
            float4 _RevealPolygon[16];
            int _RevealCount;
            float _RevealAlpha;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                // 현재 공개 중인 다각형 안쪽만 페이드한다. 이전 컷은 항상 원래 밝기를 유지한다.
                bool inside = false;
                int previous = _RevealCount - 1;
                for (int k = 0; k < 16; k++)
                {
                    if (k >= _RevealCount) break;
                    float2 a = _RevealPolygon[k].xy, b = _RevealPolygon[previous].xy;
                    if ((a.y > i.uv.y) != (b.y > i.uv.y))
                    {
                        float crossing = (b.x-a.x)*(i.uv.y-a.y)/(b.y-a.y)+a.x;
                        if (i.uv.x < crossing) inside = !inside;
                    }
                    previous = k;
                }
                if (inside) c.a *= _RevealAlpha;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - .001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
