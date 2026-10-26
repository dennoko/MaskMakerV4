using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// UV 展開図の書き出しタブ（UI 設計 §4.3）。UV ビューの [Camera] ボタンで、ツールタブの隣に開く。
    /// 状態を持たず、表示はすべて SettingsService / SessionController から作る。閉じても作業は続く。
    /// </summary>
    internal sealed class UVLayoutWindow : EditorWindow
    {
        private Label _status;
        private IVisualElementScheduledItem _statusReset;
        private string _statusAsset;

        /// <summary>ツールタブの隣にタブとして開く（既に開いていれば前に出す）。</summary>
        public static void Open()
        {
            var w = GetWindow<UVLayoutWindow>(L10n.T("layout.title"), true, typeof(MaskMakerWindow));
            w.minSize = new Vector2(280, 320);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(L10n.T("layout.title"));
            Notifier.Posted += OnMessage;
        }

        private void OnDisable()
        {
            Notifier.Posted -= OnMessage;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            UiAssets.SetupRoot(root);
            root.style.flexGrow = 1;
            var tree = UiAssets.LoadTree(UiAssets.LayoutUxmlGuid);
            if (tree == null)
            {
                root.Add(new Label("UXML Asset not found."));
                return;
            }
            tree.CloneTree(root);

            root.Q<Label>("layout-title").text = L10n.T("layout.title");
            root.Q<Label>("layout-size-label").text = L10n.T("layout.size");
            root.Q<Label>("layout-elements-label").text = L10n.T("layout.elements");
            root.Q<Label>("layout-background-label").text = L10n.T("layout.background");
            root.Q<Label>("layout-line-label").text = L10n.T("layout.line");

            Bind.Choice(new List<(Button, int)>
            {
                (root.Q<Button>("layout-size-512"), 512),
                (root.Q<Button>("layout-size-1024"), 1024),
                (root.Q<Button>("layout-size-2048"), 2048),
                (root.Q<Button>("layout-size-4096"), 4096),
            }, SettingKeys.UVLayoutSize, () => SettingsService.Get(SettingKeys.UVLayoutSize));

            Toggle(root, "layout-wire", "layout.wire", SettingKeys.UVLayoutWireframe);
            Toggle(root, "layout-seams", "layout.seams", SettingKeys.UVLayoutSeams);
            Toggle(root, "layout-selection-only", "layout.selectionOnly", SettingKeys.UVLayoutSelectionOnly);
            Toggle(root, "layout-color-fill", "layout.colorFill", SettingKeys.UVLayoutColorFill);

            var bgItems = new List<(Button, UVLayoutBackground)>
            {
                (Text(root, "layout-bg-transparent", "layout.bgTransparent"), UVLayoutBackground.Transparent),
                (Text(root, "layout-bg-base", "layout.bgBase"), UVLayoutBackground.BaseTexture),
            };
            Bind.Choice(bgItems, SettingKeys.UVLayoutBackgroundMode, () => SettingsService.Get(SettingKeys.UVLayoutBackgroundMode));

            var lineItems = new List<(Button, UVLayoutLineColor)>
            {
                (Text(root, "layout-line-white", "layout.white"), UVLayoutLineColor.White),
                (Text(root, "layout-line-black", "layout.black"), UVLayoutLineColor.Black),
                (Text(root, "layout-line-cyan", "layout.cyan"), UVLayoutLineColor.Cyan),
            };
            Bind.Choice(lineItems, SettingKeys.UVLayoutLine, () => SettingsService.Get(SettingKeys.UVLayoutLine));

            var write = Text(root, "layout-write", "button.writeLayout");
            write.clicked += SessionCommands.ExportUVLayout;

            // 対象・保存名・書き出しの可否は作業の有無に従う
            var target = root.Q<Label>("layout-target");
            var file = root.Q<Label>("layout-file");
            UiHub.Watch(root, () =>
            {
                var s = SessionController.Current;
                target.text = s != null ? s.Target.name : string.Empty;
                file.text = s != null ? s.EffectiveFileName + "_uv.png" : L10n.T("layout.noSession");
                write.SetEnabled(s != null);
            });

            _status = root.Q<Label>("status-label");
            _status.RegisterCallback<ClickEvent>(_ => PingStatus());
        }

        private static void Toggle(VisualElement root, string name, string textKey, Setting<bool> s)
        {
            var t = root.Q<Toggle>(name);
            t.text = L10n.T(textKey);
            Bind.Toggle(t, s);
        }

        private static Button Text(VisualElement root, string name, string textKey)
        {
            var b = root.Q<Button>(name);
            b.text = L10n.T(textKey);
            return b;
        }

        // ── ステータスバー（このタブを開いている間の書き出し結果） ──

        private void OnMessage(UserMessage m)
        {
            if (_status == null || m.Origin != MessageOrigin.Window) return;
            _status.text = m.Text;
            _status.EnableInClassList("dennoko-status--success", m.Level == MessageLevel.Success);
            _status.EnableInClassList("dennoko-status--error", m.Level == MessageLevel.Error);
            _status.EnableInClassList("dennoko-text-warning", m.Level == MessageLevel.Warning);
            _statusAsset = m.AssetPath;
            _statusReset?.Pause();
            if (m.Level == MessageLevel.Info || m.Level == MessageLevel.Success)
                _statusReset = _status.schedule.Execute(ClearStatus).StartingIn(4000);
        }

        private void ClearStatus()
        {
            _status.text = string.Empty;
            _status.RemoveFromClassList("dennoko-status--success");
            _status.RemoveFromClassList("dennoko-status--error");
            _status.RemoveFromClassList("dennoko-text-warning");
            _statusAsset = null;
        }

        private void PingStatus()
        {
            if (string.IsNullOrEmpty(_statusAsset)) return;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(_statusAsset);
            if (obj != null) EditorGUIUtility.PingObject(obj);
        }
    }
}
