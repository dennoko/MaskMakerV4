using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 手元カプセル（UI 設計 §6.1）。作業中だけ表示する。状態は持たず、UiHub の合図で読み直す。
    /// 同じ位置を、選択モードでは「選択の単位」、ペイントモードでは「ペイントツール」に差し替える。
    /// </summary>
    [Overlay(typeof(SceneView), "MaskMaker V4", true)]
    internal sealed class CapsuleOverlay : Overlay, ITransientOverlay
    {
        public bool visible => SessionController.IsWorking;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            UiAssets.SetupRoot(root, false);
            root.AddToClassList("mm-overlay-root");
            var tree = UiAssets.LoadTree(UiAssets.CapsuleUxmlGuid);
            if (tree == null) return root;
            tree.CloneTree(root);

            Button B(string name, string icon, string fallback, string tipKey, string key, System.Action click)
            {
                var b = root.Q<Button>(name);
                Icons.Apply(b, icon, fallback, L10n.T(tipKey) + (key != null ? "  " + key : string.Empty));
                if (click != null) b.clicked += click;
                return b;
            }

            var modeSelect = B("mode-select", Icons.Select, "S", "term.select", "Tab", () => SessionCommands.SetPaintMode(false));
            var modePaint = B("mode-paint", Icons.Paint, "P", "term.paint", "Tab", () => SessionCommands.SetPaintMode(true));

            var island = root.Q<Button>("unit-island");
            var part = root.Q<Button>("unit-part");
            var face = root.Q<Button>("unit-face");
            island.text = L10n.T("term.island"); island.tooltip = L10n.T("term.island") + "  1";
            part.text = L10n.T("term.part"); part.tooltip = L10n.T("term.part") + "  2";
            face.text = L10n.T("term.face"); face.tooltip = L10n.T("term.face") + "  3";
            island.clicked += () => SessionCommands.SetGranularity(SelectionGranularity.UVIsland);
            part.clicked += () => SessionCommands.SetGranularity(SelectionGranularity.ConnectedMesh);
            face.clicked += () => SessionCommands.SetGranularity(SelectionGranularity.Polygon);

            var brush = B("tool-brush", Icons.Paint, "B", "term.brush", "B", () => SessionCommands.SetPaintTool(PaintSubMode.Brush));
            var rect = B("tool-rect", Icons.Rect, "M", "term.rect", "M", () => SessionCommands.SetPaintTool(PaintSubMode.Rect));
            var lasso = B("tool-lasso", Icons.Lasso, "L", "term.lasso", "L", () => SessionCommands.SetPaintTool(PaintSubMode.Lasso));
            var eraser = B("tool-eraser", Icons.Eraser, "E", "term.eraser", "E", () => SessionCommands.SetPaintTool(PaintSubMode.Eraser));

            var symmetry = B("symmetry", Icons.Symmetry, "X", "term.symmetry", "X", SessionCommands.ToggleSymmetry);
            var xray = B("xray", Icons.XRay, "XR", "term.xray", null, SessionCommands.ToggleXRay);
            var isolate = B("isolate", Icons.Isolate, "IS", "term.isolate", null, SessionCommands.ToggleIsolation);
            var pause = B("pause", Icons.Pause, "II", "term.pause", "Space", SessionCommands.TogglePause);
            B("undo", Icons.Undo, "↶", "term.undo", "Ctrl+Z", Undo.PerformUndo);
            B("redo", Icons.Redo, "↷", "term.redo", "Ctrl+Y", Undo.PerformRedo);
            B("clear", Icons.Clear, "C", "term.clear", null, () => SessionCommands.ClearAll(MessageOrigin.Scene));
            var export = B("export", Icons.Export, "Ex", "term.export", "Ctrl+Enter", () =>
            {
                SessionCommands.Export(MessageOrigin.Scene);
                FlashSuccess(root.Q<Button>("export"));
            });

            var units = root.Q("seg-units");
            var tools = root.Q("seg-tools");
            var size = root.Q<Label>("brush-size");
            var count = root.Q<Label>("count");

            UiHub.Watch(root, () =>
            {
                bool paint = UiRules.PaintMode;
                Bind.Active(modeSelect, !paint);
                Bind.Active(modePaint, paint);
                Bind.Show(units, !paint);
                Bind.Show(tools, paint);
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
            return root;
        }

        /// <summary>出力成功時に 2 秒だけ枠を成功色にする（アイコンは差し替えない）。</summary>
        private static void FlashSuccess(Button b)
        {
            b.AddToClassList("mm-success-frame");
            b.schedule.Execute(() => b.RemoveFromClassList("mm-success-frame")).StartingIn(2000);
        }
    }
}
