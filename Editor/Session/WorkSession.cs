using System;
using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 作業 1 回分の実行時オブジェクト（Design architecture §5.1）。キャッシュをすべて所有し、Dispose で解放する。
    /// ドメインリロードをまたがない（リロード前に必ず終了し、リロード後に作り直す）。
    /// </summary>
    internal sealed class WorkSession : IDisposable
    {
        public Renderer Target { get; }
        public string TargetKey { get; }
        public WorkMesh Mesh { get; }
        public MeshAnalysis Analysis { get; private set; }
        public int UVChannelUsed { get; private set; }
        public int[] Active { get; private set; }
        public int ActiveSubmesh { get; private set; }

        public MeshRaycaster Raycaster { get; private set; }
        public UVGrid UVGrid { get; private set; }

        public GpuResources Gpu { get; private set; }
        public TriMesh Tri { get; private set; }
        public PaintSurface Paint { get; private set; }
        public MaskPipeline Pipeline { get; private set; }
        public PaintOps PaintOps { get; private set; }
        public ShapeMask ShapeMask { get; private set; }
        public LineMesh SeamLines { get; private set; }
        public LineMesh BorderLinesUV { get; private set; }
        public LineMesh WireLinesUV { get; private set; }
        public LineMesh HoverLines3D { get; private set; }
        public LineMesh HoverLinesUV { get; private set; }
        public UVViewRenderer UVView { get; private set; }

        public readonly InteractionState Interaction = new InteractionState();
        public readonly Isolation Isolation = new Isolation();
        private readonly PoseWatcher _pose;

        private int _flagsSelectionRevision = -1;
        private (SelectionGranularity, int, int, int) _hoverLinesKey = (SelectionGranularity.UVIsland, -2, -2, -1);
        private (int, SelectionGranularity, int) _countKey = (-1, SelectionGranularity.UVIsland, int.MinValue);
        private int _countFull, _countTotal;

        public WorkSession(Renderer target, string targetKey, WorkMesh mesh)
        {
            Target = target;
            TargetKey = targetKey;
            Mesh = mesh;
            _pose = new PoseWatcher(target);
        }

        // ── 開始時の手順（SessionController が StartTransaction の中で順に呼ぶ） ──

        /// <summary>解析する。UV が無ければ UV0 にフォールバックし、その旨を warning に返す。</summary>
        public void Analyze(out string warning)
        {
            warning = null;
            int channel = Mathf.Clamp(SettingsService.Get(SettingKeys.UVChannel), 0, 7);
            if (!Mesh.HasUVs(channel))
            {
                if (channel != 0 && Mesh.HasUVs(0))
                {
                    warning = L10n.F("status.uvFallback", channel);
                    channel = 0;
                }
                else throw new InvalidOperationException(L10n.T("error.noUV"));
            }
            UVChannelUsed = channel;

            var input = new MeshInput
            {
                Positions = Mesh.Vertices,
                UVs = Mesh.ReadUVs(channel),
                Indices = Mesh.Indices,
                TriSubmesh = Mesh.TriSubmesh,
                SubmeshCount = Mesh.SubmeshCount,
                MirrorSpacePositions = Mesh.MirrorSpaceVertices(out _),
                MirrorTolerance = SettingsService.Get(SettingKeys.MirrorTolerance),
            };
            Analysis = MeshAnalyzer.Analyze(input);
            Raycaster = new MeshRaycaster(Analysis.Indices, Mesh.Vertices, Analysis.TriSubmesh) { SubmeshCull = Mesh.SubmeshCull() };
            UVGrid = new UVGrid(Analysis.TriUV);
            UpdateActive();
        }

        public void CreateGpu()
        {
            if (!GpuResources.TryCreate(out var gpu, out string error)) throw new InvalidOperationException(error);
            Gpu = gpu;
            Tri = new TriMesh(gpu, Analysis, Mesh);
            Paint = new PaintSurface(gpu);
            Pipeline = new MaskPipeline(gpu, Tri, Paint, Analysis.NeedsWrap);
            PaintOps = new PaintOps(gpu, Tri, Paint, Analysis.NeedsWrap);
            ShapeMask = new ShapeMask(gpu);
            SeamLines = new LineMesh(gpu, "SeamLines");
            BorderLinesUV = new LineMesh(gpu, "BorderLinesUV");
            WireLinesUV = new LineMesh(gpu, "WireLinesUV");
            HoverLines3D = new LineMesh(gpu, "HoverLines3D");
            HoverLinesUV = new LineMesh(gpu, "HoverLinesUV");
            UVView = new UVViewRenderer(gpu);
            RebuildEdgeLines();
        }

        // ── 毎フレームの更新 ──

        /// <summary>EditorApplication.update から呼ぶ。ポーズとサブメッシュの変化を反映する。</summary>
        public void Tick()
        {
            if (_pose.Poll())
            {
                Mesh.Rebake();
                Raycaster.SetVertices(Mesh.Vertices);
                Tri.UpdatePose(Mesh);
                RebuildEdgeLines();
                SceneView.RepaintAll();
            }
            if (ActiveSubmesh != SettingsService.Get(SettingKeys.TargetSubmesh)) UpdateActive();
        }

        private void UpdateActive()
        {
            ActiveSubmesh = SettingsService.Get(SettingKeys.TargetSubmesh);
            if (ActiveSubmesh >= Mesh.SubmeshCount) ActiveSubmesh = -1;
            Active = TriangleBits.ActiveMask(Analysis.TriSubmesh, ActiveSubmesh);
            _flagsSelectionRevision = -1;
            if (Tri != null)
            {
                Tri.UpdateFlags(EditState.instance.SelectedBits, Active, true);
                RebuildEdgeLines();
            }
            _countKey = (-1, SelectionGranularity.UVIsland, int.MinValue);
        }

        /// <summary>GPU のキャッシュを真実に合わせる。描画（Repaint）の外で呼ぶこと。</summary>
        public void EnsureGpu()
        {
            var state = EditState.instance;
            if (_flagsSelectionRevision != state.SelectionRevision && state.SelectedBits.Length == Active.Length)
            {
                Tri.UpdateFlags(state.SelectedBits, Active, false);
                _flagsSelectionRevision = state.SelectionRevision;
            }
            Paint.Ensure(state);
            var size = OutputSize();
            Pipeline.Ensure(size.x, size.y, EffectiveMargin(size));
        }

        /// <summary>対象サブメッシュの三角形（全体なら null）。シーム・ワイヤーの表示をこれで絞る。</summary>
        public int[] EdgeFilter => ActiveSubmesh >= 0 ? Active : null;

        /// <summary>シーム（3D）と UV ビューの境界・ワイヤーを、ポーズと対象サブメッシュに合わせて作り直す。</summary>
        private void RebuildEdgeLines()
        {
            var filter = EdgeFilter;
            BorderLinesUV.SetUV(Analysis.BorderEdges, filter);
            WireLinesUV.SetUV(Analysis.WireEdges, filter);
            var a = new List<Vector3>(Analysis.SeamEdges3D.Count);
            var b = new List<Vector3>(Analysis.SeamEdges3D.Count);
            foreach (var e in Analysis.SeamEdges3D)
            {
                if (!e.VisibleIn(filter)) continue;
                a.Add(Mesh.Vertices[e.A]);
                b.Add(Mesh.Vertices[e.B]);
            }
            SeamLines.Set(a, b);
            _hoverLinesKey = (SelectionGranularity.UVIsland, -2, -2, -1);
        }

        /// <summary>ホバー中のグループ（＋対称グループ）の外周線を必要な時だけ作り直す。</summary>
        public void EnsureHoverLines()
        {
            var g = Granularity;
            int first = Interaction.HoverGroups.Count > 0 ? Interaction.HoverGroups[0] : -1;
            var key = (g, first, Interaction.HoverMirrorGroup, Interaction.HoverGroups.Count);
            if (key == _hoverLinesKey) return;
            _hoverLinesKey = key;

            var a3 = new List<Vector3>();
            var b3 = new List<Vector3>();
            var auv = new List<Vector3>();
            var buv = new List<Vector3>();
            void Add(int group)
            {
                if (group < 0) return;
                foreach (var e in Grouping.Outline(Analysis, g, group))
                {
                    a3.Add(Mesh.Vertices[e.VertexA]);
                    b3.Add(Mesh.Vertices[e.VertexB]);
                    auv.Add(e.UVA);
                    buv.Add(e.UVB);
                }
            }
            foreach (int group in Interaction.HoverGroups) Add(group);
            Add(Interaction.HoverMirrorGroup);
            HoverLines3D.Set(a3, b3);
            HoverLinesUV.Set(auv, buv);
        }

        // ── 解決済みの値 ──

        public SelectionGranularity Granularity => SettingsService.Get(SettingKeys.Granularity);

        public GroupTable Groups => Analysis.Groups(Granularity);

        public bool SymmetryOn => SettingsService.Get(SettingKeys.SymmetryEnabled) && Analysis.MirrorTri != null;

        /// <summary>ワールド → 基準空間で X 反転 → ワールド。</summary>
        public MirrorInfo Mirror
        {
            get
            {
                var root = WorkMesh.FindAvatarRoot(Target.transform);
                return new MirrorInfo
                {
                    Enabled = SymmetryOn,
                    Matrix = root.localToWorldMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1)) * root.worldToLocalMatrix,
                };
            }
        }

        public Material TargetMaterial
        {
            get
            {
                var mats = Target.sharedMaterials;
                if (mats == null || mats.Length == 0) return null;
                int i = ActiveSubmesh >= 0 && ActiveSubmesh < mats.Length ? ActiveSubmesh : 0;
                return mats[i];
            }
        }

        public Texture2D BaseTexture
        {
            get
            {
                var m = TargetMaterial;
                if (m == null) return null;
                foreach (var name in new[] { "_BaseMap", "_MainTex" })
                    if (m.HasProperty(name) && m.GetTexture(name) is Texture2D t) return t;
                return null;
            }
        }

        public Texture2D BasePng
        {
            get
            {
                if (!SettingsService.Get(SettingKeys.ChannelWriteEnabled)) return null;
                var guid = SettingsService.Get(SettingKeys.BasePng);
                if (string.IsNullOrEmpty(guid)) return null;
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        /// <summary>出力解像度（Base PNG があればそのサイズ。Impl export §2.1）。</summary>
        public Vector2Int OutputSize()
        {
            var basePng = BasePng;
            if (basePng != null && SourceImage.TryGetSourceSize(basePng, out int bw, out int bh))
                return new Vector2Int(Mathf.Clamp(bw, 1, 8192), Mathf.Clamp(bh, 1, 8192));
            int s = SettingsService.Get(SettingKeys.TextureSize);
            if (s <= 0) s = AutoTextureSize();
            return new Vector2Int(s, s);
        }

        public bool OutputSizeLockedByBase => BasePng != null;

        /// <summary>ベーステクスチャの大きさに最も近い選択肢。無ければ Project 既定。</summary>
        public int AutoTextureSize()
        {
            var tex = BaseTexture;
            if (tex != null && SourceImage.TryGetSourceSize(tex, out int w, out int h))
            {
                int m = Mathf.Max(w, h), best = SettingKeys.TextureSizes[0];
                foreach (int c in SettingKeys.TextureSizes)
                    if (Mathf.Abs(c - m) < Mathf.Abs(best - m)) best = c;
                return best;
            }
            return SettingsService.Get(SettingKeys.DefaultTextureSize);
        }

        public int EffectiveMargin(Vector2Int size)
        {
            int m = SettingsService.Get(SettingKeys.PixelMargin);
            if (m < 0) m = Mathf.Max(size.x, size.y) / 256;
            return Mathf.Clamp(m, 0, SettingKeys.MaxPixelMargin);
        }

        public string EffectiveFileName
        {
            get
            {
                var n = SettingsService.Get(SettingKeys.FileName);
                return string.IsNullOrWhiteSpace(n) ? Target.name + "_mask" : n.Trim();
            }
        }

        /// <summary>カプセルの「Full のグループ数 / グループ数」。</summary>
        public void SelectionCount(out int full, out int total)
        {
            var key = (EditState.instance.SelectionRevision, Granularity, ActiveSubmesh);
            if (key != _countKey && EditState.instance.SelectedBits.Length == Active.Length)
            {
                SelectionOps.CountGroups(EditState.instance.SelectedBits, Active, Groups, out _countFull, out _countTotal);
                _countKey = key;
            }
            full = _countFull;
            total = _countTotal;
        }

        public void Dispose()
        {
            Isolation.Exit();
            Pipeline?.Dispose();
            Gpu?.Dispose();
            Mesh?.Dispose();
        }
    }
}
