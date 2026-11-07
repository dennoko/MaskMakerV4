using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// B5 出力カード（skill blocks/cards.md）。出力設定が 3 行以上なのでスクロール領域の最後に置き、出力ドックには要約チップだけを出す。
    /// 基本（ファイル名・解像度・チャンネル）+ 「▶ 詳細」（閉じた状態が既定。閉じている間は見出し行に要約チップ）。
    /// 上書きマーク ● はプロジェクトの既定から変えている項目（クリックで既定に戻す）。
    /// </summary>
    internal sealed class OutputCard
    {
        public OutputCard(VisualElement root)
        {
            root.Q<Label>("output-header").text = L10n.T("output.header");

            Bind.Text(root.Q<TextField>("file-name"), SettingKeys.FileName,
                () => SessionController.Current?.EffectiveFileName ?? string.Empty);

            // 解像度（Base PNG がある時はそのサイズに固定）
            root.Q<Label>("size-label").text = L10n.T("output.size");
            var sizes = new List<(Button, int)>();
            foreach (int size in SettingKeys.TextureSizes) sizes.Add((root.Q<Button>("size-" + size), size));
            Bind.Choice(sizes, SettingKeys.TextureSize, () => UiRules.EffectiveSize(SessionController.Current));
            var locked = root.Q<Label>("size-locked");
            locked.tooltip = L10n.T("tooltip.sizeLocked");
            UiHub.Watch(locked, () =>
            {
                var s = SessionController.Current;
                bool isLocked = UiRules.SizeLocked(s);
                root.Q("size-group").SetEnabled(!isLocked);
                if (isLocked)
                {
                    var size = s.OutputSize();
                    locked.text = size.x + "×" + size.y;
                }
                Bind.Show(locked, isLocked);
            });

            // チャンネル書き込み（OFF の間は中身を出さない）
            var channel = root.Q<Toggle>("channel-toggle");
            channel.text = L10n.T("output.channel");
            Bind.Toggle(channel, SettingKeys.ChannelWriteEnabled);
            var group = root.Q("channel-group");
            UiHub.Watch(group, () => Bind.Show(group, SettingsService.Get(SettingKeys.ChannelWriteEnabled)));
            Bind.ToggleButton(root.Q<Button>("ch-r"), SettingKeys.WriteR);
            Bind.ToggleButton(root.Q<Button>("ch-g"), SettingKeys.WriteG);
            Bind.ToggleButton(root.Q<Button>("ch-b"), SettingKeys.WriteB);
            Bind.ToggleButton(root.Q<Button>("ch-a"), SettingKeys.WriteA);

            var basePng = root.Q<ObjectField>("base-png");
            basePng.objectType = typeof(Texture2D);
            basePng.label = L10n.T("output.basePng");
            basePng.RegisterValueChangedCallback(e =>
            {
                string guid = e.newValue != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.newValue)) : string.Empty;
                SettingsService.Set(SettingKeys.BasePng, guid);
            });
            UiHub.Watch(basePng, () =>
            {
                string guid = SettingsService.Get(SettingKeys.BasePng);
                var tex = string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                // 未指定ならマテリアルのベーステクスチャを候補として見せる（選ぶまでは保存しない）
                basePng.SetValueWithoutNotify(tex);
                var s = SessionController.Current;
                basePng.tooltip = tex == null && s?.BaseTexture != null ? s.BaseTexture.name : string.Empty;
            });

            BindDetails(root);
        }

        private static void BindDetails(VisualElement root)
        {
            root.Q<Label>("output-details-title").text = L10n.T("output.details");
            DennokoBlockUI.BindDetails(root.Q("output-details"));

            // 閉じている間の要約チップ（既定のままの項目は出さない）
            var chips = root.Q("output-details-chips");
            UiHub.Watch(chips, () =>
            {
                chips.Clear();
                foreach (var (text, _) in DetailSummary(SessionController.Current))
                    chips.Add(DennokoBlockUI.Chip(null, text));
            });

            var invert = root.Q<Toggle>("invert-toggle");
            invert.text = L10n.T("output.invert");
            Bind.Toggle(invert, SettingKeys.InvertMask, root.Q("invert-dot"));
            var margin = root.Q<SliderInt>("margin-slider");
            margin.label = L10n.T("output.margin");
            margin.highValue = SettingKeys.MaxPixelMargin;
            Bind.SliderInt(margin, SettingKeys.PixelMargin, () => UiRules.EffectiveMargin(SessionController.Current), root.Q("margin-dot"));
            var saveInv = root.Q<Toggle>("save-inverted-toggle");
            saveInv.text = L10n.T("output.saveInverted");
            Bind.Toggle(saveInv, SettingKeys.SaveInvertedToo, root.Q("save-inverted-dot"));
            var preview = root.Q<Label>("inverted-preview");
            UiHub.Watch(preview, () =>
            {
                var s = SessionController.Current;
                bool on = SettingsService.Get(SettingKeys.SaveInvertedToo) && s != null;
                Bind.Show(preview, on);
                if (on)
                {
                    var (main, inv) = OutputPaths.ResolveMask(OutputPaths.FolderFor(s), s.EffectiveFileName, true);
                    preview.text = Path.GetFileName(main) + " / " + Path.GetFileName(inv);
                }
            });
            var maskSub = root.Q<Toggle>("mask-subfolder-toggle");
            maskSub.text = L10n.T("output.maskSubfolder");
            Bind.Toggle(maskSub, SettingKeys.CreateMaskSubfolder, root.Q("mask-subfolder-dot"));
        }

        /// <summary>「詳細」の中で既定から変えている項目の要約（詳細の見出し行と出力ドックのチップ）。</summary>
        public static IEnumerable<(string text, Setting setting)> DetailSummary(WorkSession s)
        {
            if (SettingsService.Get(SettingKeys.InvertMask)) yield return (L10n.T("output.invert"), SettingKeys.InvertMask);
            if (s != null && SettingsService.Get(SettingKeys.PixelMargin) >= 0)
                yield return (L10n.T("output.margin") + " " + UiRules.EffectiveMargin(s) + "px", SettingKeys.PixelMargin);
            if (SettingsService.Get(SettingKeys.SaveInvertedToo)) yield return ("_inv", SettingKeys.SaveInvertedToo);
        }
    }
}
