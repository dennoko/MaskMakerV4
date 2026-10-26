using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>UV ビューのカード（UV ビュー本体、表示を戻す、ベース重ね、UV 展開図の書き出しタブを開く）。</summary>
    internal sealed class UVCard
    {
        public UVCard(VisualElement root)
        {
            var host = root.Q("uv-host");
            var view = new UVViewElement();
            host.Insert(0, view);

            var resetButton = root.Q<Button>("reset-view-button");
            Icons.Apply(resetButton, Icons.ResetView, "1:1", L10n.T("tooltip.resetView"));
            resetButton.clicked += view.ResetView;

            var baseButton = root.Q<Button>("base-overlay-button");
            Icons.Apply(baseButton, Icons.Texture, "B", L10n.T("tooltip.baseOverlay"));
            Bind.ToggleButton(baseButton, SettingKeys.PreviewOverlayBaseTex);

            var alpha = root.Q<Slider>("base-alpha-slider");
            Bind.Slider(alpha, SettingKeys.PreviewOverlayAlpha);
            UiHub.Watch(alpha, () => Bind.Show(alpha, SettingsService.Get(SettingKeys.PreviewOverlayBaseTex)));

            var layoutButton = root.Q<Button>("layout-button");
            Icons.Apply(layoutButton, Icons.Layout, "UV", L10n.T("tooltip.layout"));
            layoutButton.clicked += UVLayoutWindow.Open;
        }
    }
}
