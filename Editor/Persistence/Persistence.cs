using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace Dennoko.UVMaskMaker
{
    /// <summary>保存先パスの一元管理（プロジェクトルートからの相対パス）。</summary>
    internal static class StoragePaths
    {
        public const string Root = "UserSettings/UVMaskMaker";
        public const string ProjectSettings = "ProjectSettings/UVMaskMaker.json";

        public static string TargetDir(string targetKey) => Root + "/targets/" + targetKey;
        public static string TargetSettings(string targetKey) => TargetDir(targetKey) + "/settings.json";
        public static string SessionFile(string targetKey) => TargetDir(targetKey) + "/session.bin";
    }

    /// <summary>一時ファイルに書いてから置き換える（書き込み途中で落ちても前回の内容が残る）。</summary>
    internal static class AtomicFile
    {
        public static void WriteAllBytes(string path, byte[] data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public static void WriteAllText(string path, string text) => WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
    }

    /// <summary>
    /// 遅延保存。同じ ID の予約は後から来たもので置き換える（連続した変更をまとめる）。
    /// ドメインリロード前と Unity 終了時に SessionLifecycle が FlushAll を呼ぶ。
    /// </summary>
    [InitializeOnLoad]
    internal static class SaveScheduler
    {
        private struct Job
        {
            public double Due;
            public Action Action;
        }

        private static readonly Dictionary<string, Job> Jobs = new Dictionary<string, Job>();

        static SaveScheduler()
        {
            EditorApplication.update += Tick;
        }

        public static void Schedule(string id, double delaySeconds, Action action)
        {
            Jobs[id] = new Job { Due = EditorApplication.timeSinceStartup + delaySeconds, Action = action };
        }

        public static void Cancel(string id) => Jobs.Remove(id);

        public static void Flush(string id)
        {
            if (!Jobs.TryGetValue(id, out var job)) return;
            Jobs.Remove(id);
            Run(job);
        }

        public static void FlushAll()
        {
            var ids = new List<string>(Jobs.Keys);
            foreach (var id in ids) Flush(id);
        }

        private static void Tick()
        {
            if (Jobs.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            List<string> due = null;
            foreach (var kv in Jobs)
                if (kv.Value.Due <= now) (due ??= new List<string>()).Add(kv.Key);
            if (due == null) return;
            foreach (var id in due) Flush(id);
        }

        private static void Run(Job job)
        {
            try { job.Action(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }
    }

    /// <summary>作業状態ファイル session.bin（Design state_and_lifecycle §6.1）。</summary>
    internal static class SessionFile
    {
        private const uint Magic = 0x4D4D5655; // "UVMM"
        private const int Version = 1;

        public sealed class Data
        {
            public ulong MeshHash;
            public int TriangleCount;
            public int[] SelectedBits;
            public byte[] PaintRle;
        }

        public static void Write(string path, Data data)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(data.MeshHash);
                w.Write(data.TriangleCount);
                w.Write(data.SelectedBits.Length);
                foreach (var v in data.SelectedBits) w.Write(v);
                w.Write(data.PaintRle.Length);
                w.Write(data.PaintRle);
                w.Flush();
                AtomicFile.WriteAllBytes(path, ms.ToArray());
            }
        }

        public static bool TryRead(string path, out Data data)
        {
            data = null;
            if (!File.Exists(path)) return false;
            try
            {
                using (var r = new BinaryReader(File.OpenRead(path)))
                {
                    if (r.ReadUInt32() != Magic || r.ReadInt32() > Version) return false;
                    var d = new Data { MeshHash = r.ReadUInt64(), TriangleCount = r.ReadInt32() };
                    int bitsLen = r.ReadInt32();
                    if (bitsLen < 0 || bitsLen > (1 << 26)) return false;
                    d.SelectedBits = new int[bitsLen];
                    for (int i = 0; i < bitsLen; i++) d.SelectedBits[i] = r.ReadInt32();
                    int rleLen = r.ReadInt32();
                    if (rleLen < 0 || rleLen > (1 << 28)) return false;
                    d.PaintRle = r.ReadBytes(rleLen);
                    if (d.PaintRle.Length != rleLen) return false;
                    data = d;
                    return true;
                }
            }
            catch (IOException) { return false; }
        }
    }
}
