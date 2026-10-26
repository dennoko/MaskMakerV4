using Dennoko.UVMaskMaker.Core;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    public enum PaintSubMode
    {
        Brush = 0,
        Rect = 1,
        Lasso = 2,
        Eraser = 3,
    }

    public enum UVLayoutBackground
    {
        Transparent = 0,
        BaseTexture = 1,
    }

    public enum UVLayoutLineColor
    {
        White = 0,
        Black = 1,
        Cyan = 2,
        Red = 3,
    }

    public enum UVLayoutFillColor
    {
        Islands = 0,
        White = 1,
        Black = 2,
        Cyan = 3,
    }

    public enum UILanguage
    {
        Auto = 0,
        Japanese = 1,
        English = 2,
    }

    /// <summary>
    /// 全設定の宣言（Impl work_context_and_rendering §4.3 と 1 対 1）。設定の追加はここに 1 行足す。
    /// 「自動」を表す既定値: TextureSize = 0、PixelMargin = -1、FileName = ""、MirrorTolerance = 0。
    /// </summary>
    public static class SettingKeys
    {
        private const SettingLayer S = SettingLayer.Session;
        private const SettingLayer T = SettingLayer.Target;
        private const SettingLayer P = SettingLayer.Project;
        private const SettingLayer U = SettingLayer.User;

        // ── Session（作業中の状態） ──
        public static readonly Setting<bool> IsPaintMode = new Setting<bool>("IsPaintMode", false, S);
        public static readonly Setting<SelectionGranularity> Granularity = new Setting<SelectionGranularity>("Granularity", SelectionGranularity.UVIsland, U);
        public static readonly Setting<PaintSubMode> PaintTool = new Setting<PaintSubMode>("PaintSubMode", PaintSubMode.Brush, S);
        public static readonly Setting<int> BrushSize = new Setting<int>("BrushSize", 30, U);
        public static readonly Setting<bool> SymmetryEnabled = new Setting<bool>("SymmetryEnabled", false, S);
        public static readonly Setting<bool> ScenePickPaused = new Setting<bool>("ScenePickPaused", false, S);
        public static readonly Setting<bool> OverlayOnTop = new Setting<bool>("OverlayOnTop", false, S);
        public static readonly Setting<bool> IsolateTargetMesh = new Setting<bool>("IsolateTargetMesh", true, S);

        // ── Target（対象ごと） ──
        public static readonly Setting<int> UVChannel = new Setting<int>("UVChannel", 0, T);
        public static readonly Setting<int> TargetSubmesh = new Setting<int>("TargetSubmesh", 0, T);
        public static readonly Setting<string> FileName = new Setting<string>("FileName", "", T);
        public static readonly Setting<int> TextureSize = new Setting<int>("TextureSize", 0, T);
        public static readonly Setting<bool> ChannelWriteEnabled = new Setting<bool>("ChannelWriteEnabled", false, T);
        public static readonly Setting<bool> WriteR = new Setting<bool>("WriteR", true, T);
        public static readonly Setting<bool> WriteG = new Setting<bool>("WriteG", false, T);
        public static readonly Setting<bool> WriteB = new Setting<bool>("WriteB", false, T);
        public static readonly Setting<bool> WriteA = new Setting<bool>("WriteA", false, T);
        public static readonly Setting<string> BasePng = new Setting<string>("BasePng", "", T);
        public static readonly Setting<bool> PreviewOverlayBaseTex = new Setting<bool>("PreviewOverlayBaseTex", false, T);
        public static readonly Setting<float> PreviewOverlayAlpha = new Setting<float>("PreviewOverlayAlpha", 0.6f, T);
        public static readonly Setting<int> UVLayoutSize = new Setting<int>("UVLayoutSize", 2048, T);
        public static readonly Setting<bool> UVLayoutWireframe = new Setting<bool>("UVLayoutWireframe", true, T);
        public static readonly Setting<bool> UVLayoutSeams = new Setting<bool>("UVLayoutSeams", true, T);
        public static readonly Setting<bool> UVLayoutSelectionOnly = new Setting<bool>("UVLayoutSelectionOnly", false, T);
        public static readonly Setting<bool> UVLayoutColorFill = new Setting<bool>("UVLayoutColorFill", false, T);
        public static readonly Setting<UVLayoutFillColor> UVLayoutFill = new Setting<UVLayoutFillColor>("UVLayoutFillColor", UVLayoutFillColor.Islands, T);
        public static readonly Setting<UVLayoutBackground> UVLayoutBackgroundMode = new Setting<UVLayoutBackground>("UVLayoutBackground", UVLayoutBackground.Transparent, T);
        public static readonly Setting<UVLayoutLineColor> UVLayoutWireColor = new Setting<UVLayoutLineColor>("UVLayoutLineColor", UVLayoutLineColor.White, T);
        public static readonly Setting<UVLayoutLineColor> UVLayoutSeamColor = new Setting<UVLayoutLineColor>("UVLayoutSeamColor", UVLayoutLineColor.Red, T);
        public static readonly Setting<string> MaterialSlot = new Setting<string>("MaterialSlot", "", T);
        public static readonly Setting<string> LastOutputPath = new Setting<string>("LastOutputPath", "", T);
        public static readonly Setting<string> LastOutputInvPath = new Setting<string>("LastOutputInvPath", "", T);
        public static readonly Setting<bool> InvertMask = new Setting<bool>("InvertMask", false, T);
        public static readonly Setting<int> PixelMargin = new Setting<int>("PixelMargin", -1, T);
        public static readonly Setting<bool> SaveInvertedToo = new Setting<bool>("SaveInvertedToo", false, T);

        // ── Project ──
        public static readonly Setting<int> DefaultTextureSize = new Setting<int>("DefaultTextureSize", 512, P);
        public static readonly Setting<bool> UseTextureFolder = new Setting<bool>("UseTextureFolder", true, P);
        public static readonly Setting<string> OutputDir = new Setting<string>("OutputDir", "Assets/GeneratedMasks", P);
        public static readonly Setting<bool> AutoApplyMaterial = new Setting<bool>("AutoApplyMaterial", false, P);
        public static readonly Setting<int> BlackThreshold = new Setting<int>("BlackThreshold", 128, P);
        public static readonly Setting<float> MirrorTolerance = new Setting<float>("MirrorTolerance", 0f, P);
        public static readonly Setting<bool> ImportAsSingleChannel = new Setting<bool>("ImportAsSingleChannel", false, P);

        // ── User ──
        public static readonly Setting<Color> SelectedSceneColor = new Setting<Color>("SelectedSceneColor", new Color(0f, 1f, 1f, 1f), U);
        public static readonly Setting<float> SceneMaskAlpha = new Setting<float>("SceneMaskAlpha", 0.6f, U);
        public static readonly Setting<Color> SeamColor = new Setting<Color>("SeamColor", new Color(1f, 0.15f, 0.15f, 1f), U);
        public static readonly Setting<float> OverlaySeamThickness = new Setting<float>("OverlaySeamThickness", 2.5f, U);
        public static readonly Setting<UILanguage> Language = new Setting<UILanguage>("Language", UILanguage.Auto, U);

        public const string HintCountPrefix = "HintShownCount.";

        public static Setting<int> HintShownCount(string hintId) => new Setting<int>(HintCountPrefix + hintId, 0, U);

        public static readonly int[] TextureSizes = { 512, 1024, 2048, 4096 };
        public const int MinBrushSize = 1;
        public const int MaxBrushSize = 300;
        public const int MaxPixelMargin = 32;
    }
}
