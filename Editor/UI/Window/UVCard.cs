using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>UV ビューのカード（UV ビュー本体、ベース重ね、UV 展開図の書き出しポップアップ）。</summary>
    internal sealed class UVCard
    {
        public UVCard(VisualElement root)
        {
            var host = root.Q("uv-host");
            var view = new UVViewElement();
            host.Insert(0, view);

            var baseButton = root.Q<Button>("base-overlay-button");
            Icons.Apply(baseButton, Icons.Texture, "B", L10n.T("tooltip.baseOverlay"));
            Bind.ToggleButton(baseButton, SettingKeys.PreviewOverlayBaseTex);

            var alpha = root.Q<Slider>("base-alpha-slider");
            Bind.Slider(alpha, SettingKeys.PreviewOverlayAlpha);
            UiHub.Watch(alpha, () => Bind.Show(alpha, SettingsService.Get(SettingKeys.PreviewOverlayBaseTex)));

            var popup = root.Q("layout-popup");
            Bind.Show(popup, false);
            var layoutButton = root.Q<Button>("layout-button");
            Icons.Apply(layoutButton, Icons.Layout, "UV", L10n.T("tooltip.layout"));
            layoutButton.clicked += () =>
            {
                bool open = popup.resolvedStyle.display == DisplayStyle.None;
                Bind.Show(popup, open);
                Bind.Active(layoutButton, open);
            };

            Bind.Choice(new List<(Button, int)>
            {
                (root.Q<Button>("layout-size-512"), 512),
                (root.Q<Button>("layout-size-1024"), 1024),
                (root.Q<Button>("layout-size-2048"), 2048),
                (root.Q<Button>("layout-size-4096"), 4096),
            }, SettingKeys.UVLayoutSize, () => SettingsService.Get(SettingKeys.UVLayoutSize));

            Toggle(root, "layout-wire", "layout.wire", SettingKeys.UVLayoutWireframe);
            Toggle(root, "layout-seams", "layout.seams", SettingKeys.UVLayoutSeams);
            Toggle(root, "layout-selection-only", "layout.selectionOnly", SettingKeys.UVLayoutSelectionOnly);
            Toggle(root, "layout-color-fill", "layout.colorFill", SettingKeys.UVLayoutColorFill);

            var bgItems = new List<(Button, UVLayoutBackground)>
            {
                (Text(root, "layout-bg-transparent", "layout.bgTransparent"), UVLayoutBackground.Transparent),
                (Text(root, "layout-bg-base", "layout.bgBase"), UVLayoutBackground.BaseTexture),
            };
            Bind.Choice(bgItems, SettingKeys.UVLayoutBackgroundMode, () => SettingsService.Get(SettingKeys.UVLayoutBackgroundMode));

            var lineItems = new List<(Button, UVLayoutLineColor)>
            {
                (Text(root, "layout-line-white", "layout.white"), UVLayoutLineColor.White),
                (Text(root, "layout-line-black", "layout.black"), UVLayoutLineColor.Black),
                (Text(root, "layout-line-cyan", "layout.cyan"), UVLayoutLineColor.Cyan),
            };
            Bind.Choice(lineItems, SettingKeys.UVLayoutLine, () => SettingsService.Get(SettingKeys.UVLayoutLine));

            var write = Text(root, "layout-write", "button.writeLayout");
            write.clicked += SessionCommands.ExportUVLayout;
        }

        private static void Toggle(VisualElement root, string name, string textKey, Setting<bool> s)
        {
            var t = root.Q<Toggle>(name);
            t.text = L10n.T(textKey);
            Bind.Toggle(t, s);
        }

        private static Button Text(VisualElement root, string name, string textKey)
        {
            var b = root.Q<Button>(name);
            b.text = L10n.T(textKey);
            return b;
        }
    }
}
