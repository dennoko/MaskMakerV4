using System.IO;
using UnityEditor;

namespace Dennoko.UVMaskMaker
{
    /// <summary>保存先フォルダ・上書き・連番（Impl export §3）。</summary>
    internal static class OutputPaths
    {
        /// <summary>保存先フォルダを決めて、無ければ作る。</summary>
        public static string ResolveFolder(WorkSession s)
        {
            var dir = FolderFor(s);
            EnsureAssetFolder(dir);
            return dir;
        }

        /// <summary>保存先フォルダ（作らない。表示用）。</summary>
        public static string FolderFor(WorkSession s)
        {
            if (SettingsService.Get(SettingKeys.UseTextureFolder))
            {
                var tex = s.BaseTexture;
                var path = tex != null ? AssetDatabase.GetAssetPath(tex) : null;
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
                    return Path.GetDirectoryName(path).Replace('\\', '/');
            }
            var dir = SettingsService.Get(SettingKeys.OutputDir);
            if (string.IsNullOrWhiteSpace(dir) || !dir.Replace('\\', '/').StartsWith("Assets")) dir = SettingKeys.OutputDir.Default;
            return dir.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// 前回と同じパスなら上書き、他のファイルと衝突する時だけ元 / 反転が同じ番号になる連番を付ける。
        /// </summary>
        public static (string main, string inv) ResolveMask(string folder, string baseName, bool saveInverted)
        {
            string main = $"{folder}/{baseName}.png";
            string inv = saveInverted ? $"{folder}/{baseName}_inv.png" : null;
            bool mainOurs = main == SettingsService.Get(SettingKeys.LastOutputPath);
            bool invOurs = inv == null || inv == SettingsService.Get(SettingKeys.LastOutputInvPath);
            if ((mainOurs || !File.Exists(main)) && (invOurs || inv == null || !File.Exists(inv))) return (main, inv);
            return ResolveUniqueMaskPaths(folder, baseName, saveInverted);
        }

        /// <summary>上書きしない。同名のファイルがあれば「名前 1.png」「名前 2.png」… の空いている番号にする。</summary>
        public static string ResolveUnique(string folder, string baseName)
        {
            string path = $"{folder}/{baseName}.png";
            if (!File.Exists(path)) return path;
            for (int i = 1; ; i++)
            {
                path = $"{folder}/{baseName} {i}.png";
                if (!File.Exists(path)) return path;
            }
        }

        public static (string mainPath, string invPath) ResolveUniqueMaskPaths(string folder, string baseName, bool saveInvertedToo)
        {
            for (int counter = 0; ; counter++)
            {
                string suffix = counter == 0 ? "" : $" {counter}";
                string main = $"{folder}/{baseName}{suffix}.png";
                string inv = saveInvertedToo ? $"{folder}/{baseName}{suffix}_inv.png" : null;
                if (!File.Exists(main) && (inv == null || !File.Exists(inv)))
                    return (main, inv);
            }
        }

        public static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
