#ifndef UVMASKMAKER_COMMON_INCLUDED
#define UVMASKMAKER_COMMON_INCLUDED

#include "UnityCG.cginc"

// 三角形フラグ（TriMesh の TEXCOORD1.x が三角形番号）
#define FLAG_SELECTED 1u
#define FLAG_ACTIVE   2u

StructuredBuffer<uint> _TriFlags;
float2 _UVOffset;

// 描かない三角形は 3 頂点を同じ点（画面外）に潰す
static const float4 COLLAPSED = float4(-2.0, -2.0, 0.5, 1.0);

uint TriFlagsOf(float triId)
{
    return _TriFlags[(uint)(triId + 0.5)];
}

bool HasFlags(float triId, uint flags)
{
    return (TriFlagsOf(triId) & flags) == flags;
}

// NDC (y 上向き) → RenderTexture へ描く時のクリップ座標。
// 上下反転の扱いはこの関数だけに閉じ込める（Design gpu_pipeline §3.1）。
float4 NdcToClip(float2 ndc)
{
#if UNITY_UV_STARTS_AT_TOP
    ndc.y = -ndc.y;
#endif
    return float4(ndc, 0.5, 1.0);
}

// UV (0..1) → RT 全面に対応するクリップ座標
float4 UVToClip(float2 uv)
{
    return NdcToClip(uv * 2.0 - 1.0);
}

float SegmentDistance3(float3 p, float3 a, float3 b)
{
    float3 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-12));
    return length(p - (a + ab * t));
}

float SegmentDistance2(float2 p, float2 a, float2 b)
{
    float2 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-12));
    return length(p - (a + ab * t));
}

#endif
