using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// S1 シーンツールバー（カプセル。skill blocks/sceneview.md）。作業中だけ表示する（ITransientOverlay）。
    /// 状態は持たず、UiHub の合図で読み直す。同じ位置を、選択モードでは「選択の単位」、ペイントモードでは「ペイントツール」に差し替える。
    /// 既定の置き場所はシーンビューの左下（ユーザーが動かした位置は Unity が覚える）。
    /// </summary>
    [Overlay(typeof(SceneView), "dennokoworks.MaskMakerV4.Capsule", "MaskMaker V4", true,
        defaultDockZone = DockZone.LeftColumn, defaultDockPosition = DockPosition.Bottom, defaultLayout = Layout.Panel)]
    internal sealed class CapsuleOverlay : Overlay, ITransientOverlay
    {
        public bool visible => SessionController.IsWorking;

        // ドック位置の変更や SceneView の作り直しのたびに呼ばれるので、毎回作り直せる形にする
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            DennokoSceneUI.SetupRoot(root);
            root.AddToClassList("dennoko-overlay-root");
            // 表示言語が変わったら中身ごと作り直す（ツールチップと単位の文字）
            LanguageUi.Build(root, content =>
            {
                if (DennokoSceneUI.CloneTree(UiAssets.CapsuleUxmlGuid, content)) Build(content);
            }, false);
            return root;
        }

        private static void Build(VisualElement root)
        {
            Button B(string name, string icon, string fallback, string tipKey, string key, System.Action click)
            {
                var b = DennokoIcons.Apply(root.Q<Button>(name), icon, fallback, Tip(tipKey, key));
                if (click != null) b.clicked += click;
                return b;
            }

            var modeSelect = B("mode-select", Icons.Select, "S", "term.select", "Tab", () => SessionCommands.SetPaintMode(false));
            var modePaint = B("mode-paint", Icons.Paint, "P", "term.paint", "Tab", () => SessionCommands.SetPaintMode(true));

            var island = Choice(root, "unit-island", "term.island", "1", SelectionGranularity.UVIsland);
            var part = Choice(root, "unit-part", "term.part", "2", SelectionGranularity.ConnectedMesh);
            var face = Choice(root, "unit-face", "term.face", "3", SelectionGranularity.Polygon);

            var brush = B("tool-brush", Icons.Paint, "B", "term.brush", "B", () => SessionCommands.SetPaintTool(PaintSubMode.Brush));
            var rect = B("tool-rect", Icons.Rect, "M", "term.rect", "M", () => SessionCommands.SetPaintTool(PaintSubMode.Rect));
            var lasso = B("tool-lasso", Icons.Lasso, "L", "term.lasso", "L", () => SessionCommands.SetPaintTool(PaintSubMode.Lasso));
            var eraser = B("tool-eraser", Icons.Eraser, "E", "term.eraser", "Shift+B", () => SessionCommands.SetPaintTool(PaintSubMode.Eraser));

            var symmetry = B("symmetry", Icons.Symmetry, "X", "term.symmetry", "X", SessionCommands.ToggleSymmetry);
            var xray = B("xray", Icons.XRay, "XR", "term.xray", null, SessionCommands.ToggleXRay);
            var isolate = B("isolate", Icons.Isolate, "IS", "term.isolate", null, SessionCommands.ToggleIsolation);
            var pause = B("pause", Icons.Pause, "II", "term.pause", "Space", SessionCommands.TogglePause);
            B("undo", Icons.Undo, "↶", "term.undo", "Ctrl+Z", Undo.PerformUndo);
            B("redo", Icons.Redo, "↷", "term.redo", "Ctrl+Y", Undo.PerformRedo);
            B("clear", Icons.Clear, "C", "term.clear", null, () => SessionCommands.ClearAll(MessageOrigin.Scene));
            Button export = null;
            export = B("export", Icons.Export, "Ex", "term.export", "Ctrl+Enter", () =>
            {
                // シーンから実行したので、結果はカーソル脇のトースト（SceneHud）に出る
                SessionCommands.Export(MessageOrigin.Scene);
                DennokoSceneUI.FlashSuccess(export);
            });

            var units = root.Q("seg-units");
            var tools = root.Q("seg-tools");
            var size = root.Q<Label>("brush-size");
            var count = root.Q<Label>("count");

            // 状態 → 表示。選択中・ON は selection 色の枠（.dennoko-button-active）
            UiHub.Watch(root.Q("capsule"), () =>
            {
                bool paint = UiRules.PaintMode;
                Bind.Active(modeSelect, !paint);
                Bind.Active(modePaint, paint);
                DennokoSceneUI.Show(units, !paint);
                DennokoSceneUI.Show(tools, paint);
                var g = SettingsService.Get(SettingKeys.Granularity);
                Bind.Active(island, g == SelectionGranularity.UVIsland);
                Bind.Active(part, g == SelectionGranularity.ConnectedMesh);
                Bind.Active(face, g == SelectionGranularity.Polygon);
                var t = SettingsService.Get(SettingKeys.PaintTool);
                Bind.Active(brush, t == PaintSubMode.Brush);
                Bind.Active(rect, t == PaintSubMode.Rect);
                Bind.Active(lasso, t == PaintSubMode.Lasso);
                Bind.Active(eraser, t == PaintSubMode.Eraser);
                size.text = SettingsService.Get(SettingKeys.BrushSize) + "px";
                Bind.Active(symmetry, SettingsService.Get(SettingKeys.SymmetryEnabled));
                Bind.Active(xray, SettingsService.Get(SettingKeys.OverlayOnTop));
                Bind.Active(isolate, SettingsService.Get(SettingKeys.IsolateTargetMesh));
                Bind.Active(pause, SessionController.IsWorking && SessionCommands.IsPaused);
                count.text = UiRules.CountText(SessionController.Current);
            });
        }

        /// <summary>文字の選択肢（.dennoko-choice、1 ボタン 3 字まで）。</summary>
        private static Button Choice(VisualElement root, string name, string textKey, string key, SelectionGranularity g)
        {
            var b = root.Q<Button>(name);
            b.text = L10n.T(textKey);
            b.tooltip = Tip(textKey, key);
            b.clicked += () => SessionCommands.SetGranularity(g);
            return b;
        }

        /// <summary>ツールチップは「名前  キー」。</summary>
        private static string Tip(string textKey, string key) => L10n.T(textKey) + (key != null ? "  " + key : string.Empty);
    }
}
