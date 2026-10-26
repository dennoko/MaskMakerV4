// TriMesh を UV 空間へ描くパス群（Design gpu_pipeline §3）
Shader "Hidden/UVMaskMaker/UVRaster"
{
    Properties
    {
        _ShapeTex ("Shape", 2D) = "black" {}
    }

    CGINCLUDE
    #pragma target 4.5
    #include "UVMaskMakerCommon.hlsl"

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv     : TEXCOORD0;
        float2 tri    : TEXCOORD1;
    };
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: Island（選択済み かつ 有効）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(appdata v) : SV_POSITION
            {
                return HasFlags(v.tri.x, FLAG_SELECTED | FLAG_ACTIVE) ? UVToClip(v.uv + _UVOffset) : COLLAPSED;
            }
            fixed4 frag() : SV_Target { return 1; }
            ENDCG
        }

        // 1: Coverage（有効な三角形すべて）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(appdata v) : SV_POSITION
            {
                return HasFlags(v.tri.x, FLAG_ACTIVE) ? UVToClip(v.uv + _UVOffset) : COLLAPSED;
            }
            fixed4 frag() : SV_Target { return 1; }
            ENDCG
        }

        // 2: PaintWorld（ワールド空間のブラシ / 画面空間の図形。Impl mask_processing §6.4–6.6）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float _PaintMode;      // 0 = カプセル, 1 = 図形
            float4 _SegA;
            float4 _SegB;
            float _Radius;
            float4 _CamPos;
            float4 _CamFwd;
            float _CamOrtho;
            float _XRay;
            float _Mirror;
            float4x4 _MirrorMatrix;
            float4x4 _ShapeVP;
            sampler2D _ShapeTex;
            float _PaintValue;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = HasFlags(v.tri.x, FLAG_ACTIVE) ? UVToClip(v.uv + _UVOffset) : COLLAPSED;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = mul((float3x3)unity_ObjectToWorld, v.normal);
                return o;
            }

            bool Facing(float3 p, float3 n)
            {
                if (_XRay > 0.5) return true;
                if (dot(n, n) < 1e-12) return true; // 法線が無いメッシュは向きで絞らない
                float3 toCam = _CamOrtho > 0.5 ? -_CamFwd.xyz : (_CamPos.xyz - p);
                return dot(n, toCam) > 0.0;
            }

            bool Hit(float3 p, float3 n)
            {
                if (!Facing(p, n)) return false;
                if (_PaintMode < 0.5) return SegmentDistance3(p, _SegA.xyz, _SegB.xyz) <= _Radius;
                float4 c = mul(_ShapeVP, float4(p, 1.0));
                if (c.w <= 0.0) return false;
                float2 vp = c.xy / c.w * 0.5 + 0.5;
                if (any(vp < 0.0) || any(vp > 1.0)) return false;
                return tex2Dlod(_ShapeTex, float4(vp, 0, 0)).r > 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                bool hit = Hit(i.worldPos, i.worldNormal);
                if (!hit && _Mirror > 0.5)
                {
                    float3 mp = mul(_MirrorMatrix, float4(i.worldPos, 1.0)).xyz;
                    float3 mn = mul((float3x3)_MirrorMatrix, i.worldNormal);
                    hit = Hit(mp, mn);
                }
                if (!hit) discard;
                return _PaintValue;
            }
            ENDCG
        }

        // 3: ColorFill（UV 展開図の島の塗り分け。色はバッファ）
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            StructuredBuffer<float4> _TriColors;
            float _RequireSelected;

            struct v2f
            {
                float4 pos : SV_POSITION;
                nointerpolation float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                uint need = _RequireSelected > 0.5 ? (FLAG_SELECTED | FLAG_ACTIVE) : FLAG_ACTIVE;
                o.pos = HasFlags(v.tri.x, need) ? UVToClip(v.uv + _UVOffset) : COLLAPSED;
                o.color = _TriColors[(uint)(v.tri.x + 0.5)];
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
