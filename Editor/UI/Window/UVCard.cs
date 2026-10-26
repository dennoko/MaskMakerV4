using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>UV ビューのカード（UV ビュー本体、表示を戻す、ベース重ね、UV 展開図の書き出し、マスク取り込みの案内）。</summary>
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

            // 既存マスクの取り込みはドロップのみ。左上の表示にホバーすると案内を出す（USS の :hover）
            var import = root.Q("uv-import");
            var importIcon = root.Q("uv-import-icon");
            var importTex = Icons.Get(Icons.Import);
            if (importTex != null) importIcon.style.backgroundImage = importTex;
            Bind.Show(importIcon, importTex != null);
            root.Q<Label>("uv-import-label").text = L10n.T("uv.import");
            root.Q<Label>("uv-import-hint").text = L10n.T("uv.importHint");
            view.AcceptDrops(import);
        }
    }
}
