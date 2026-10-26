using System.IO;
using Dennoko.UVMaskMaker.Core;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>既存マスクの黒い部分を手塗りへ OR する（Impl mask_processing §7）。</summary>
    internal static class MaskImporter
    {
        public static void Import(WorkSession s, string assetPath, MessageOrigin origin)
        {
            if (!SourceImage.TryLoadRgba(assetPath, out byte[] rgba, out int w, out int h))
            {
                Notifier.Post(MessageLevel.Warning, L10n.T("status.nothingToImport"), origin);
                return;
            }
            byte[] binary = ImportThreshold.Apply(rgba, SettingsService.Get(SettingKeys.BlackThreshold), out int black);
            if (black == 0)
            {
                Notifier.Post(MessageLevel.Warning, L10n.T("status.nothingToImport"), origin);
                return;
            }

            var tex = s.Gpu.CreateTexture("Import", w, h, TextureFormat.R8, true);
            try
            {
                tex.LoadRawTextureData(binary);
                tex.Apply(false, false);
                s.EnsureGpu();
                s.PaintOps.ImportOr(tex);
                PaintCommit.Commit(s, L10n.T("undo.import"));
            }
            finally
            {
                s.Gpu.Release(tex);
            }
            Notifier.Post(MessageLevel.Success, L10n.F("status.imported", Path.GetFileName(assetPath)), origin);
        }
    }
}
