using System.IO;
using System.Text;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>対象の識別子 {Renderer名}_{8桁ハッシュ}（Impl work_context_and_rendering §2.5）。</summary>
    internal static class TargetKey
    {
        public static string For(Renderer renderer)
        {
            var mesh = SourceMesh(renderer);
            string source;
            if (mesh != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId))
                source = guid + ":" + localId + ":" + renderer.name;
            else
                source = GlobalObjectId.GetGlobalObjectIdSlow(renderer).ToString();

            var h = new Fnv1a();
            foreach (byte b in Encoding.UTF8.GetBytes(source)) h.Add(b);
            return Sanitize(renderer.name) + "_" + ((uint)(h.Value ^ (h.Value >> 32))).ToString("x8");
        }

        public static Mesh SourceMesh(Renderer renderer)
        {
            switch (renderer)
            {
                case SkinnedMeshRenderer smr: return smr.sharedMesh;
                case MeshRenderer mr:
                    var mf = mr.GetComponent<MeshFilter>();
                    return mf != null ? mf.sharedMesh : null;
                default: return null;
            }
        }

        private static string Sanitize(string name)
        {
            var sb = new StringBuilder(name.Length);
            var invalid = Path.GetInvalidFileNameChars();
            foreach (char c in name) sb.Append(System.Array.IndexOf(invalid, c) >= 0 || c == ' ' ? '_' : c);
            if (sb.Length > 40) sb.Length = 40;
            return sb.Length == 0 ? "target" : sb.ToString();
        }
    }
}
