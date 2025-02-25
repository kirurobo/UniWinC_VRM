// Reference https://docs.unity3d.com/ja/2023.2/Manual/SL-ShaderSemantics.html

Shader "Kirurobo/ShadowShader"
{
    Properties
    {
        _Color ("Shadow color", Color) = (0.0, 0.0, 0.0, 0.40)
        _Offset ("Shadow offset [px]", Vector) = (25.0, 25.0, 0, 0)
        _Cutoff ("Clip alpha threshold", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutoff" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //float4 vertex : SV_POSITION;
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _CameraOpaqueTexture;
            float4 _Offset;
            float4 _Color;
            float _Cutoff;

            v2f vert (appdata v, out float4 outpos : SV_POSITION)
            {
                v2f o;
                o.uv = v.uv;
                outpos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                // 画面座標上でオフセット分だけずらした位置をUVとする
                float2 uv = (screenPos.xy - _Offset.xy) / _ScreenParams.xy;

                // 画面からはみ出していた場合はクリップ
                //  はみ出した場合は x, y, (1-x), (1-y) いずれかが負になる
                clip(min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)));

                // レンダリング結果のアルファ値を取得し、指定値以上の場合にのみ影を秒が
                clip(tex2D(_CameraOpaqueTexture, uv).a - _Cutoff);

                // 影の色は指定値
                fixed4 col = _Color;

                return col;
            }
            ENDCG
        }
    }
}
