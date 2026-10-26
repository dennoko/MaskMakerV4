using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>表示文字列（ja / en）。用語は UI 設計 §2.6.5 に固定する。</summary>
    public static class L10n
    {
        private static readonly Dictionary<string, (string ja, string en)> Table = new Dictionary<string, (string, string)>
        {
            // 用語
            { "term.island", ("島", "Island") },
            { "term.part", ("パーツ", "Part") },
            { "term.face", ("面", "Face") },
            { "term.select", ("選択", "Select") },
            { "term.paint", ("ペイント", "Paint") },
            { "term.brush", ("ブラシ", "Brush") },
            { "term.rect", ("矩形", "Rect") },
            { "term.lasso", ("投げ縄", "Lasso") },
            { "term.eraser", ("消しゴム", "Eraser") },
            { "term.symmetry", ("対称", "Symmetry") },
            { "term.xray", ("X-Ray", "X-Ray") },
            { "term.isolate", ("孤立表示", "Isolate") },
            { "term.pause", ("一時停止", "Pause") },
            { "term.undo", ("元に戻す", "Undo") },
            { "term.redo", ("やり直す", "Redo") },
            { "term.clear", ("クリア", "Clear") },
            { "term.selectAll", ("全選択", "Select all") },
            { "term.invert", ("反転", "Invert") },
            { "term.export", ("出力", "Export") },
            { "term.apply", ("適用", "Apply") },
            { "term.all", ("全体", "All") },

            // ツールタブ
            { "window.title", ("MaskMaker V4", "MaskMaker V4") },
            { "button.start", ("作業開始", "Start") },
            { "button.end", ("終了", "End") },
            { "button.export", ("出  力", "Export") },
            { "button.writeLayout", ("書き出し", "Write") },
            { "target.placeholder", ("Renderer を選択", "Select a Renderer") },
            { "output.header", ("出力", "Output") },
            { "output.channel", ("チャンネル", "Channels") },
            { "output.details", ("詳細", "Details") },
            { "output.invert", ("反転", "Invert") },
            { "output.margin", ("余白", "Margin") },
            { "output.saveInverted", ("反転も保存", "Also save inverted") },
            { "output.basePng", ("Base PNG", "Base PNG") },
            { "uv.drop", ("取り込む", "Import") },
            { "uv.baseOverlay", ("ベーステクスチャを重ねる", "Overlay base texture") },
            { "layout.size", ("解像度", "Size") },
            { "layout.wire", ("ポリゴン線", "Wireframe") },
            { "layout.seams", ("シーム線", "Seams") },
            { "layout.selectionOnly", ("選択限定", "Selection only") },
            { "layout.colorFill", ("色分け", "Color fill") },
            { "layout.background", ("背景", "Background") },
            { "layout.bgTransparent", ("透過", "Transparent") },
            { "layout.bgBase", ("ベース", "Base") },
            { "layout.line", ("線色", "Line") },
            { "layout.white", ("白", "White") },
            { "layout.black", ("黒", "Black") },
            { "layout.cyan", ("シアン", "Cyan") },
            { "summary.folder.texture", ("テクスチャと同じ場所", "Next to texture") },
            { "tooltip.settings", ("設定", "Settings") },
            { "tooltip.reload", ("更新を確認", "Check for updates") },
            { "tooltip.baseOverlay", ("ベーステクスチャを重ねる", "Overlay base texture") },
            { "tooltip.layout", ("UV 展開図を書き出す", "Export UV layout") },
            { "tooltip.overrideDot", ("既定に戻す", "Reset to default") },

            // ステータス・トースト
            { "status.saved", ("保存: {0}", "Saved: {0}") },
            { "status.updated", ("更新: {0}", "Updated: {0}") },
            { "status.applied", ("適用: {0}", "Applied: {0}") },
            { "status.layoutSaved", ("保存: {0}", "Saved: {0}") },
            { "status.restored", ("前回の作業を復元", "Restored previous work") },
            { "status.meshChanged", ("メッシュ変更のため復元なし", "Mesh changed; not restored") },
            { "status.uvFallback", ("UV{0} なし → UV0 を使用", "UV{0} missing; using UV0") },
            { "status.empty", ("選択なし（全白で出力）", "Nothing selected (exported white)") },
            { "status.nothingToImport", ("取り込める部分なし", "Nothing to import") },
            { "status.imported", ("取り込み: {0}", "Imported: {0}") },
            { "status.cleared", ("クリアしました", "Cleared") },
            { "status.writeFailed", ("書き込みできません", "Cannot write file") },
            { "status.version.checking", ("確認中…", "Checking…") },
            { "status.version.update", ("v{0} 更新あり", "v{0} available") },
            { "status.version.error", ("確認できません", "Check failed") },
            { "info.readWriteEnabled", ("Read/Write を有効化しました", "Enabled Read/Write") },
            { "info.originalMesh", ("元のメッシュで作業中", "Working on the original mesh") },

            // エラー
            { "error.noMesh", ("メッシュがありません", "No mesh") },
            { "error.notReadable", ("メッシュを読み込めません", "Mesh is not readable") },
            { "error.noUV", ("UV がありません", "Mesh has no UVs") },
            { "error.shaderMissing", ("シェーダーが見つかりません", "Shaders not found") },
            { "error.gpuUnsupported", ("この GPU では動作しません", "GPU not supported") },
            { "error.startFailed", ("開始できません: {0}", "Cannot start: {0}") },

            // Undo 名
            { "undo.select", ("UV Mask: 選択", "UV Mask: Select") },
            { "undo.paint", ("UV Mask: ペイント", "UV Mask: Paint") },
            { "undo.clear", ("UV Mask: クリア", "UV Mask: Clear") },
            { "undo.import", ("UV Mask: 取り込み", "UV Mask: Import") },
            { "undo.restore", ("UV Mask: 前回の作業を復元", "UV Mask: Restore") },
            { "undo.apply", ("UV Mask: マテリアルに適用", "UV Mask: Apply to material") },

            // ヒント
            { "hint.click", ("クリックで選択", "Click to select") },
            { "hint.symmetry", ("X で左右同時", "X: both sides") },
            { "hint.ctrlErase", ("Ctrl で消去", "Hold Ctrl to erase") },

            // 設定画面
            { "prefs.selectedColor", ("選択色", "Selection color") },
            { "prefs.maskAlpha", ("マスク表示の不透明度", "Mask opacity") },
            { "prefs.seamColor", ("シーム色", "Seam color") },
            { "prefs.seamThickness", ("シームの太さ", "Seam thickness") },
            { "prefs.xray", ("X-Ray を既定で ON", "X-Ray on by default") },
            { "prefs.isolate", ("孤立表示を既定で ON", "Isolate by default") },
            { "prefs.language", ("言語", "Language") },
            { "prefs.resetHints", ("ヒントをもう一度表示", "Show hints again") },
            { "prefs.shortcuts", ("ショートカットを編集", "Edit shortcuts") },
            { "project.defaultSize", ("既定の解像度", "Default size") },
            { "project.useTextureFolder", ("テクスチャと同じ場所に保存", "Save next to texture") },
            { "project.outputDir", ("保存先フォルダ", "Output folder") },
            { "project.invert", ("反転", "Invert") },
            { "project.margin", ("余白（-1 = 自動）", "Margin (-1 = auto)") },
            { "project.saveInverted", ("反転も保存", "Also save inverted") },
            { "project.autoApply", ("出力後に自動で適用", "Apply after export") },
            { "project.threshold", ("取り込みのしきい値", "Import threshold") },
            { "project.mirrorTolerance", ("対称判定の許容誤差（0 = 自動）", "Symmetry tolerance (0 = auto)") },
            { "project.singleChannel", ("単一チャンネルで取り込む", "Import as single channel") },
        };

        public static bool Japanese
        {
            get
            {
                var lang = SettingsService.Get(SettingKeys.Language);
                if (lang == UILanguage.Japanese) return true;
                if (lang == UILanguage.English) return false;
                return Application.systemLanguage == SystemLanguage.Japanese;
            }
        }

        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var v)) return key;
            return Japanese ? v.ja : v.en;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        /// <summary>フォントのアトラスを先に作っておくための全日本語文字列。</summary>
        public static string AllJapanese()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var v in Table.Values) sb.Append(v.ja);
            return sb.ToString();
        }
    }

    internal enum MessageLevel
    {
        Info,
        Success,
        Warning,
        Error,
    }

    internal enum MessageOrigin
    {
        Window,
        Scene,
    }

    /// <summary>ステータスバー / トーストに出す 1 行（UI 設計 §2.6.1 原則 4）。</summary>
    internal sealed class UserMessage
    {
        public MessageLevel Level;
        public string Text;
        public MessageOrigin Origin;
        public string AssetPath;          // クリックで Ping
        public string ActionLabel;        // トーストのボタン（[適用] など）
        public System.Action<UnityEngine.UIElements.VisualElement> Action;  // 引数はボタン（候補リストの表示位置）
    }

    /// <summary>メッセージの出口。表示はタブのステータスバーかシーンのトーストのどちらか 1 か所。</summary>
    internal static class Notifier
    {
        public static event System.Action<UserMessage> Posted;

        public static void Post(MessageLevel level, string text, MessageOrigin origin = MessageOrigin.Window)
            => Post(new UserMessage { Level = level, Text = text, Origin = origin });

        public static void Post(UserMessage message) => Posted?.Invoke(message);
    }
}
