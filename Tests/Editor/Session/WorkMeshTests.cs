using NUnit.Framework;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Tests
{
    /// <summary>WorkMesh が IMeshDeformer（NDMF プレビューなど）の頂点を使う・使わない判断。Unity の Mesh を使うので Unity 上でだけ走る。</summary>
    public class WorkMeshTests
    {
        private GameObject _go;
        private Mesh _source;
        private Mesh _deformed;

        [SetUp]
        public void SetUp()
        {
            _source = Quad(Vector3.zero, 4);
            _go = new GameObject("WorkMeshTests");
            _go.AddComponent<MeshFilter>().sharedMesh = _source;
            _go.AddComponent<MeshRenderer>();
            _go.transform.position = new Vector3(1, 2, 3);
            _go.transform.localScale = new Vector3(2, 2, 2);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_source);
            if (_deformed != null) Object.DestroyImmediate(_deformed);
        }

        [Test]
        public void NoDeformer_UsesSourceVertices()
        {
            using (var mesh = new WorkMesh(_go.GetComponent<Renderer>(), _source, null))
            {
                Assert.IsFalse(mesh.Deformed);
                CollectionAssert.AreEqual(_source.vertices, mesh.Vertices);
            }
        }

        [Test]
        public void Deformer_VerticesMovedIntoRendererLocalSpace()
        {
            // 変形後のメッシュはワールド原点・スケール 1 の空間にあり、各頂点が +Z に 1 ずれている
            _deformed = Quad(new Vector3(0, 0, 1), 4);
            var renderer = _go.GetComponent<Renderer>();
            var deformer = new FakeDeformer { Mesh = _deformed, LocalToWorld = Matrix4x4.identity };
            using (var mesh = new WorkMesh(renderer, _source, null, deformer))
            {
                Assert.IsTrue(mesh.Deformed);
                Assert.IsFalse(mesh.DeformMismatch);
                var expected = _deformed.vertices;
                for (int i = 0; i < expected.Length; i++)
                {
                    var world = mesh.LocalToWorld.MultiplyPoint3x4(mesh.Vertices[i]);
                    Assert.Less(Vector3.Distance(expected[i], world), 1e-4f);
                }
                CollectionAssert.AreEqual(_source.triangles, mesh.Indices);
            }
        }

        [Test]
        public void Deformer_VertexCountMismatch_FallsBackToSource()
        {
            _deformed = Quad(Vector3.zero, 3);
            var deformer = new FakeDeformer { Mesh = _deformed, LocalToWorld = Matrix4x4.identity };
            using (var mesh = new WorkMesh(_go.GetComponent<Renderer>(), _source, null, deformer))
            {
                Assert.IsFalse(mesh.Deformed);
                Assert.IsTrue(mesh.DeformMismatch);
                Assert.IsNotNull(mesh.Info);
                CollectionAssert.AreEqual(_source.vertices, mesh.Vertices);
            }
        }

        [Test]
        public void Deformer_DisappearsLater_RevertsToSource()
        {
            _deformed = Quad(new Vector3(0, 0, 1), 4);
            var deformer = new FakeDeformer { Mesh = _deformed, LocalToWorld = Matrix4x4.identity };
            using (var mesh = new WorkMesh(_go.GetComponent<Renderer>(), _source, null, deformer))
            {
                Assert.IsTrue(mesh.Deformed);
                deformer.Mesh = null;
                mesh.Rebake();
                Assert.IsFalse(mesh.Deformed);
                Assert.IsFalse(mesh.DeformMismatch);
                CollectionAssert.AreEqual(_source.vertices, mesh.Vertices);
            }
        }

        /// <summary>count 頂点（4 なら四角形 2 枚、3 なら三角形 1 枚）の XY 平面のメッシュ。</summary>
        private static Mesh Quad(Vector3 offset, int count)
        {
            var v = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) };
            var vertices = new Vector3[count];
            for (int i = 0; i < count; i++) vertices[i] = v[i] + offset;
            var mesh = new Mesh { vertices = vertices };
            mesh.triangles = count == 4 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private sealed class FakeDeformer : IMeshDeformer
        {
            public Mesh Mesh;
            public Matrix4x4 LocalToWorld;

            public int StateHash(Renderer target) => Mesh != null ? Mesh.GetInstanceID() : 0;

            public bool TryGetDeformed(Renderer target, Mesh scratch, out Mesh mesh, out Matrix4x4 localToWorld)
            {
                mesh = Mesh;
                localToWorld = LocalToWorld;
                return Mesh != null;
            }
        }
    }
}
