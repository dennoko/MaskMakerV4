using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// ツールタブ。skill の画面パターン P4（シーン作業）: 状態は Idle（未開始）と Working（作業中）の 2 つだけ。
    ///   Idle    : B1 対象カードと主ボタン「作業開始」だけ
    ///   Working : B7 作業中バナー + B1 + B3 UV ビュー + B5 出力カード、主ボタン「出力」、出力ドックに要約チップ
    /// 状態を持たず、表示はすべて SettingsService / EditState / SessionController から作る。
    /// ウィンドウを閉じると作業を終える（リロードでは OnDestroy は呼ばれない）。
    /// </summary>
    internal sealed class MaskMakerWindow : EditorWindow
    {
        private const string ToolName = "MaskMaker V4";
        private const string ReleaseUrl = "https://github.com/dennoko/MaskMakerV4/releases";

        private Label _versionLabel;
        private DennokoAppBar _appBar;
        private DennokoStatus _status;
        private Button _statusAction;
        private string _statusAsset;
        private int _statusSerial;   // 後から来たメッセージを、前のメッセージの時間切れで消さないための番号
        private DennokoVersionChecker.Result _versionResult =
            new DennokoVersionChecker.Result { State = DennokoVersionChecker.State.Checking, LocalVersion = "0.0.0" };

        [MenuItem("dennokoworks/MaskMaker V4")]
        public static void Open()
        {
            var w = GetWindow<MaskMakerWindow>();
            w.titleContent = new GUIContent(ToolName);
            w.minSize = new Vector2(320, 420);
        }

        /// <summary>右クリック・自動再開からウィンドウを開く。bringToFront = true の場合、既に開いているインスタンスがあれば最前面に表示する。</summary>
        public static void EnsureOpen(bool bringToFront = false)
        {
            if (HasOpenInstances<MaskMakerWindow>())
            {
                if (bringToFront)
                {
                    var existing = GetWindow<MaskMakerWindow>(false, ToolName, false);
                    existing.Show();
                    existing.Focus();
                }
                return;
            }
            var w = GetWindow<MaskMakerWindow>(false, ToolName, bringToFront);
            w.minSize = new Vector2(320, 420);
            if (bringToFront)
            {
                w.Show();
                w.Focus();
            }
        }

        private void OnEnable()
        {
            Notifier.Posted += OnMessage;
            DennokoSceneSession.Ended += OnSessionEnded;
        }

        private void OnDisable()
        {
            Notifier.Posted -= OnMessage;
            DennokoSceneSession.Ended -= OnSessionEnded;
        }

        private void OnDestroy()
        {
            SessionController.End(reason: DennokoSessionEndReason.WindowClosed);
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
            // 表示言語が変わったら中身ごと作り直す
            LanguageUi.Build(root, content => Build(content, tree));
        }

        private void Build(VisualElement root, VisualTreeAsset tree)
        {
            tree.CloneTree(root);

            new TargetCard(root);
            new UVCard(root);
            new OutputCard(root);
            BindAppBar(root);
            BindSessionBanner(root);
            BindOutputDock(root);
            BindFooter(root);

            _status = new DennokoStatus(root.Q<Label>("status-label"), "Ready", 4000);
            _status.Ready();
            root.Q<Label>("status-label").RegisterCallback<ClickEvent>(_ => PingStatus());
            _statusAction = root.Q<Button>("status-action");
            Bind.Show(_statusAction, false);

            UiHub.Watch(root, () =>
            {
                bool working = UiRules.Working;
                Bind.Show(root.Q("session-banner"), working);
                Bind.Show(root.Q("uv-card"), working);
                Bind.Show(root.Q("output-card"), working);
            });
        }

        // ── 標準ヘッダー（トップバー） ──

        private void BindAppBar(VisualElement root)
        {
            _appBar = new DennokoAppBar(root, ToolName, english: !L10n.Japanese)
            {
                AddToolMenuItems = menu =>
                {
                    menu.AddItem(new GUIContent(L10n.T("menu.preferences")), false, () => OpenSettings(true));
                    menu.AddItem(new GUIContent(L10n.T("menu.projectSettings")), false, () => OpenSettings(false));
                    menu.AddSeparator(string.Empty);
                    menu.AddItem(new GUIContent(L10n.T("menu.checkUpdate")), false, CheckUpdate);
                    menu.AddItem(new GUIContent(L10n.T("menu.openRelease")), false, OpenRelease);
                },
            };
            // 言語は SettingsService に持つ。切り替えると LanguageUi が中身ごと作り直す
            _appBar.LanguageChanged += LanguageUi.SetEnglish;

            // ↻ / ? / ⋮ のツールチップは表示言語で出し直す（DennokoAppBar の既定は日本語）
            var reload = root.Q<Button>("version-reload-button");
            reload.tooltip = L10n.T("tooltip.reload");
            reload.clicked += CheckUpdate;
            var manual = root.Q<Button>("appbar-manual");
            manual.tooltip = L10n.T("tooltip.manual");
            manual.clicked += UiAssets.OpenManual;
            root.Q<Button>("appbar-settings").tooltip = L10n.T("tooltip.menu");

            _versionLabel = root.Q<Label>("version-label");
            _versionLabel.RegisterCallback<ClickEvent>(_ =>
            {
                if (_versionResult.State == DennokoVersionChecker.State.UpdateAvailable) OpenRelease();
            });
            LoadVersionResultFromSessionState();
            MaskMakerVersion.StartCheckBackgroundTask();
        }

        private void CheckUpdate()
        {
            MaskMakerVersion.ForceRecheck();
            LoadVersionResultFromSessionState();
        }

        private void OpenRelease()
        {
            Application.OpenURL(string.IsNullOrEmpty(_versionResult.Url) ? ReleaseUrl : _versionResult.Url);
        }

        private static void OpenSettings(bool user)
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
            ApplyVersionLabel();
        }

        private void ApplyVersionLabel()
        {
            if (_versionLabel == null) return;
            var r = _versionResult;
            string text = "v" + r.LocalVersion;
            bool update = r.State == DennokoVersionChecker.State.UpdateAvailable;
            bool error = r.State == DennokoVersionChecker.State.Error;
            bool checking = r.State == DennokoVersionChecker.State.Checking;
            if (update) text += "  " + L10n.F("status.version.update", r.LatestVersion);
            else if (error) text += "  " + L10n.T("status.version.error");
            else if (checking) text += "  " + L10n.T("status.version.checking");
            _versionLabel.text = text;
            _versionLabel.EnableInClassList("dennoko-version-label--update", update);
            _versionLabel.EnableInClassList("dennoko-version-label--error", error);
            _versionLabel.EnableInClassList("dennoko-version-label--checking", checking);
            _appBar?.SetVersionClickable(update, update ? L10n.T("tooltip.openRelease") : null);
        }

        // ── B7 作業中バナー ──

        private static void BindSessionBanner(VisualElement root)
        {
            root.Q<Label>("session-state").text = L10n.T("session.state");
            root.Q<Label>("session-hint").text = L10n.T("session.hint");
            var end = root.Q<Button>("session-end");
            end.text = L10n.T("button.end");
            end.clicked += () => SessionController.End();

            var target = root.Q<Label>("session-target");
            target.tooltip = L10n.T("tooltip.frameTarget");
            target.RegisterCallback<ClickEvent>(_ => DennokoSceneSession.FrameTarget());
            UiHub.Watch(target, () => target.text = SessionController.Current?.Target != null ? SessionController.Current.Target.name : string.Empty);
        }

        // ── 出力ドック（要約チップ。出力カードが見えている間は隠す。skill cards.md B5） ──

        private static void BindOutputDock(VisualElement root)
        {
            var dock = root.Q("output-dock");
            var chips = root.Q("output-dock-chips");
            var card = root.Q("output-card");
            var scroll = root.Q<ScrollView>("main-scroll");
            var footer = root.Q("footer");

            void ScrollToCard() => scroll.ScrollTo(card);

            void Refresh()
            {
                var s = SessionController.Current;
                bool cardVisible = s != null && card.resolvedStyle.display == DisplayStyle.Flex
                                   && card.worldBound.Overlaps(scroll.contentViewport.worldBound);
                bool show = s != null && !cardVisible;
                Bind.Show(dock, show);
                // 出力ドックの直後のフッターは上罫線を重ねない
                footer.EnableInClassList("dennoko-footer--joined", show);
                chips.Clear();
                if (!show) return;
                string tip = L10n.T("tooltip.toOutput");
                chips.Add(DennokoBlockUI.Chip(DennokoIcons.Folder, OutputFolderPreview(s), tip, ScrollToCard));
                chips.Add(DennokoBlockUI.Chip(null, UiRules.EffectiveSize(s).ToString(), tip, ScrollToCard));
                foreach (var (text, _) in OutputCard.DetailSummary(s)) chips.Add(DennokoBlockUI.Chip(null, text, tip, ScrollToCard));
            }

            UiHub.Watch(dock, Refresh);
            scroll.verticalScroller.valueChanged += _ => Refresh();
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            card.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        private static string OutputFolderPreview(WorkSession s)
        {
            string baseDir = OutputPaths.BaseFolderFor(s);
            string baseName = Path.GetFileName(baseDir);
            string resolved = OutputPaths.FolderFor(s);
            return resolved == baseDir ? baseName : baseName + "/" + OutputPaths.MaskSubfolderName;
        }

        // ── フッター（主ボタン 1 つ。Idle = 作業開始 / Working = 出力） ──

        private static void BindFooter(VisualElement root)
        {
            var primary = root.Q<Button>("primary-button");
            primary.clicked += () =>
            {
                if (SessionController.IsWorking) SessionCommands.Export(MessageOrigin.Window);
                else SessionController.Start(TargetCard.PendingTarget);
            };
            UiHub.Watch(primary, () =>
            {
                bool working = UiRules.Working;
                bool canStart = TargetCard.PendingTarget != null;
                primary.text = working ? L10n.T("button.export") : L10n.T("button.start");
                primary.SetEnabled(working || canStart);
                // 押せない理由はツールチップに出す
                primary.tooltip = working || canStart ? string.Empty : L10n.T("tooltip.chooseTarget");
            });
        }

        // ── ステータスバー（ウィンドウから実行した結果。シーンから実行した結果はトースト） ──

        private void OnMessage(UserMessage m)
        {
            if (m.Origin == MessageOrigin.Scene && SceneView.lastActiveSceneView != null && SessionController.IsWorking) return;
            if (_status == null) return;
            int serial = ++_statusSerial;
            // 操作ボタンが付いているときは少し長く出す
            _status.AutoResetMs = m.Action != null ? 6000 : 4000;
            _status.Set(m.Text, ToStatusType(m.Level));
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

            // DennokoStatus は Info を自動では戻さないので、ここで Ready に戻す（成功・警告・エラーは DennokoStatus が戻す）。
            // 操作ボタンは、文が消えるのと一緒に隠す
            _statusAction.schedule.Execute(() =>
            {
                if (serial != _statusSerial) return;
                if (m.Level == MessageLevel.Info) ClearStatus();
                else Bind.Show(_statusAction, false);
            }).StartingIn(_status.AutoResetMs);
        }

        /// <summary>共通の編集セッションが終わった理由（リロード・Play モードは自動で再開するので出さない。「終了」ボタンは押した本人が分かる）。</summary>
        private void OnSessionEnded(DennokoSessionEndReason reason)
        {
            if (_status == null) return;
            string key = reason switch
            {
                DennokoSessionEndReason.Escape => "session.end.escape",
                DennokoSessionEndReason.SceneChanged => "session.end.sceneChanged",
                DennokoSessionEndReason.TargetLost => "session.end.targetLost",
                DennokoSessionEndReason.Replaced => "session.end.replaced",
                _ => null,
            };
            if (key == null) return;
            _statusSerial++;
            _status.AutoResetMs = 4000;
            _status.Set(L10n.T(key), DennokoStatusType.Warning);
            Bind.Show(_statusAction, false);
            _statusAsset = null;
        }

        private static DennokoStatusType ToStatusType(MessageLevel level) => level switch
        {
            MessageLevel.Success => DennokoStatusType.Success,
            MessageLevel.Warning => DennokoStatusType.Warning,
            MessageLevel.Error => DennokoStatusType.Error,
            _ => DennokoStatusType.Info,
        };

        private void ClearStatus()
        {
            _status.Ready();
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
