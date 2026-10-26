using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 作業メッシュ（Impl work_context_and_rendering §2.4）。頂点・法線は Renderer のローカル空間。
    /// 三角形の並びと UV は元のメッシュのもので、ポーズが変わっても変わらない。
    /// </summary>
    internal sealed class WorkMesh : IDisposable
    {
        public Renderer Renderer { get; }
        public Mesh SourceMesh { get; }
        public Vector3[] Vertices { get; private set; }
        public Vector3[] Normals { get; private set; }
        public int[] Indices { get; }
        public int[] TriSubmesh { get; }
        public int SubmeshCount { get; }
        public int PoseRevision { get; private set; }
        public string Info { get; }

        private readonly Mesh _baked;

        public WorkMesh(Renderer renderer, Mesh source, string info)
        {
            Renderer = renderer;
            SourceMesh = source;
            Info = info;
            SubmeshCount = source.subMeshCount;

            var indices = new List<int>();
            var triSub = new List<int>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                if (source.GetTopology(s) != MeshTopology.Triangles) continue;
                var sub = source.GetTriangles(s);
                indices.AddRange(sub);
                for (int i = 0; i < sub.Length / 3; i++) triSub.Add(s);
            }
            Indices = indices.ToArray();
            TriSubmesh = triSub.ToArray();

            if (renderer is SkinnedMeshRenderer)
            {
                _baked = new Mesh { name = "UVMaskMaker/BakedMesh", hideFlags = HideFlags.HideAndDontSave };
                Rebake();
            }
            else
            {
                Vertices = source.vertices;
                Normals = source.normals;
                if (Normals == null || Normals.Length != Vertices.Length) Normals = new Vector3[Vertices.Length];
            }
        }

        public Matrix4x4 LocalToWorld => Renderer.transform.localToWorldMatrix;

        public int TriangleCount => Indices.Length / 3;

        /// <summary>現在のポーズで焼き直す（SkinnedMeshRenderer のみ）。</summary>
        public void Rebake()
        {
            if (!(Renderer is SkinnedMeshRenderer smr) || _baked == null) return;
            smr.BakeMesh(_baked, false);
            Vertices = _baked.vertices;
            Normals = _baked.normals;
            if (Normals == null || Normals.Length != Vertices.Length) Normals = new Vector3[Vertices.Length];
            PoseRevision++;
        }

        public Vector2[] ReadUVs(int channel)
        {
            var list = new List<Vector2>();
            SourceMesh.GetUVs(channel, list);
            return list.ToArray();
        }

        public bool HasUVs(int channel)
        {
            var list = new List<Vector2>();
            SourceMesh.GetUVs(channel, list);
            return list.Count == SourceMesh.vertexCount && list.Count > 0;
        }

        /// <summary>対称の基準空間（アバタールートのローカル）での頂点位置と、その空間 → ワールドの行列。</summary>
        public Vector3[] MirrorSpaceVertices(out Matrix4x4 mirrorSpaceToWorld)
        {
            var root = FindAvatarRoot(Renderer.transform);
            mirrorSpaceToWorld = root.localToWorldMatrix;
            var toRoot = root.worldToLocalMatrix * LocalToWorld;
            var result = new Vector3[Vertices.Length];
            for (int i = 0; i < result.Length; i++) result[i] = toRoot.MultiplyPoint3x4(Vertices[i]);
            return result;
        }

        /// <summary>Animator を持つ最上位の祖先。無ければ Renderer 自身。</summary>
        public static Transform FindAvatarRoot(Transform t)
        {
            Transform found = null;
            for (var p = t; p != null; p = p.parent)
                if (p.GetComponent<Animator>() != null) found = p;
            return found != null ? found : t;
        }

        public void Dispose()
        {
            if (_baked != null) UnityEngine.Object.DestroyImmediate(_baked);
        }
    }

    /// <summary>作業メッシュの供給元（Design architecture §5.3）。</summary>
    internal interface IWorkMeshSource
    {
        int Priority { get; }
        bool TryCreate(Renderer target, out WorkMesh mesh, out string error);
    }

    /// <summary>供給元の登録表。NDMF 連携などは別アセンブリから Register する。</summary>
    internal static class WorkMeshSources
    {
        private static readonly List<IWorkMeshSource> Sources = new List<IWorkMeshSource> { new RendererMeshSource() };

        public static void Register(IWorkMeshSource source)
        {
            Sources.Add(source);
            Sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static bool TryCreate(Renderer target, out WorkMesh mesh, out string error)
        {
            error = null;
            foreach (var s in Sources)
            {
                if (s.TryCreate(target, out mesh, out string e)) return true;
                error ??= e;
            }
            mesh = null;
            return false;
        }
    }

    /// <summary>Renderer の sharedMesh（SkinnedMeshRenderer は BakeMesh）。</summary>
    internal sealed class RendererMeshSource : IWorkMeshSource
    {
        public int Priority => 0;

        public bool TryCreate(Renderer target, out WorkMesh mesh, out string error)
        {
            mesh = null;
            error = null;
            var source = TargetKey.SourceMesh(target);
            if (source == null)
            {
                error = L10n.T("error.noMesh");
                return false;
            }
            string info = null;
            if (!source.isReadable)
            {
                if (!TryMakeReadable(source))
                {
                    error = L10n.T("error.notReadable");
                    return false;
                }
                info = L10n.T("info.readWriteEnabled");
                source = TargetKey.SourceMesh(target);
            }
            mesh = new WorkMesh(target, source, info);
            return true;
        }

        private static bool TryMakeReadable(Mesh mesh)
        {
            var path = AssetDatabase.GetAssetPath(mesh);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            return true;
        }
    }

    /// <summary>ボーンとブレンドシェイプからポーズの変化を検知する。</summary>
    internal sealed class PoseWatcher
    {
        private readonly Renderer _renderer;
        private int _lastHash;
        private double _changedAt = -1;
        private const double Settle = 0.2;

        public PoseWatcher(Renderer renderer)
        {
            _renderer = renderer;
            _lastHash = Compute();
        }

        /// <summary>変化が落ち着いてから true を 1 回返す（ドラッグ中はまとめる）。</summary>
        public bool Poll()
        {
            int h = Compute();
            double now = EditorApplication.timeSinceStartup;
            if (h != _lastHash)
            {
                _lastHash = h;
                _changedAt = now;
                return false;
            }
            if (_changedAt >= 0 && now - _changedAt >= Settle)
            {
                _changedAt = -1;
                return true;
            }
            return false;
        }

        private int Compute()
        {
            if (_renderer == null) return 0;
            int h = _renderer.transform.localToWorldMatrix.GetHashCode();
            if (_renderer is SkinnedMeshRenderer smr)
            {
                var bones = smr.bones;
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != null) h = h * 31 + bones[i].localToWorldMatrix.GetHashCode();
                var mesh = smr.sharedMesh;
                int count = mesh != null ? mesh.blendShapeCount : 0;
                for (int i = 0; i < count; i++) h = h * 31 + smr.GetBlendShapeWeight(i).GetHashCode();
            }
            return h;
        }
    }
}
