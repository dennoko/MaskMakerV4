using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// S3 ラジアルメニュー（C 長押し。skill blocks/radial-menu.md）のツール側。本体は DennokoRadialMenu。
    /// 項目は常に 8 個（0 = →、1 = ↗、2 = ↑、3 = ↖、4 = ←、5 = ↙、6 = ↓、7 = ↘）。
    /// ↑ モード切替・↗ 対称・↖ クリアは両モード共通にして、方向を手が覚えられるようにする。
    /// </summary>
    [InitializeOnLoad]
    internal static class MaskMakerRadialMenu
    {
        public static readonly DennokoRadialMenu Menu = new DennokoRadialMenu(
            "UVMaskMaker-Radial", BuildItems, DennokoSceneUI.SetupRoot)
        {
            CanOpen = () => SessionController.IsWorking,
        };

        static MaskMakerRadialMenu()
        {
            Menu.Executed += UiHub.Raise;
        }

        // 作業中かつ作業ツールが選ばれている間だけ有効なタグ（SessionLifecycle が登録する）
        [ClutchShortcut("MaskMaker V4/Radial Menu (Hold)", typeof(SceneView), SessionLifecycle.ShortcutTag, KeyCode.C)]
        private static void Shortcut(ShortcutArguments args)
        {
            if (args.stage == ShortcutStage.Begin) Menu.OpenAtPointer();
            else Menu.Release();
        }

        private static DennokoRadialMenu.Item[] BuildItems()
        {
            bool paint = UiRules.PaintMode;
            var items = new DennokoRadialMenu.Item[DennokoRadialMenu.SliceCount];
            items[2] = paint
                ? Item("term.select", Icons.Select, "S", "Tab", () => SessionCommands.SetPaintMode(false))
                : Item("term.paint", Icons.Paint, "P", "Tab", () => SessionCommands.SetPaintMode(true));
            items[1] = Item("term.symmetry", Icons.Symmetry, "X", "X", SessionCommands.ToggleSymmetry,
                SettingsService.Get(SettingKeys.SymmetryEnabled));
            items[3] = Item("term.clear", Icons.Clear, "C", null, () => SessionCommands.ClearAll(MessageOrigin.Scene));

            if (!paint)
            {
                var g = SettingsService.Get(SettingKeys.Granularity);
                items[0] = Unit(SelectionGranularity.UVIsland, "term.island", "1", g);
                items[7] = Unit(SelectionGranularity.ConnectedMesh, "term.part", "2", g);
                items[6] = Unit(SelectionGranularity.Polygon, "term.face", "3", g);
                items[4] = Item("term.selectAll", null, "All", "Ctrl+A", SessionCommands.SelectAll);
                items[5] = Item("term.invert", null, "Inv", "Ctrl+I", SessionCommands.InvertSelection);
            }
            else
            {
                var t = SettingsService.Get(SettingKeys.PaintTool);
                items[0] = Tool(PaintSubMode.Brush, Icons.Paint, "term.brush", "B", t);
                items[7] = Tool(PaintSubMode.Rect, Icons.Rect, "term.rect", "M", t);
                items[6] = Tool(PaintSubMode.Lasso, Icons.Lasso, "term.lasso", "L", t);
                items[5] = Tool(PaintSubMode.Eraser, Icons.Eraser, "term.eraser", "Shift+B", t);
                // 4 (←) は空き。Run を入れないので薄く表示されて選べない
            }
            return items;
        }

        private static DennokoRadialMenu.Item Item(string nameKey, string icon, string fallback, string key, System.Action run, bool active = false)
            => new DennokoRadialMenu.Item
            {
                Name = L10n.T(nameKey),
                Icon = icon,
                Fallback = fallback,
                Key = key,
                Run = run,
                Active = active,
            };

        private static DennokoRadialMenu.Item Unit(SelectionGranularity g, string nameKey, string key, SelectionGranularity current)
            => Item(nameKey, null, L10n.T(nameKey), key, () => SessionCommands.SetGranularity(g), current == g);

        private static DennokoRadialMenu.Item Tool(PaintSubMode tool, string icon, string nameKey, string key, PaintSubMode current)
            => Item(nameKey, icon, key, key, () => SessionCommands.SetPaintTool(tool), current == tool);
    }
}
