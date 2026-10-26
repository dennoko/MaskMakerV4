using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// Preferences（User 層）と Project Settings（Project 層）。D ランクの設定だけを置く（UI 設計 §2.5.3）。
    /// 項目は SettingKeys の宣言を並べるだけの一覧なので、C# で行を作る。
    /// </summary>
    internal static class MaskMakerSettingsPages
    {
        public const string PreferencesPath = "Preferences/MaskMaker V4";
        public const string ProjectPath = "Project/MaskMaker V4";

        [SettingsProvider]
        private static SettingsProvider Preferences()
        {
            return new SettingsProvider(PreferencesPath, SettingsScope.User)
            {
                label = "MaskMaker V4",
                activateHandler = (search, root) =>
                {
                    var page = Page(root);
                    ColorRow(page, "prefs.selectedColor", SettingKeys.SelectedSceneColor, SettingLayer.User);
                    FloatRow(page, "prefs.maskAlpha", SettingKeys.SceneMaskAlpha, SettingLayer.User, 0f, 1f);
                    ColorRow(page, "prefs.seamColor", SettingKeys.SeamColor, SettingLayer.User);
                    FloatRow(page, "prefs.seamThickness", SettingKeys.OverlaySeamThickness, SettingLayer.User, 0.5f, 8f);
                    BoolRow(page, "prefs.xray", SettingKeys.OverlayOnTop, SettingLayer.User);
                    BoolRow(page, "prefs.isolate", SettingKeys.IsolateTargetMesh, SettingLayer.User);
                    EnumRow(page, "prefs.language", SettingKeys.Language, SettingLayer.User);
                    page.Add(new Button(SettingsService.ResetHintCounts) { text = L10n.T("prefs.resetHints") });
                    page.Add(new Button(() => EditorApplication.ExecuteMenuItem("Edit/Shortcuts...")) { text = L10n.T("prefs.shortcuts") });
                    page.Add(new Button(Icons.ReportMissing) { text = "Check icons" });
                },
                keywords = new HashSet<string>(new[] { "MaskMaker", "UV", "Mask" }),
            };
        }

        [SettingsProvider]
        private static SettingsProvider Project()
        {
            return new SettingsProvider(ProjectPath, SettingsScope.Project)
            {
                label = "MaskMaker V4",
                activateHandler = (search, root) =>
                {
                    var page = Page(root);
                    IntChoiceRow(page, "project.defaultSize", SettingKeys.DefaultTextureSize, SettingKeys.TextureSizes);
                    BoolRow(page, "project.useTextureFolder", SettingKeys.UseTextureFolder, SettingLayer.Project);
                    TextRow(page, "project.outputDir", SettingKeys.OutputDir, SettingLayer.Project);
                    BoolRow(page, "project.invert", SettingKeys.InvertMask, SettingLayer.Project);
                    IntRow(page, "project.margin", SettingKeys.PixelMargin, SettingLayer.Project, -1, SettingKeys.MaxPixelMargin);
                    BoolRow(page, "project.saveInverted", SettingKeys.SaveInvertedToo, SettingLayer.Project);
                    BoolRow(page, "project.autoApply", SettingKeys.AutoApplyMaterial, SettingLayer.Project);
                    IntRow(page, "project.threshold", SettingKeys.BlackThreshold, SettingLayer.Project, 0, 255);
                    FloatRow(page, "project.mirrorTolerance", SettingKeys.MirrorTolerance, SettingLayer.Project, 0f, 0.05f);
                    BoolRow(page, "project.singleChannel", SettingKeys.ImportAsSingleChannel, SettingLayer.Project);
                },
                keywords = new HashSet<string>(new[] { "MaskMaker", "UV", "Mask" }),
            };
        }

        private static VisualElement Page(VisualElement root)
        {
            var page = new ScrollView();
            UiAssets.SetupRoot(page);
            page.style.paddingLeft = 10;
            page.style.paddingTop = 10;
            page.style.paddingRight = 10;
            root.Add(page);
            return page;
        }

        // 各行は「その層から下で解決した値」を表示し、その層へ書く

        private static void BoolRow(VisualElement page, string key, Setting<bool> s, SettingLayer layer)
        {
            var f = new Toggle(L10n.T(key));
            f.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue, layer));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, layer)));
            page.Add(f);
        }

        private static void IntRow(VisualElement page, string key, Setting<int> s, SettingLayer layer, int min, int max)
        {
            var f = new SliderInt(L10n.T(key), min, max) { showInputField = true };
            f.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue, layer));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, layer)));
            page.Add(f);
        }

        private static void FloatRow(VisualElement page, string key, Setting<float> s, SettingLayer layer, float min, float max)
        {
            var f = new Slider(L10n.T(key), min, max) { showInputField = true };
            f.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue, layer));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, layer)));
            page.Add(f);
        }

        private static void TextRow(VisualElement page, string key, Setting<string> s, SettingLayer layer)
        {
            var f = new TextField(L10n.T(key)) { isDelayed = true };
            f.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue, layer));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, layer)));
            page.Add(f);
        }

        private static void ColorRow(VisualElement page, string key, Setting<Color> s, SettingLayer layer)
        {
            var f = new ColorField(L10n.T(key));
            f.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue, layer));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, layer)));
            page.Add(f);
        }

        private static void EnumRow<T>(VisualElement page, string key, Setting<T> s, SettingLayer layer) where T : Enum
        {
            var f = new EnumField(L10n.T(key), s.Default);
            f.RegisterValueChangedCallback(e => SettingsService.Set(s, (T)e.newValue, layer));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, layer)));
            page.Add(f);
        }

        private static void IntChoiceRow(VisualElement page, string key, Setting<int> s, int[] choices)
        {
            var names = new List<string>();
            foreach (int c in choices) names.Add(c.ToString());
            var f = new DropdownField(L10n.T(key), names, 0);
            f.RegisterValueChangedCallback(_ => SettingsService.Set(s, choices[Mathf.Max(0, f.index)], SettingLayer.Project));
            UiHub.Watch(f, () => f.SetValueWithoutNotify(SettingsService.GetFrom(s, SettingLayer.Project).ToString()));
            page.Add(f);
        }
    }

    /// <summary>Hierarchy の右クリック「dennokoworks/MaskMaker V4」（UI 設計 §3）。</summary>
    internal static class MaskMakerMenus
    {
        private const string Path = "GameObject/dennokoworks/MaskMaker V4";

        [MenuItem(Path, false, 49)]
        private static void EditSelected()
        {
            var r = TargetResolver.FromSelection();
            if (r == null) return;
            MaskMakerWindow.EnsureOpen(bringToFront: true);
            SessionController.Start(r);
        }

        [MenuItem(Path, true)]
        private static bool CanEditSelected() => TargetResolver.FromSelection() != null;
    }
}
