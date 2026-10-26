namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>解析の入口。各段を順に呼ぶだけ（Impl mesh_analysis）。</summary>
    public static class MeshAnalyzer
    {
        public static MeshAnalysis Analyze(MeshInput input)
        {
            var a = new MeshAnalysis
            {
                TriangleCount = input.TriangleCount,
                VertexCount = input.VertexCount,
                SubmeshCount = input.SubmeshCount,
                Indices = input.Indices,
                TriSubmesh = input.TriSubmesh,
            };

            a.PosIds = VertexIds.PositionIds(input.Positions, out _);
            a.PosUVIds = VertexIds.PositionUVIds(a.PosIds, input.UVs, out _);
            a.TriUV = UVWrap.Apply(input.UVs, input.Indices, out a.NeedsWrap);

            EdgeTable.Build(a);
            a.Islands = Grouping.BuildIslands(a);
            a.Parts = Grouping.BuildParts(a);
            a.Polygons = GroupTable.Identity(a.TriangleCount);

            if (input.MirrorSpacePositions != null)
                a.MirrorTri = MirrorMapBuilder.Build(input.MirrorSpacePositions, input.Indices, input.MirrorTolerance, out a.MirrorMatchRate);

            a.MeshHash = ComputeMeshHash(input.VertexCount, input.SubmeshCount, input.Indices);
            return a;
        }

        /// <summary>作業状態の照合用ハッシュ（Impl mesh_analysis §9）。</summary>
        public static ulong ComputeMeshHash(int vertexCount, int submeshCount, int[] indices)
        {
            var h = new Fnv1a();
            h.Add(vertexCount);
            h.Add(submeshCount);
            h.Add(indices);
            return h.Value;
        }
    }
}
