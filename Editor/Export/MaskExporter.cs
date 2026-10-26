using System;
using System.Collections.Generic;
using System.IO;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Dennoko.UVMaskMaker
{
    /// <summary>マスク PNG の出力（Impl export §2–3）。プレビューと同じ MaskRT を読み戻す。</summary>
    internal static class MaskExporter
    {
        public static void Export(WorkSession s, MessageOrigin origin)
        {
            try
            {
                s.EnsureGpu();
                var size = new Vector2Int(s.Pipeline.Width, s.Pipeline.Height);
                byte[] mask = s.Pipeline.ReadMask();
                bool empty = !TriangleBits.AnySet(EditState.instance.SelectedBits) && PaintRle.IsEmpty(EditState.instance.PaintRle);

                bool invert = SettingsService.Get(SettingKeys.InvertMask);
                bool saveInv = SettingsService.Get(SettingKeys.SaveInvertedToo);
                string folder = OutputPaths.ResolveFolder(s);
                var (mainPath, invPath) = OutputPaths.ResolveMask(folder, s.EffectiveFileName, saveInv);

                byte[] baseRgba = null;
                bool channel = SettingsService.Get(SettingKeys.ChannelWriteEnabled);
                var basePng = s.BasePng;
                if (channel && basePng != null)
                {
                    if (!SourceImage.TryLoadRgba(AssetDatabase.GetAssetPath(basePng), out baseRgba, out int bw, out int bh))
                        baseRgba = null;
                    else if (bw != size.x || bh != size.y)
                        baseRgba = SourceImage.ResizeNearest(baseRgba, bw, bh, size.x, size.y);
                }

                Write(mainPath, Encode(mask, baseRgba, channel, invert), size);
                if (invPath != null) Write(invPath, Encode(mask, baseRgba, channel, !invert), size);

                MaskTexturePostprocessor.Register(mainPath, MaskTexturePostprocessor.Kind.Mask);
                if (invPath != null) MaskTexturePostprocessor.Register(invPath, MaskTexturePostprocessor.Kind.Mask);
                AssetDatabase.ImportAsset(mainPath, ImportAssetOptions.ForceUpdate);
                if (invPath != null) AssetDatabase.ImportAsset(invPath, ImportAssetOptions.ForceUpdate);

                bool overwrote = mainPath == SettingsService.Get(SettingKeys.LastOutputPath);
                SettingsService.Set(SettingKeys.LastOutputPath, mainPath);
                SettingsService.Set(SettingKeys.LastOutputInvPath, invPath ?? string.Empty);
                SessionPersistence.FlushPending();

                PostResult(s, mainPath, overwrote, empty, origin);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                Debug.LogException(e);
                Notifier.Post(MessageLevel.Error, L10n.T("status.writeFailed"), origin);
            }
        }

        private static byte[] Encode(byte[] mask, byte[] baseRgba, bool channel, bool invert)
        {
            if (!channel) return MaskEncoder.Classic(mask, invert);
            return MaskEncoder.Channel(mask, baseRgba,
                SettingsService.Get(SettingKeys.WriteR), SettingsService.Get(SettingKeys.WriteG),
                SettingsService.Get(SettingKeys.WriteB), SettingsService.Get(SettingKeys.WriteA), invert);
        }

        internal static void Write(string path, byte[] rgba, Vector2Int size)
        {
            var png = ImageConversion.EncodeArrayToPNG(rgba, GraphicsFormat.R8G8B8A8_UNorm, (uint)size.x, (uint)size.y);
            File.WriteAllBytes(path, png);
        }

        private static void PostResult(WorkSession s, string path, bool overwrote, bool empty, MessageOrigin origin)
        {
            if (empty)
            {
                Notifier.Post(MessageLevel.Warning, L10n.T("status.empty"), origin);
                return;
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var plan = MaterialApplier.Plan(s, tex);
            string name = Path.GetFileName(path);

            if (plan.AlreadyAssigned)
            {
                Notifier.Post(new UserMessage { Level = MessageLevel.Success, Text = L10n.F("status.updated", name), Origin = origin, AssetPath = path });
                return;
            }
            if (plan.RememberedSlot != null && SettingsService.Get(SettingKeys.AutoApplyMaterial))
            {
                MaterialApplier.Apply(s, tex, plan.RememberedSlot);
                Notifier.Post(new UserMessage { Level = MessageLevel.Success, Text = L10n.F("status.applied", name), Origin = origin, AssetPath = path });
                return;
            }
            var msg = new UserMessage
            {
                Level = MessageLevel.Success,
                Text = L10n.F(overwrote ? "status.updated" : "status.saved", name),
                Origin = origin,
                AssetPath = path,
            };
            if (plan.CanApply)
            {
                msg.ActionLabel = L10n.T("term.apply");
                msg.Action = anchor => MaterialApplier.ApplyInteractive(s, tex, plan, anchor);
            }
            Notifier.Post(msg);
        }
    }

    /// <summary>
    /// 本ツールが書いたパスの初回インポートだけに設定を当てる（Impl export §2.2）。
    /// 再出力（上書き）では当て直さないので、後からユーザーが変えた設定は保たれる。
    /// </summary>
    internal sealed class MaskTexturePostprocessor : AssetPostprocessor
    {
        public enum Kind { Mask, Layout }

        private static readonly Dictionary<string, Kind> Pending = new Dictionary<string, Kind>();

        public static void Register(string path, Kind kind) => Pending[path] = kind;

        private void OnPreprocessTexture()
        {
            if (!Pending.TryGetValue(assetPath, out var kind)) return;
            Pending.Remove(assetPath);
            if (!assetImporter.importSettingsMissing) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.isReadable = false;
            if (kind == Kind.Mask)
            {
                ti.sRGBTexture = false;
                ti.mipmapEnabled = true;
                ti.streamingMipmaps = true;
                if (SettingsService.Get(SettingKeys.ImportAsSingleChannel))
                {
                    ti.textureType = TextureImporterType.SingleChannel;
                    var settings = new TextureImporterSettings();
                    ti.ReadTextureSettings(settings);
                    settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                    ti.SetTextureSettings(settings);
                }
            }
            else
            {
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
            }
        }
    }
}
