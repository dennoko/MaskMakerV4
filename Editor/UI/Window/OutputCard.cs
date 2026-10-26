using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>出力カード（基本 = B ランク、▸詳細 = C ランク。UI 設計 §4.3）。</summary>
    internal sealed class OutputCard
    {
        public OutputCard(VisualElement root)
        {
            root.Q<Label>("output-header").text = L10n.T("output.header");

            Bind.Text(root.Q<TextField>("file-name"), SettingKeys.FileName,
                () => SessionController.Current?.EffectiveFileName ?? string.Empty);

            // 解像度（Base PNG がある時はそのサイズに固定）
            var sizes = new List<(Button, int)>();
            foreach (int size in SettingKeys.TextureSizes) sizes.Add((root.Q<Button>("size-" + size), size));
            Bind.Choice(sizes, SettingKeys.TextureSize, () => UiRules.EffectiveSize(SessionController.Current));
            var locked = root.Q<Label>("size-locked");
            UiHub.Watch(locked, () =>
            {
                var s = SessionController.Current;
                bool isLocked = UiRules.SizeLocked(s);
                foreach (var (b, _) in sizes) b.SetEnabled(!isLocked);
                if (isLocked)
                {
                    var size = s.OutputSize();
                    locked.text = size.x + "×" + size.y;
                }
                Bind.Show(locked, isLocked);
            });

            // チャンネル書き込み
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

            // 詳細
            var fold = root.Q<Foldout>("details-foldout");
            fold.text = L10n.T("output.details");
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
                if (on) preview.text = s.EffectiveFileName + ".png / " + s.EffectiveFileName + "_inv.png";
            });
        }
    }
}
