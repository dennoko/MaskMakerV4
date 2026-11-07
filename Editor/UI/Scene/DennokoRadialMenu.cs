using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// SceneView 上のパイメニュー (ラジアルメニュー) のテンプレート。MaskMakerV4 の SceneHud から、
    /// ツール固有の部分 (項目・ショートカット・状態) を抜いたもの。このファイルはそのままコピーして使う。
    ///
    /// ツール側で書くのは次の 2 つだけ (手順と例: blocks/radial-menu.md)。
    ///   1. 項目を返す関数 (8 個。0 = → から反時計回り)
    ///   2. [ClutchShortcut] の static メソッド: Begin で OpenAtPointer()、End で Release()
    ///
    /// 動き:
    ///   - キーを押している間、カーソル直下に出る。向いた方向の項目を、キーを離したときに実行する
    ///   - 中心付近で離すと何もしない (キャンセル)
    ///   - 押してすぐ離した (タップ) ときは開いたままにして、クリックで選べる。Esc / 右クリックで閉じる
    ///   - 動かさずに待つと、各項目の下にキー表記が出る
    ///   - SceneView の上だけで出す (他のウィンドウの上でキーを押しても出さない)
    ///
    /// 見た目は DennokoSceneUI.uss の .dennoko-radial-* (寸法 180px / 36px / 半径 64px を下の定数と揃える)。
    /// </summary>
    internal sealed class DennokoRadialMenu
    {
        public const int SliceCount = 8;

        /// <summary>1 項目。Run が null の項目は薄く表示して選べない (数を 8 に保つと方向を手が覚えられる)。</summary>
        public struct Item
        {
            public string Name;      // 中心に出す名前 (8 字まで)
            public string Icon;      // Unity 組み込みアイコン名 ("名前|代わり" 可)。null なら Fallback の文字
            public string Fallback;  // アイコンが無いときの短い文字
            public string Key;       // 待つと下に出すキー表記
            public Action Run;       // 実行する処理 (メニューを閉じてから呼ぶ)
            public bool Active;      // 選択中なら selection 色の枠
        }

        // 寸法は DennokoSceneUI.uss と揃える
        private const float MenuHalf = 90f;
        private const float SliceHalf = 18f;
        private const float SliceRing = 64f;
        private const float DeadZone = 18f;          // これより中心に近い間は何も選ばない
        private const double KeyHintDelay = 0.6;     // 動かさずに待つとキー表記を出すまでの秒数
        private const double TapThreshold = 0.25;    // これより短く押して離したら開いたままにする

        /// <summary>開いてよいか (例: 作業中だけ)。false なら Open は何もしない。</summary>
        public Func<bool> CanOpen = () => true;

        /// <summary>項目を実行した後に呼ばれる (表示の更新など)。</summary>
        public event Action Executed;

        public bool IsOpen => _menu != null;

        private readonly string _rootName;
        private readonly Func<Item[]> _buildItems;
        private readonly Action<VisualElement> _setupRoot;

        private readonly Dictionary<SceneView, VisualElement> _huds = new Dictionary<SceneView, VisualElement>();
        private double _nextPoll;
        private SceneView _pointerView;
        private Vector2 _pointerGui;

        private VisualElement _menu;
        private Label _center;
        private readonly List<Button> _slices = new List<Button>();
        private Item[] _items;
        private SceneView _menuView;
        private Vector2 _menuCenter;
        private int _hot = -1;
        private double _lastChange;
        private double _openedAt;
        private bool _sticky;

        /// <param name="rootName">SceneView に重ねる要素の名前。他のツールと重ならない名前にする</param>
        /// <param name="buildItems">開くたびに呼ばれ、8 個の項目を返す (モードで中身を差し替えてよい)</param>
        /// <param name="setupRoot">テーマの USS を当てる処理 (DennokoSceneUI.SetupRoot など)</param>
        public DennokoRadialMenu(string rootName, Func<Item[]> buildItems, Action<VisualElement> setupRoot)
        {
            _rootName = rootName;
            _buildItems = buildItems;
            _setupRoot = setupRoot;
            EditorApplication.update += Poll;
            SceneView.duringSceneGui += OnSceneGui;
            AssemblyReloadEvents.beforeAssemblyReload += () => Close(false);
        }

        // ─── 開く / 離す / 閉じる ─────────────────────────────────────

        /// <summary>
        /// [ClutchShortcut] の Begin から呼ぶ。カーソル直下に開く。
        /// ショートカットは「キー入力を受けているウィンドウ」で決まるので、カーソルの下が
        /// SceneView でなければ開かない (他のウィンドウの上で押すと、SceneView の古い位置に出てしまうため)。
        /// </summary>
        public void OpenAtPointer()
        {
            if (!(EditorWindow.mouseOverWindow is SceneView view) || view != _pointerView) return;
            Open(view, ToRoot(view, _pointerGui), false);
        }

        /// <summary>ボタンなどから開く。クリックで選ぶ固定表示で、位置は SceneView の中央。</summary>
        public void OpenSticky(SceneView view = null)
        {
            view = view != null ? view : SceneView.lastActiveSceneView;
            if (view == null) return;
            EnsureHud(view);
            var layout = view.rootVisualElement.layout;
            Open(view, new Vector2(layout.width * 0.5f, layout.height * 0.5f), true);
            view.Focus();
        }

        /// <summary>
        /// [ClutchShortcut] の End から呼ぶ。向いている項目を実行して閉じる。
        /// 押してすぐ離した (何も向いていない) ときは開いたままにして、クリックで選べるようにする。
        /// </summary>
        public void Release()
        {
            if (_menu == null || _sticky) return;
            if (_hot < 0 && EditorApplication.timeSinceStartup - _openedAt < TapThreshold)
            {
                _sticky = true;
                return;
            }
            Close(true);
        }

        public void Close(bool apply)
        {
            if (_menu == null) return;
            int hot = _hot;
            _menu.RemoveFromHierarchy();
            _menu = null;
            _menuView = null;
            _sticky = false;
            // 閉じてから実行する (実行した処理が UI を作り直してもメニューの後始末と競合しない)
            if (apply && hot >= 0 && _items[hot].Run != null)
            {
                _items[hot].Run();
                Executed?.Invoke();
            }
        }

        private void Open(SceneView view, Vector2 center, bool sticky)
        {
            if (_menu != null || !CanOpen()) return;
            if (!_huds.TryGetValue(view, out var hud) || hud.panel == null) return;

            _items = _buildItems() ?? new Item[SliceCount];
            if (_items.Length < SliceCount) Array.Resize(ref _items, SliceCount);
            _menuView = view;
            _menuCenter = center;
            _hot = -1;
            _sticky = sticky;
            _openedAt = _lastChange = EditorApplication.timeSinceStartup;

            _menu = new VisualElement { pickingMode = PickingMode.Ignore };
            _menu.AddToClassList("dennoko-radial-menu");
            _menu.style.left = center.x - MenuHalf;
            _menu.style.top = center.y - MenuHalf;
            // 背景の円は枠なしの容れ物の子に置く (容れ物に枠を付けると子の基準位置が枠の分ずれる)
            var bg = new VisualElement { pickingMode = PickingMode.Ignore };
            bg.AddToClassList("dennoko-radial-bg");
            _menu.Add(bg);
            _center = new Label { pickingMode = PickingMode.Ignore };
            _center.AddToClassList("dennoko-radial-center");
            _menu.Add(_center);

            _slices.Clear();
            for (int i = 0; i < SliceCount; i++)
            {
                var item = _items[i];
                // Button にするのは選択中の枠 (.dennoko-button-active) をそのまま使うため。クリックは受けない
                var slice = new Button { pickingMode = PickingMode.Ignore, text = string.Empty };
                slice.AddToClassList("dennoko-radial-slice");
                float ang = i * 45f * Mathf.Deg2Rad;
                slice.style.left = MenuHalf + Mathf.Cos(ang) * SliceRing - SliceHalf;
                slice.style.top = MenuHalf - Mathf.Sin(ang) * SliceRing - SliceHalf;
                if (item.Run == null)
                {
                    slice.style.opacity = 0.25f;
                }
                else
                {
                    ApplyIcon(slice, item.Icon, item.Fallback);
                    slice.EnableInClassList("dennoko-button-active", item.Active);
                }
                var key = new Label(item.Key ?? string.Empty) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("dennoko-radial-key");
                key.style.display = DisplayStyle.None;
                slice.Add(key);
                _menu.Add(slice);
                _slices.Add(slice);
            }
            hud.Add(_menu);
            hud.schedule.Execute(ShowKeysIfStill).Every(100).Until(() => _menu == null);
        }

        // ─── ポインタ ─────────────────────────────────────────────────

        private void OnSceneGui(SceneView view)
        {
            var e = Event.current;
            _pointerView = view;
            _pointerGui = e.mousePosition;
            if (_menu == null || view != _menuView) return;

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                UpdateHot(e.mousePosition);
                view.Repaint();
            }

            if (!_sticky) return;
            // 固定表示: 左クリックで決定、右クリック / Esc で閉じる
            if (e.type == EventType.MouseDown)
            {
                UpdateHot(e.mousePosition);
                Close(e.button == 0);
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close(false);
                e.Use();
            }
        }

        private void UpdateHot(Vector2 gui)
        {
            var d = ToRoot(_menuView, gui) - _menuCenter;
            int hot = -1;
            if (d.magnitude >= DeadZone)
            {
                // GUI 座標は下向きが +y なので反転して、→ = 0° から反時計回りの角度にする
                float ang = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
                if (ang < 0) ang += 360f;
                hot = Mathf.RoundToInt(ang / 45f) % SliceCount;
                if (_items[hot].Run == null) hot = -1;
            }
            if (hot == _hot) return;
            _hot = hot;
            _lastChange = EditorApplication.timeSinceStartup;
            for (int i = 0; i < _slices.Count; i++) _slices[i].EnableInClassList("dennoko-radial-hot", i == hot);
            _center.text = hot >= 0 ? _items[hot].Name : string.Empty;   // 中心には向いている項目の名前だけ
        }

        // 迷っている (動かさずに待っている) ときだけキー表記を出す
        private void ShowKeysIfStill()
        {
            if (_menu == null || EditorApplication.timeSinceStartup - _lastChange < KeyHintDelay) return;
            foreach (var slice in _slices)
                foreach (var child in slice.Children())
                    if (child is Label) child.style.display = DisplayStyle.Flex;
        }

        // ─── SceneView に重ねる透明な全面ルート ─────────────────────────

        // SceneView は後から開かれたり、ドックし直しでパネルが作り直されたりするので、定期的に付け直す
        private void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.5;
            foreach (SceneView view in SceneView.sceneViews) EnsureHud(view);
            var dead = new List<SceneView>();
            foreach (var kv in _huds) if (kv.Key == null) dead.Add(kv.Key);
            foreach (var d in dead) _huds.Remove(d);
        }

        private void EnsureHud(SceneView view)
        {
            if (view == null || (_huds.TryGetValue(view, out var existing) && existing.panel != null)) return;
            var parent = view.rootVisualElement;
            parent.Q(_rootName)?.RemoveFromHierarchy();   // ドメインリロード前に付けた物が残っていれば外す
            // 全面ルートとメニューはすべて picking を無視する (忘れるとシーンのクリックやカメラ操作を吸う)
            var root = new VisualElement { name = _rootName, pickingMode = PickingMode.Ignore };
            _setupRoot?.Invoke(root);
            root.AddToClassList("dennoko-hud");
            parent.Add(root);
            _huds[view] = root;
        }

        /// <summary>
        /// SceneView の GUI 座標 (Event.mousePosition、シーン描画領域の左上が原点) → rootVisualElement の座標。
        /// rootVisualElement は上部のシーンツールバーを含むので、その高さの分だけ下へずらす。
        /// </summary>
        private static Vector2 ToRoot(SceneView view, Vector2 gui)
        {
            float camHeight = view.camera != null ? view.camera.pixelHeight / EditorGUIUtility.pixelsPerPoint : view.position.height;
            float offset = Mathf.Max(0f, view.rootVisualElement.layout.height - camHeight);
            return gui + new Vector2(0f, offset);
        }

        // ─── アイコン (他のファイルに依存しないよう内蔵) ───────────────

        private static readonly Dictionary<string, Texture2D> IconCache = new Dictionary<string, Texture2D>();

        private static void ApplyIcon(Button button, string iconNames, string fallback)
        {
            Texture2D tex = null;
            if (!string.IsNullOrEmpty(iconNames) && !(IconCache.TryGetValue(iconNames, out tex) && tex != null))
            {
                foreach (var n in iconNames.Split('|'))
                {
                    tex = EditorGUIUtility.FindTexture(n);
                    if (tex != null) break;
                }
                IconCache[iconNames] = tex;
            }
            if (tex == null)
            {
                button.text = fallback ?? string.Empty;
                return;
            }
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("dennoko-icon");
            icon.style.backgroundImage = tex;
            button.Add(icon);
        }
    }
}
