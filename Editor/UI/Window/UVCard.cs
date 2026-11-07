using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// B3 プレビューカード（UV ビュー。skill blocks/cards.md）。見出しは付けない。
    /// 右上のツールバー（アイコン 3 個まで）: 表示を戻す、ベーステクスチャを重ねる、UV 展開図の書き出し（別ウィンドウ）。
    /// 既存マスクの取り込みはビューへのテクスチャのドロップ（案内は情報行、詳しくはツールチップ）。
    /// </summary>
    internal sealed class UVCard
    {
        public UVCard(VisualElement root)
        {
            var host = root.Q("uv-host");
            var view = new UVViewElement();
            host.Insert(0, view);

            var resetButton = DennokoIcons.Apply(root.Q<Button>("reset-view-button"), Icons.ResetView, "1:1", L10n.T("tooltip.resetView"));
            resetButton.clicked += view.ResetView;

            var baseButton = DennokoIcons.Apply(root.Q<Button>("base-overlay-button"), Icons.Texture, "B", L10n.T("tooltip.baseOverlay"));
            Bind.ToggleButton(baseButton, SettingKeys.PreviewOverlayBaseTex);

            var alpha = root.Q<Slider>("base-alpha-slider");
            alpha.tooltip = L10n.T("tooltip.baseAlpha");
            Bind.Slider(alpha, SettingKeys.PreviewOverlayAlpha);
            UiHub.Watch(alpha, () => Bind.Show(alpha, SettingsService.Get(SettingKeys.PreviewOverlayBaseTex)));

            var layoutButton = DennokoIcons.Apply(root.Q<Button>("layout-button"), Icons.Layout, "UV", L10n.T("tooltip.layout"));
            layoutButton.clicked += UVLayoutWindow.Open;

            var info = root.Q<Label>("uv-info");
            info.text = L10n.T("uv.dropHint");
            info.tooltip = L10n.T("uv.importHint");
        }
    }
}
