using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// ショートカット（UI 設計 §2.6.7）。SceneView のコンテキスト + 作業中だけ有効なタグで登録する。
    /// Undo / Redo は Unity 標準のものを使う（作業履歴を Unity の Undo に載せているため）。
    /// </summary>
    internal static class MaskMakerShortcuts
    {
        private const string Tag = SessionLifecycle.ShortcutTag;
        private const string P = "MaskMaker V4/";

        [Shortcut(P + "Toggle Select ⇄ Paint", typeof(SceneView), Tag, KeyCode.Tab)]
        private static void ToggleMode() => SessionCommands.TogglePaintMode();

        [Shortcut(P + "Unit: Island", typeof(SceneView), Tag, KeyCode.Alpha1)]
        private static void Island() => SessionCommands.SetGranularity(SelectionGranularity.UVIsland);

        [Shortcut(P + "Unit: Part", typeof(SceneView), Tag, KeyCode.Alpha2)]
        private static void Part() => SessionCommands.SetGranularity(SelectionGranularity.ConnectedMesh);

        [Shortcut(P + "Unit: Face", typeof(SceneView), Tag, KeyCode.Alpha3)]
        private static void Face() => SessionCommands.SetGranularity(SelectionGranularity.Polygon);

        [Shortcut(P + "Brush", typeof(SceneView), Tag, KeyCode.B)]
        private static void Brush() => SessionCommands.SetPaintTool(PaintSubMode.Brush);

        [Shortcut(P + "Rect", typeof(SceneView), Tag, KeyCode.M)]
        private static void Rect() => SessionCommands.SetPaintTool(PaintSubMode.Rect);

        [Shortcut(P + "Lasso", typeof(SceneView), Tag, KeyCode.L)]
        private static void Lasso() => SessionCommands.SetPaintTool(PaintSubMode.Lasso);

        // E は右ドラッグ中のカメラ移動（WASDQE）と重なるので、ブラシの逆として Shift+B にする
        [Shortcut(P + "Eraser", typeof(SceneView), Tag, KeyCode.B, ShortcutModifiers.Shift)]
        private static void Eraser() => SessionCommands.SetPaintTool(PaintSubMode.Eraser);

        [Shortcut(P + "Symmetry", typeof(SceneView), Tag, KeyCode.X)]
        private static void Symmetry() => SessionCommands.ToggleSymmetry();

        [Shortcut(P + "Brush Smaller", typeof(SceneView), Tag, KeyCode.LeftBracket)]
        private static void Smaller() => SessionCommands.SetBrushSize(Mathf.RoundToInt(SettingsService.Get(SettingKeys.BrushSize) / 1.2f));

        [Shortcut(P + "Brush Larger", typeof(SceneView), Tag, KeyCode.RightBracket)]
        private static void Larger() => SessionCommands.SetBrushSize(Mathf.CeilToInt(SettingsService.Get(SettingKeys.BrushSize) * 1.2f));

        [Shortcut(P + "Select All", typeof(SceneView), Tag, KeyCode.A, ShortcutModifiers.Action)]
        private static void SelectAll() => SessionCommands.SelectAll();

        [Shortcut(P + "Invert Selection", typeof(SceneView), Tag, KeyCode.I, ShortcutModifiers.Action)]
        private static void Invert() => SessionCommands.InvertSelection();

        [Shortcut(P + "Quick Export", typeof(SceneView), Tag, KeyCode.Return, ShortcutModifiers.Action)]
        private static void Export() => SessionCommands.Export(MessageOrigin.Scene);

        [ClutchShortcut(P + "Radial Menu (Hold)", typeof(SceneView), Tag, KeyCode.C)]
        private static void Radial(ShortcutArguments args)
        {
            if (args.stage == ShortcutStage.Begin) SceneHud.OpenRadial();
            else SceneHud.CloseRadial(true);
        }

        [ClutchShortcut(P + "Pause (Hold)", typeof(SceneView), Tag, KeyCode.Space)]
        private static void Pause(ShortcutArguments args)
        {
            var s = SessionController.Current;
            if (s == null) return;
            s.Interaction.SpaceHeld = args.stage == ShortcutStage.Begin;
            SceneView.RepaintAll();
        }
    }
}
