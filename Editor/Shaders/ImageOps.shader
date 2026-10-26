// 全画面の画像処理（Graphics.Blit で使う。Design gpu_pipeline §3）
Shader "Hidden/UVMaskMaker/ImageOps"
{
    Properties
    {
        _MainTex ("Dummy", 2D) = "black" {}
    }

    CGINCLUDE
    #pragma target 4.5
    #include "UVMaskMakerCommon.hlsl"

    sampler2D _SrcTex;
    sampler2D _PaintTex;
    sampler2D _IslandTex;
    sampler2D _MergedTex;
    sampler2D _CoverageTex;
    sampler2D _ShapeTex;
    float4 _OutSize;     // (w, h, 1/w, 1/h)
    float _N;
    float _Margin;
    float _PaintMode;
    float4 _Seg2;        // (ax, ay, bx, by) UV ビューの画面座標
    float _Radius;
    float4 _UVToView;    // view = uv * xy + zw
    float4 _ViewSize;    // (w, h, 0, 0)
    float _PaintValue;

    float Sample(sampler2D tex, float2 uv)
    {
        return tex2Dlod(tex, float4(uv, 0, 0)).r;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: Downsample（手塗り 4096² の面積判定 ∨ Island = M）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag(v2f_img i) : SV_Target
            {
                int n = (int)_N;
                int count = 0;
                [loop] for (int y = 0; y < n; y++)
                {
                    [loop] for (int x = 0; x < n; x++)
                    {
                        float2 offset = ((float2(x, y) + 0.5) / n - 0.5) * _OutSize.zw;
                        count += Sample(_PaintTex, i.uv + offset) > 0.5 ? 1 : 0;
                    }
                }
                float paintLow = count * 2 >= n * n ? 1.0 : 0.0;
                float island = Sample(_IslandTex, i.uv) > 0.5 ? 1.0 : 0.0;
                return max(paintLow, island);
            }
            ENDCG
        }

        // 1: DilateH（横に ±m の最大値）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag(v2f_img i) : SV_Target
            {
                int m = (int)_Margin;
                float v = 0.0;
                [loop] for (int k = -m; k <= m; k++)
                    v = max(v, Sample(_SrcTex, i.uv + float2(k * _OutSize.z, 0.0)));
                return v > 0.5 ? 1.0 : 0.0;
            }
            ENDCG
        }

        // 2: DilateV（縦に ±m の最大値。面が使っている画素は元の M のまま = D）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag(v2f_img i) : SV_Target
            {
                float m0 = Sample(_MergedTex, i.uv) > 0.5 ? 1.0 : 0.0;
                if (Sample(_CoverageTex, i.uv) > 0.5) return m0;
                int m = (int)_Margin;
                float v = 0.0;
                [loop] for (int k = -m; k <= m; k++)
                    v = max(v, Sample(_SrcTex, i.uv + float2(0.0, k * _OutSize.w)));
                return max(m0, v > 0.5 ? 1.0 : 0.0);
            }
            ENDCG
        }

        // 3: PaintView（UV ビュー上のブラシ / 図形）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 view = i.uv * _UVToView.xy + _UVToView.zw;
                bool hit;
                if (_PaintMode < 0.5)
                {
                    hit = SegmentDistance2(view, _Seg2.xy, _Seg2.zw) <= _Radius;
                }
                else
                {
                    float2 sv = view / _ViewSize.xy;
                    hit = all(sv >= 0.0) && all(sv <= 1.0) && Sample(_ShapeTex, sv) > 0.5;
                }
                if (!hit) discard;
                return _PaintValue;
            }
            ENDCG
        }

        // 4: ImportOr（2 値画像の黒い部分を手塗りへ OR）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag(v2f_img i) : SV_Target
            {
                if (Sample(_SrcTex, i.uv) < 0.5) discard;
                return 1.0;
            }
            ENDCG
        }

        // 5: Copy（.r をそのまま写す）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag(v2f_img i) : SV_Target
            {
                return Sample(_SrcTex, i.uv) > 0.5 ? 1.0 : 0.0;
            }
            ENDCG
        }
    }
}
