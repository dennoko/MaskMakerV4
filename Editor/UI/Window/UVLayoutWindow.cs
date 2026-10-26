using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// UV 展開図の書き出しウィンドウ（UI 設計 §4.3）。UV ビューの [Camera] ボタンで、フローティングで開く。
    /// 書き出しかキャンセルで閉じる。状態を持たず、表示はすべて SettingsService / SessionController から作る。
    /// </summary>
    internal sealed class UVLayoutWindow : EditorWindow
    {
        private static readonly Vector2 DefaultSize = new Vector2(320, 520);

        private Label _status;
        private IVisualElementScheduledItem _statusReset;
        private string _statusAsset;

        /// <summary>ツールタブの上にフローティングで開く（既に開いていれば前に出す）。</summary>
        public static void Open()
        {
            if (HasOpenInstances<UVLayoutWindow>())
            {
                FocusWindowIfItsOpen<UVLayoutWindow>();
                return;
            }
            var w = CreateInstance<UVLayoutWindow>();
            w.minSize = new Vector2(280, 320);
            w.position = CenteredOn(OwnerRect(), DefaultSize);
            w.ShowUtility();
        }

        /// <summary>ツールタブの位置。見つからなければエディタ本体のウィンドウ。</summary>
        private static Rect OwnerRect()
        {
            var tools = Resources.FindObjectsOfTypeAll<MaskMakerWindow>();
            return tools.Length > 0 ? tools[0].position : EditorGUIUtility.GetMainWindowPosition();
        }

        private static Rect CenteredOn(Rect owner, Vector2 size)
        {
            return new Rect(owner.center - size * 0.5f, size);
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
            root.Q<Label>("layout-seam-color-label").text = L10n.T("layout.seamColor");
            root.Q<Label>("layout-wire-color-label").text = L10n.T("layout.wireColor");
            root.Q<Label>("layout-fill-label").text = L10n.T("layout.fill");

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

            LineColorChoice(root, "layout-seam", SettingKeys.UVLayoutSeamColor);
            LineColorChoice(root, "layout-wire", SettingKeys.UVLayoutWireColor);

            var fillItems = new List<(Button, UVLayoutFillColor)>
            {
                (Text(root, "layout-fill-islands", "layout.islands"), UVLayoutFillColor.Islands),
                (Text(root, "layout-fill-white", "layout.white"), UVLayoutFillColor.White),
                (Text(root, "layout-fill-black", "layout.black"), UVLayoutFillColor.Black),
                (Text(root, "layout-fill-cyan", "layout.cyan"), UVLayoutFillColor.Cyan),
            };
            Bind.Choice(fillItems, SettingKeys.UVLayoutFill, () => SettingsService.Get(SettingKeys.UVLayoutFill));

            // 書き出せたら閉じる。失敗時は開いたまま、このウィンドウのステータスにエラーを出す
            var write = Text(root, "layout-write", "button.writeLayout");
            write.clicked += () =>
            {
                if (SessionCommands.ExportUVLayout()) Dismiss();
            };

            // 書き出さずに閉じる（Esc でも同じ）
            Text(root, "layout-cancel", "button.cancel").clicked += Dismiss;
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                e.StopPropagation();
                Dismiss();
            }, TrickleDown.TrickleDown);

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

        /// <summary>ウィンドウを閉じてツールタブへ戻る。設定は書き換えた時点で保存済みなので、閉じるだけでよい。</summary>
        private void Dismiss()
        {
            Close();
            if (HasOpenInstances<MaskMakerWindow>()) FocusWindowIfItsOpen<MaskMakerWindow>();
        }

        private static void Toggle(VisualElement root, string name, string textKey, Setting<bool> s)
        {
            var t = root.Q<Toggle>(name);
            t.text = L10n.T(textKey);
            Bind.Toggle(t, s);
        }

        private static void LineColorChoice(VisualElement root, string prefix, Setting<UVLayoutLineColor> s)
        {
            var items = new List<(Button, UVLayoutLineColor)>
            {
                (Text(root, prefix + "-red", "layout.red"), UVLayoutLineColor.Red),
                (Text(root, prefix + "-white", "layout.white"), UVLayoutLineColor.White),
                (Text(root, prefix + "-black", "layout.black"), UVLayoutLineColor.Black),
                (Text(root, prefix + "-cyan", "layout.cyan"), UVLayoutLineColor.Cyan),
            };
            Bind.Choice(items, s, () => SettingsService.Get(s));
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
