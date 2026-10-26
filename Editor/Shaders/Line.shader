// 画面上の太さが一定の線（アンチエイリアス付き。Design gpu_pipeline §3）
// LineMesh: POSITION = 端点 A、TEXCOORD0.xyz = 端点 B、TEXCOORD1 = (side ±1, end 0/1)
Shader "Hidden/UVMaskMaker/Line"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.15, 0.15, 1)
        _Thickness ("Thickness", Float) = 2.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    CGINCLUDE
    #pragma target 4.5
    #include "UVMaskMakerCommon.hlsl"

    fixed4 _Color;
    float _Thickness;
    float4 _TargetSize;  // 描画先のピクセルサイズ (w, h)
    float4 _UVToClip;    // ndc = uv * xy + zw（LineUV 用）

    struct appdata
    {
        float4 vertex : POSITION;
        float3 other  : TEXCOORD0;
        float2 param  : TEXCOORD1;
    };

    struct v2f
    {
        float4 pos  : SV_POSITION;
        float  dist : TEXCOORD0;
    };

    v2f Expand(float4 c0, float4 c1, float side, float end)
    {
        float2 halfSize = _TargetSize.xy * 0.5;
        float2 s0 = c0.xy / c0.w * halfSize;
        float2 s1 = c1.xy / c1.w * halfSize;
        float2 d = s1 - s0;
        float len = length(d);
        d = len > 1e-5 ? d / len : float2(1.0, 0.0);
        float2 n = float2(-d.y, d.x);
        float halfW = _Thickness * 0.5 + 0.5;

        v2f o;
        float4 c = end > 0.5 ? c1 : c0;
        c.xy += n * side * halfW / halfSize * c.w;
        o.pos = c;
        o.dist = side * halfW;
        return o;
    }

    fixed4 fragLine(v2f i) : SV_Target
    {
        float a = saturate(_Thickness * 0.5 + 0.5 - abs(i.dist));
        return fixed4(_Color.rgb, _Color.a * a);
    }
    ENDCG

    SubShader
    {
        // 0: Line3D（SceneView）
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragLine
            v2f vert(appdata v)
            {
                float4 c0 = UnityObjectToClipPos(v.vertex);
                float4 c1 = UnityObjectToClipPos(float4(v.other, 1.0));
                return Expand(c0, c1, v.param.x, v.param.y);
            }
            ENDCG
        }

        // 1: LineUV（UV ビュー / UV 展開図の RT）
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragLine
            v2f vert(appdata v)
            {
                float4 c0 = NdcToClip(v.vertex.xy * _UVToClip.xy + _UVToClip.zw);
                float4 c1 = NdcToClip(v.other.xy * _UVToClip.xy + _UVToClip.zw);
                return Expand(c0, c1, v.param.x, v.param.y);
            }
            ENDCG
        }
    }
}
