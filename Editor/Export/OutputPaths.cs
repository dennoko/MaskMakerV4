using System;
using System.IO;
using UnityEditor;

namespace Dennoko.UVMaskMaker
{
    /// <summary>保存先フォルダ・重複回避連番（Impl export §3）。</summary>
    internal static class OutputPaths
    {
        public const string MaskSubfolderName = "Mask";

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
            var baseDir = BaseFolderFor(s);
            return AppendMaskSubfolder(baseDir, SettingsService.Get(SettingKeys.CreateMaskSubfolder));
        }

        /// <summary>Mask/ サブフォルダを付ける前の基準フォルダ。</summary>
        public static string BaseFolderFor(WorkSession s)
        {
            if (SettingsService.Get(SettingKeys.UseTextureFolder) && s != null)
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
        /// createSubfolder が有効な場合、フォルダ内に Mask/ を作成するパスを返す。
        /// ただし元のフォルダ自体が既に Mask/ の場合は入れ子にせずそのまま返す。
        /// </summary>
        public static string AppendMaskSubfolder(string folder, bool createSubfolder)
        {
            if (string.IsNullOrEmpty(folder)) return folder;
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (!createSubfolder) return normalized;
            if (string.Equals(Path.GetFileName(normalized), MaskSubfolderName, StringComparison.OrdinalIgnoreCase))
                return normalized;
            return normalized + "/" + MaskSubfolderName;
        }

        /// <summary>
        /// 重複するファイル名がある場合は末尾に「 1」「 2」…（Unity標準方式）を付けて重複回避する。
        /// 反転マスク同時保存時は、元マスクと反転マスクが共に重複しなくなるまで番号をインクリメントし、同じ番号にする。
        /// </summary>
        public static (string main, string inv) ResolveMask(string folder, string baseName, bool saveInverted)
        {
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
                string inv = saveInvertedToo ? $"{folder}/{baseName}_inv{suffix}.png" : null;
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
