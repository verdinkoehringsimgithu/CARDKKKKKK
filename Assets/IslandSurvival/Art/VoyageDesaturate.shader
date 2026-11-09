Shader "CardKKK/Island/Voyage Artwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Desaturation ("Desaturation", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct AppData
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd, _Color;
            float4 _ClipRect;
            float _Desaturation;

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.localPosition = input.position.xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 pixel = (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;
                fixed luminance = dot(pixel.rgb, fixed3(.2126, .7152, .0722));
                pixel.rgb = lerp(pixel.rgb, luminance.xxx, _Desaturation);
                #ifdef UNITY_UI_CLIP_RECT
                pixel.a *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(pixel.a - .001);
                #endif
                pixel.rgb *= pixel.a;
                return pixel;
            }
            ENDCG
        }
    }
}
