using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// ツールタブ（UI 設計 §4）。状態を持たず、表示はすべて SettingsService / EditState / SessionController から作る。
    /// ウィンドウを閉じると作業を終える（リロードでは OnDestroy は呼ばれない）。
    /// </summary>
    internal sealed class MaskMakerWindow : EditorWindow
    {
        private Label _versionLabel;
        private Label _status;
        private Button _statusAction;
        private IVisualElementScheduledItem _statusReset;
        private DennokoVersionChecker.Result _versionResult =
            new DennokoVersionChecker.Result { State = DennokoVersionChecker.State.Checking, LocalVersion = "0.0.0" };

        [MenuItem("dennokoworks/MaskMaker V4")]
        public static void Open()
        {
            var w = GetWindow<MaskMakerWindow>();
            w.titleContent = new GUIContent("MaskMaker V4");
            w.minSize = new Vector2(320, 420);
        }

        /// <summary>右クリック・自動再開から、フォーカスを奪わずに開く。</summary>
        public static void EnsureOpen()
        {
            if (HasOpenInstances<MaskMakerWindow>()) return;
            var w = GetWindow<MaskMakerWindow>(false, "MaskMaker V4", false);
            w.minSize = new Vector2(320, 420);
        }

        private void OnEnable()
        {
            Notifier.Posted += OnMessage;
        }

        private void OnDisable()
        {
            Notifier.Posted -= OnMessage;
        }

        private void OnDestroy()
        {
            SessionController.End();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            UiAssets.SetupRoot(root);
            root.style.flexGrow = 1;
            var tree = UiAssets.LoadTree(UiAssets.WindowUxmlGuid);
            if (tree == null)
            {
                root.Add(new Label("UXML Asset not found."));
                return;
            }
            tree.CloneTree(root);

            new TargetCard(root);
            new UVCard(root);
            new OutputCard(root);
            BindHeader(root);
            BindSummary(root);
            BindFooter(root);

            _status = root.Q<Label>("status-label");
            _statusAction = root.Q<Button>("status-action");
            Bind.Show(_statusAction, false);
            _status.RegisterCallback<ClickEvent>(_ => PingStatus());

            UiHub.Watch(root, () =>
            {
                Bind.Show(root.Q("uv-card"), UiRules.Working);
                Bind.Show(root.Q("output-card"), UiRules.Working);
                Bind.Show(root.Q("summary-row"), UiRules.Working);
            });
        }

        // ── ヘッダー ──

        private void BindHeader(VisualElement root)
        {
            _versionLabel = root.Q<Label>("version-label");
            var reload = root.Q<Button>("version-reload-button");
            reload.tooltip = L10n.T("tooltip.reload");
            reload.clicked += () =>
            {
                MaskMakerVersion.ForceRecheck();
                LoadVersionResultFromSessionState();
            };
            var settings = root.Q<Button>("settings-button");
            Icons.Apply(settings, Icons.Settings, "⚙", L10n.T("tooltip.settings"));
            settings.clicked += () =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem("Preferences", false, () => SettingsService_Open(true));
                menu.AddItem("Project Settings", false, () => SettingsService_Open(false));
                menu.DropDown(settings.worldBound, settings, false);
            };
            LoadVersionResultFromSessionState();
            MaskMakerVersion.StartCheckBackgroundTask();
        }

        private static void SettingsService_Open(bool user)
        {
            if (user) UnityEditor.SettingsService.OpenUserPreferences(MaskMakerSettingsPages.PreferencesPath);
            else UnityEditor.SettingsService.OpenProjectSettings(MaskMakerSettingsPages.ProjectPath);
        }

        internal void LoadVersionResultFromSessionState()
        {
            string local = MaskMakerVersion.Current;
            string latest = SessionState.GetString(MaskMakerVersion.VerCheckLatestKey, string.Empty);
            bool done = SessionState.GetBool(MaskMakerVersion.VerCheckDoneKey, false);
            bool error = SessionState.GetBool(MaskMakerVersion.VerCheckErrorKey, false);
            DennokoVersionChecker.State state;
            if (!done) state = DennokoVersionChecker.State.Checking;
            else if (error || string.IsNullOrEmpty(latest)) state = DennokoVersionChecker.State.Error;
            else if (DennokoVersionChecker.IsUpdateAvailable(latest, local)) state = DennokoVersionChecker.State.UpdateAvailable;
            else state = DennokoVersionChecker.State.UpToDate;
            _versionResult = new DennokoVersionChecker.Result
            {
                State = state,
                LocalVersion = local,
                LatestVersion = latest,
                Url = SessionState.GetString(MaskMakerVersion.VerCheckUrlKey, string.Empty),
            };
            if (_versionLabel == null) return;
            _versionLabel.EnableInClassList("dennoko-version-label--update", state == DennokoVersionChecker.State.UpdateAvailable);
            _versionLabel.EnableInClassList("dennoko-version-label--error", state == DennokoVersionChecker.State.Error);
            switch (state)
            {
                case DennokoVersionChecker.State.UpdateAvailable:
                    _versionLabel.text = "v" + local + "  " + L10n.F("status.version.update", latest);
                    break;
                case DennokoVersionChecker.State.Error:
                    _versionLabel.text = "v" + local + "  " + L10n.T("status.version.error");
                    break;
                default:
                    _versionLabel.text = "v" + local;
                    break;
            }
        }

        // ── 出力サマリ（値のチップ。UI 設計 §4.3） ──

        private void BindSummary(VisualElement root)
        {
            var row = root.Q("summary-row");
            UiHub.Watch(row, () =>
            {
                row.Clear();
                var s = SessionController.Current;
                if (s == null) return;
                string folder = SettingsService.Get(SettingKeys.UseTextureFolder) && s.BaseTexture != null
                    ? Path.GetFileName(OutputFolderPreview(s))
                    : Path.GetFileName(SettingsService.Get(SettingKeys.OutputDir).TrimEnd('/'));
                row.Add(Chip(Icons.Folder, folder, false, () => SettingsService_Open(false)));
                row.Add(Chip(null, L10n.T("output.margin") + UiRules.EffectiveMargin(s), SettingsService.IsOverridden(SettingKeys.PixelMargin), OpenDetails));
                if (SettingsService.Get(SettingKeys.InvertMask))
                    row.Add(Chip(null, L10n.T("output.invert"), SettingsService.IsOverridden(SettingKeys.InvertMask), OpenDetails));
                if (SettingsService.Get(SettingKeys.SaveInvertedToo))
                    row.Add(Chip(null, "_inv", SettingsService.IsOverridden(SettingKeys.SaveInvertedToo), OpenDetails));
            });

            void OpenDetails()
            {
                var fold = root.Q<Foldout>("details-foldout");
                if (fold != null) fold.value = true;
            }
        }

        private static string OutputFolderPreview(WorkSession s)
        {
            var path = AssetDatabase.GetAssetPath(s.BaseTexture);
            return string.IsNullOrEmpty(path) ? SettingsService.Get(SettingKeys.OutputDir) : Path.GetDirectoryName(path);
        }

        private static VisualElement Chip(string icon, string text, bool overridden, System.Action onClick)
        {
            var chip = new Button(onClick) { text = string.Empty };
            chip.AddToClassList("mm-chip");
            if (icon != null && Icons.Get(icon) != null)
            {
                var img = new Image { image = Icons.Get(icon) };
                img.AddToClassList("mm-chip-icon");
                chip.Add(img);
            }
            var label = new Label(text);
            label.AddToClassList("dennoko-text-tertiary");
            chip.Add(label);
            if (overridden)
            {
                var dot = new VisualElement();
                dot.AddToClassList("mm-override-dot");
                chip.Add(dot);
            }
            return chip;
        }

        // ── フッター（主ボタン 1 つ） ──

        private void BindFooter(VisualElement root)
        {
            var primary = root.Q<Button>("primary-button");
            var field = root.Q<UnityEditor.UIElements.ObjectField>("target-field");
            primary.clicked += () =>
            {
                if (SessionController.IsWorking) SessionCommands.Export(MessageOrigin.Window);
                else SessionController.Start(TargetCard.PendingTarget ?? field?.value as Renderer);
            };
            UiHub.Watch(primary, () =>
            {
                bool working = UiRules.Working;
                primary.text = working ? L10n.T("button.export") : L10n.T("button.start");
                primary.SetEnabled(working || TargetCard.PendingTarget != null || field?.value is Renderer);
            });
        }

        // ── ステータスバー ──

        private string _statusAsset;

        private void OnMessage(UserMessage m)
        {
            // シーンから出した通知はトーストだけ（出口は 1 か所。UI 設計 §2.6.1 原則 4）
            if (m.Origin == MessageOrigin.Scene && SceneView.lastActiveSceneView != null && SessionController.IsWorking) return;
            if (_status == null) return;
            _status.text = m.Text;
            _status.EnableInClassList("dennoko-status--success", m.Level == MessageLevel.Success);
            _status.EnableInClassList("dennoko-status--error", m.Level == MessageLevel.Error);
            _status.EnableInClassList("dennoko-text-warning", m.Level == MessageLevel.Warning);
            _statusAsset = m.AssetPath;

            _statusAction.clickable = new Clickable(() => { });
            Bind.Show(_statusAction, m.Action != null);
            if (m.Action != null)
            {
                _statusAction.text = m.ActionLabel;
                var action = m.Action;
                _statusAction.clickable = new Clickable(() =>
                {
                    action(_statusAction);
                    Bind.Show(_statusAction, false);
                });
            }

            _statusReset?.Pause();
            if (m.Level == MessageLevel.Info || m.Level == MessageLevel.Success)
                _statusReset = _status.schedule.Execute(ClearStatus).StartingIn(m.Action != null ? 6000 : 4000);
        }

        private void ClearStatus()
        {
            _status.text = string.Empty;
            _status.RemoveFromClassList("dennoko-status--success");
            _status.RemoveFromClassList("dennoko-status--error");
            _status.RemoveFromClassList("dennoko-text-warning");
            Bind.Show(_statusAction, false);
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
