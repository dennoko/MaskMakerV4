using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dennoko.UVMaskMaker.Ndmf
{
    /// <summary>
    /// NDMF プレビューの変形後メッシュで作業する供給元（Design architecture §5.3）。
    /// 元のメッシュの準備は RendererMeshSource と同じで、頂点の位置だけを NdmfPreviewDeformer から取る。
    /// </summary>
    [InitializeOnLoad]
    internal sealed class NdmfMeshSource : IWorkMeshSource
    {
        static NdmfMeshSource() => WorkMeshSources.Register(new NdmfMeshSource());

        public int Priority => 10;

        public bool TryCreate(Renderer target, out WorkMesh mesh, out string error)
        {
            mesh = null;
            if (!RendererMeshSource.TryPrepareSource(target, out var source, out string info, out error)) return false;
            mesh = new WorkMesh(target, source, info, new NdmfPreviewDeformer());
            return true;
        }
    }

    /// <summary>
    /// NDMF プレビューのプロキシ Renderer から変形後の頂点を取る。公開 API だけを使い、
    /// プレビュー用シーンにあるプロキシを NDMFPreview.GetOriginalObjectForProxy で元の Renderer と対応づける。
    /// プレビューが無効・対象に変形が無い時はプロキシが見つからず、元のメッシュで作業する。
    /// </summary>
    internal sealed class NdmfPreviewDeformer : IMeshDeformer
    {
        private const double RescanInterval = 0.5;

        private Renderer _proxy;
        private double _nextScan;

        public int StateHash(Renderer target)
        {
            var proxy = FindProxy(target);
            if (proxy == null) return 0;
            int h = proxy.GetInstanceID();
            h = h * 31 + proxy.transform.localToWorldMatrix.GetHashCode();
            var mesh = ProxyMesh(proxy);
            h = h * 31 + (mesh != null ? mesh.GetInstanceID() : 0);
            if (proxy is SkinnedMeshRenderer smr && mesh != null)
                for (int i = 0; i < mesh.blendShapeCount; i++) h = h * 31 + smr.GetBlendShapeWeight(i).GetHashCode();
            return h;
        }

        public bool TryGetDeformed(Renderer target, Mesh scratch, out Mesh mesh, out Matrix4x4 localToWorld)
        {
            mesh = null;
            localToWorld = Matrix4x4.identity;
            var proxy = FindProxy(target);
            if (proxy == null) return false;
            var source = ProxyMesh(proxy);
            if (source == null) return false;
            if (proxy is SkinnedMeshRenderer smr)
            {
                // useScale: true でプロキシのローカル空間になる（WorkMesh.Rebake と同じ）
                smr.BakeMesh(scratch, true);
                mesh = scratch;
            }
            else
            {
                if (!source.isReadable) return false;
                mesh = source;
            }
            localToWorld = proxy.transform.localToWorldMatrix;
            return true;
        }

        private static Mesh ProxyMesh(Renderer proxy)
        {
            if (proxy is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var filter = proxy.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>前回のプロキシがまだ対象のものならそれを使う。違えば一定間隔でプレビュー用シーンを探し直す。</summary>
        private Renderer FindProxy(Renderer target)
        {
            if (target == null) return null;
            if (_proxy != null && IsProxyOf(_proxy, target)) return _proxy;
            _proxy = null;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextScan) return null;
            _nextScan = now + RescanInterval;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !NDMFPreviewSceneManager.IsPreviewScene(scene)) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        if (IsProxyOf(r, target)) return _proxy = r;
            }
            return null;
        }

        private static bool IsProxyOf(Renderer proxy, Renderer target)
        {
            return NDMFPreview.GetOriginalObjectForProxy(proxy.gameObject) == target.gameObject;
        }
    }
}
