using System;
using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>UXML / USS を GUID で読む（スキル絶対規則 5）。GUID は各 .meta と一致させる。</summary>
    internal static class UiAssets
    {
        public const string ThemeUssGuid = "b3e0a4c19f2d4c7e8a6b5d0f1c2e3a91";
        public const string StylesUssGuid = "c4f1b5d2a0e34d8f9b7c6e1a2d3f4b52";
        public const string WindowUxmlGuid = "d5a2c6e3b1f44e9a8c7d0f2b3e4a5c63";
        public const string CapsuleUxmlGuid = "e6b3d7f4c2a54fab9d8e1a3c4f5b6d74";
        public const string LayoutUxmlGuid = "8cb76dc39e39429aa930b43082667f4c";

        /// <summary>dennoko-root と保険の背景色、テーマ + 固有 USS、フォントを適用する。</summary>
        public static void SetupRoot(VisualElement root, bool opaque = true)
        {
            root.AddToClassList("dennoko-root");
            if (opaque) root.style.backgroundColor = (Color)new Color32(0x12, 0x12, 0x12, 0xFF);
            var theme = Load<StyleSheet>(ThemeUssGuid);
            if (theme != null) root.styleSheets.Add(theme);
            var styles = Load<StyleSheet>(StylesUssGuid);
            if (styles != null) root.styleSheets.Add(styles);
            DennokoUIFont.Apply(root);
        }

        public static VisualTreeAsset LoadTree(string guid) => Load<VisualTreeAsset>(guid);

        private static T Load<T>(string guid) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[MaskMaker V4] UI asset not found for GUID: " + guid);
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }

    /// <summary>
    /// UI の再描画の合図を 1 つにまとめる（Design state_and_lifecycle §2.1 の Push）。
    /// 設定・作業内容・作業の開始終了・ツール切替のどれが変わっても、全ビューが読み直す。
    /// 同じフレームの連続した変更は 1 回にまとめる。
    /// </summary>
    [InitializeOnLoad]
    internal static class UiHub
    {
        public static event Action Changed;
        private static bool _pending;

        static UiHub()
        {
            SettingsService.Changed += _ => Raise();
            EditState.Changed += _ => Raise();
            SessionController.Changed += Raise;
            ToolManager.activeToolChanged += Raise;
        }

        public static void Raise()
        {
            if (_pending) return;
            _pending = true;
            EditorApplication.delayCall += Fire;
        }

        private static void Fire()
        {
            _pending = false;
            Changed?.Invoke();
        }

        /// <summary>owner がパネルにある間だけ refresh を購読し、付けた直後に 1 回呼ぶ。</summary>
        public static void Watch(VisualElement owner, Action refresh)
        {
            void Handler() => refresh();
            owner.RegisterCallback<AttachToPanelEvent>(_ => { Changed -= Handler; Changed += Handler; refresh(); });
            owner.RegisterCallback<DetachFromPanelEvent>(_ => Changed -= Handler);
            if (owner.panel != null) { Changed -= Handler; Changed += Handler; }
            refresh();
        }
    }

    /// <summary>
    /// 設定キー ⇄ UI 要素（Design state_and_lifecycle §3.1）。
    /// 表示は常に解決済みの値から SetValueWithoutNotify で作り、入力は SettingsService.Set に流すだけ。
    /// </summary>
    internal static class Bind
    {
        public static void Toggle(Toggle toggle, Setting<bool> s, VisualElement dot = null)
        {
            toggle.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue));
            UiHub.Watch(toggle, () =>
            {
                toggle.SetValueWithoutNotify(SettingsService.Get(s));
                Dot(dot, s);
            });
            HookDot(dot, s);
        }

        public static void Slider(Slider slider, Setting<float> s)
        {
            slider.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue));
            UiHub.Watch(slider, () => slider.SetValueWithoutNotify(SettingsService.Get(s)));
        }

        public static void SliderInt(SliderInt slider, Setting<int> s, Func<int> effective, VisualElement dot = null)
        {
            slider.RegisterValueChangedCallback(e => SettingsService.Set(s, e.newValue));
            UiHub.Watch(slider, () =>
            {
                slider.SetValueWithoutNotify(effective());
                Dot(dot, s);
            });
            HookDot(dot, s);
        }

        /// <summary>空にすると既定（自動）に戻るテキスト。</summary>
        public static void Text(TextField field, Setting<string> s, Func<string> effective)
        {
            field.isDelayed = true;
            field.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue?.Trim() ?? string.Empty;
                if (v.Length == 0 || v == effective()) SettingsService.ResetToLower(s);
                else SettingsService.Set(s, v);
            });
            UiHub.Watch(field, () => field.SetValueWithoutNotify(effective()));
        }

        /// <summary>複数のボタンから 1 つを選ぶ（選択中に .dennoko-button-active）。</summary>
        public static void Choice<T>(IReadOnlyList<(Button button, T value)> items, Setting<T> s, Func<T> effective)
        {
            foreach (var (button, value) in items)
            {
                var v = value;
                button.clicked += () => SettingsService.Set(s, v);
            }
            if (items.Count == 0) return;
            UiHub.Watch(items[0].button, () =>
            {
                var current = effective();
                foreach (var (button, value) in items) Active(button, EqualityComparer<T>.Default.Equals(value, current));
            });
        }

        /// <summary>ON/OFF のボタン（ON で青枠）。</summary>
        public static void ToggleButton(Button button, Setting<bool> s, Action onClick = null)
        {
            button.clicked += onClick ?? (() => SettingsService.Set(s, !SettingsService.Get(s)));
            UiHub.Watch(button, () => Active(button, SettingsService.Get(s)));
        }

        public static void Active(VisualElement e, bool on) => e.EnableInClassList("dennoko-button-active", on);

        public static void Show(VisualElement e, bool visible)
        {
            if (e != null) e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void Dot(VisualElement dot, Setting s)
        {
            if (dot == null) return;
            bool over = s switch
            {
                Setting<bool> b => SettingsService.IsOverridden(b),
                Setting<int> i => SettingsService.IsOverridden(i),
                Setting<float> f => SettingsService.IsOverridden(f),
                Setting<string> t => SettingsService.IsOverridden(t),
                _ => false,
            };
            Show(dot, over);
        }

        private static void HookDot(VisualElement dot, Setting s)
        {
            if (dot == null) return;
            dot.tooltip = L10n.T("tooltip.overrideDot");
            dot.RegisterCallback<ClickEvent>(_ => SettingsService.ResetToLower(s));
        }
    }

    /// <summary>表示・有効/無効の判定（全ビュー共通の純粋関数。Design state_and_lifecycle §3.2）。</summary>
    internal static class UiRules
    {
        public static bool Working => SessionController.Current != null;

        public static bool PaintMode => SettingsService.Get(SettingKeys.IsPaintMode);

        public static bool ShowSubmesh(WorkSession s) => s != null && s.Mesh.SubmeshCount > 1;

        public static List<int> UVChannels(WorkSession s)
        {
            var list = new List<int>();
            if (s == null) return list;
            for (int c = 0; c < 8; c++) if (s.Mesh.HasUVs(c)) list.Add(c);
            return list;
        }

        public static bool SizeLocked(WorkSession s) => s != null && s.OutputSizeLockedByBase;

        public static int EffectiveSize(WorkSession s) => s == null ? SettingsService.Get(SettingKeys.DefaultTextureSize) : s.OutputSize().x;

        public static int EffectiveMargin(WorkSession s) => s == null ? 0 : s.EffectiveMargin(s.OutputSize());

        public static string GranularityLabel(SelectionGranularity g)
            => g == SelectionGranularity.UVIsland ? L10n.T("term.island")
             : g == SelectionGranularity.ConnectedMesh ? L10n.T("term.part")
             : L10n.T("term.face");

        public static string ToolLabel(PaintSubMode t)
            => t == PaintSubMode.Brush ? L10n.T("term.brush")
             : t == PaintSubMode.Rect ? L10n.T("term.rect")
             : t == PaintSubMode.Lasso ? L10n.T("term.lasso")
             : L10n.T("term.eraser");

        /// <summary>カプセルの「Full のグループ数 / グループ数」。</summary>
        public static string CountText(WorkSession s)
        {
            if (s == null) return string.Empty;
            s.SelectionCount(out int full, out int total);
            return full + "/" + total;
        }
    }

    /// <summary>Unity 内蔵アイコン（d_ 版のみ。UI 設計 §2.6.6）。</summary>
    internal static class Icons
    {
        public const string Select = "d_scenepicking_pickable";
        public const string Paint = "d_Grid.PaintTool|d_TerrainInspector.TerrainToolSplat";
        public const string Rect = "d_RectTool";
        public const string Lasso = "d_EditCollider";
        public const string Eraser = "d_Grid.EraserTool";
        public const string Symmetry = "d_align_horizontally_center";
        public const string XRay = "d_scenevis_visible";
        public const string Isolate = "d_scenevis_hidden";
        public const string Pause = "d_PauseButton";
        public const string Undo = "d_back|d_Animation.PrevKey";
        public const string Redo = "d_forward|d_Animation.NextKey";
        public const string Clear = "d_TreeEditor.Trash";
        public const string Export = "d_SaveAs";
        public const string Material = "d_Material Icon";
        public const string Folder = "d_Folder Icon";
        public const string Texture = "d_Texture Icon";
        public const string Layout = "d_Camera|d_RawImage Icon";
        public const string ResetView = "d_ViewToolZoom|d_Search Icon";
        public const string Import = "d_Import";
        public const string SkinnedMesh = "d_SkinnedMeshRenderer Icon";
        public const string MeshRenderer = "d_MeshRenderer Icon";
        public const string Settings = "d_SettingsIcon|d__Popup";
        public const string Refresh = "d_Refresh";
        public const string Plus = "d_Toolbar Plus";
        public const string Minus = "d_Toolbar Minus";
        public const string Info = "d_console.infoicon.sml";
        public const string Warning = "d_console.warnicon.sml";
        public const string Error = "d_console.erroricon.sml";

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>"名前|代わりの名前" の順に探す。見つからなければ null。</summary>
        public static Texture2D Get(string names)
        {
            if (Cache.TryGetValue(names, out var cached) && cached != null) return cached;
            Texture2D tex = null;
            foreach (var n in names.Split('|'))
            {
                tex = EditorGUIUtility.FindTexture(n);
                if (tex != null) break;
            }
            Cache[names] = tex;
            return tex;
        }

        /// <summary>ボタンにアイコンを付ける。アイコンが無ければ短い語を出す。</summary>
        public static void Apply(Button button, string names, string fallbackText, string tooltip)
        {
            var tex = Get(names);
            if (tex != null)
            {
                button.style.backgroundImage = tex;
                button.text = string.Empty;
            }
            else
            {
                button.style.backgroundImage = StyleKeyword.None;
                button.text = fallbackText;
            }
            button.tooltip = tooltip;
        }

        public static Texture2D ForRenderer(Renderer r) => Get(r is SkinnedMeshRenderer ? SkinnedMesh : MeshRenderer);

        public static Texture2D ForLevel(MessageLevel level)
            => level == MessageLevel.Error ? Get(Error) : level == MessageLevel.Warning ? Get(Warning) : Get(Info);

        /// <summary>見つからないアイコンを Console に列挙する（Preferences のボタンから）。</summary>
        public static void ReportMissing()
        {
            var fields = typeof(Icons).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            int missing = 0;
            foreach (var f in fields)
            {
                if (f.FieldType != typeof(string)) continue;
                var names = (string)f.GetValue(null);
                Cache.Remove(names);
                if (Get(names) != null) continue;
                missing++;
                Debug.LogWarning("[MaskMaker V4] icon not found: " + f.Name + " (" + names + ")");
            }
            Debug.Log("[MaskMaker V4] icon check done. missing: " + missing);
        }
    }

    /// <summary>選択中のオブジェクトから作業対象を決める（UI 設計 §3「対象の決め方」）。</summary>
    internal static class TargetResolver
    {
        public static List<Renderer> Candidates(GameObject go)
        {
            var list = new List<Renderer>();
            if (go == null) return list;
            var own = go.GetComponent<Renderer>();
            if (IsUsable(own))
            {
                list.Add(own);
                return list;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (IsUsable(r)) list.Add(r);
            return list;
        }

        /// <summary>候補の既定（頂点数が最大のもの）。</summary>
        public static Renderer Best(List<Renderer> candidates)
        {
            Renderer best = null;
            int bestCount = -1;
            foreach (var r in candidates)
            {
                var m = TargetKey.SourceMesh(r);
                int n = m != null ? m.vertexCount : 0;
                if (n > bestCount) { best = r; bestCount = n; }
            }
            return best;
        }

        public static Renderer FromSelection()
        {
            var c = Candidates(Selection.activeGameObject);
            return c.Count == 0 ? null : Best(c);
        }

        /// <summary>同じアバター（Animator を持つ最上位の祖先）配下の Renderer。</summary>
        public static List<Renderer> Siblings(Renderer r)
        {
            var root = WorkMesh.FindAvatarRoot(r.transform);
            return Candidates(root.gameObject);
        }

        public static bool IsUsable(Renderer r)
        {
            if (r == null) return false;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) return false;
            var m = TargetKey.SourceMesh(r);
            return m != null && m.vertexCount > 0;
        }
    }
}
