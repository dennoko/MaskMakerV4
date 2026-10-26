// マスクの表示（SceneView / UV ビュー。Impl work_context_and_rendering §3）
Shader "Hidden/UVMaskMaker/Overlay"
{
    Properties
    {
        _MaskTex ("Mask", 2D) = "black" {}
        _BaseTex ("Base", 2D) = "black" {}
        _Color ("Color", Color) = (0, 1, 1, 0.6)
        _BgColor ("Background", Color) = (0.07, 0.07, 0.07, 1)
        _BaseAlpha ("Base Alpha", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    CGINCLUDE
    #pragma target 4.5
    #include "UVMaskMakerCommon.hlsl"

    sampler2D _MaskTex;
    sampler2D _BaseTex;
    fixed4 _Color;
    fixed4 _BgColor;
    float _BaseAlpha;
    float4 _UVToClip;   // ndc = uv * xy + zw

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv     : TEXCOORD0;
        float2 tri    : TEXCOORD1;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv  : TEXCOORD0;
    };
    ENDCG

    SubShader
    {
        // 0: Scene（作業メッシュに出力解像度のマスクをポイントサンプリングで貼る）
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = HasFlags(v.tri.x, FLAG_ACTIVE) ? UnityObjectToClipPos(v.vertex) : COLLAPSED;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                if (tex2D(_MaskTex, i.uv).r < 0.5) discard;
                return _Color;
            }
            ENDCG
        }

        // 1: UVView（UV 0..1 の四角形。ベーステクスチャとマスクの重ね）
        Pass
        {
            Cull Off ZWrite Off ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = NdcToClip(v.uv * _UVToClip.xy + _UVToClip.zw);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = _BgColor.rgb;
                if (_BaseAlpha > 0.0) c = lerp(c, tex2D(_BaseTex, i.uv).rgb, _BaseAlpha);
                if (tex2D(_MaskTex, i.uv).r > 0.5) c = lerp(c, _Color.rgb, _Color.a);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
