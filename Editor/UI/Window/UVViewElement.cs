using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// UV ビュー（UI 設計 §4.3）。シーンと同じツール・同じモードで操作できる。
    /// 表示は UVViewRenderer が描いた RT。座標は要素ローカル（pt、y 下向き）で、view = uv * xy + zw。
    /// </summary>
    internal sealed class UVViewElement : VisualElement
    {
        private readonly Image _image;
        private readonly VisualElement _cursor;
        private readonly Label _dropLabel;
        private readonly SelectGesture _select = new SelectGesture();
        private readonly List<int> _tris = new List<int>();
        private readonly List<int> _groups = new List<int>();

        private float _zoom = 1f;
        private Vector2 _pan;
        private Vector2 _pointer;
        private bool _pointerInside;
        private bool _panning;
        private bool _resizing;
        private float _resizeStartX;
        private int _resizeStartSize;

        // ペイント
        private bool _painting;
        private bool _paintValue;
        private PaintSubMode _tool;
        private Vector2 _prev;
        private readonly List<Vector2> _shape = new List<Vector2>();

        private (int, int, int, Vector4, Vector2Int, int, bool) _renderKey;

        public UVViewElement()
        {
            AddToClassList("mm-uv-view");
            focusable = true;
            _image = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            _image.AddToClassList("mm-uv-image");
            Add(_image);
            _cursor = new VisualElement { pickingMode = PickingMode.Ignore };
            _cursor.AddToClassList("mm-uv-image");
            _cursor.generateVisualContent += DrawCursor;
            Add(_cursor);
            _dropLabel = new Label(L10n.T("uv.drop")) { pickingMode = PickingMode.Ignore };
            _dropLabel.AddToClassList("mm-drop-label");
            Bind.Show(_dropLabel, false);
            Add(_dropLabel);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(_ => { _pointerInside = false; ClearHover(); });
            RegisterCallback<PointerEnterEvent>(_ => _pointerInside = true);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(_ => SetDropVisual(false));
            schedule.Execute(RenderIfNeeded).Every(33);
        }

        // ── 座標 ──

        private Vector4 UVToView
        {
            get
            {
                var size = contentRect.size;
                float side = Mathf.Min(size.x, size.y) * _zoom;
                Vector2 origin = size * 0.5f - new Vector2(side, side) * 0.5f + _pan;
                return new Vector4(side, -side, origin.x, origin.y + side);
            }
        }

        private Vector2 ViewToUV(Vector2 p)
        {
            var m = UVToView;
            return new Vector2((p.x - m.z) / m.x, (p.y - m.w) / m.y);
        }

        // ── 描画 ──

        private void RenderIfNeeded()
        {
            var s = SessionController.Current;
            if (s == null)
            {
                _image.image = null;
                return;
            }
            var size = new Vector2Int(Mathf.CeilToInt(contentRect.width), Mathf.CeilToInt(contentRect.height));
            if (size.x < 8 || size.y < 8) return;
            try
            {
                s.EnsureGpu();
                s.EnsureHoverLines();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return;
            }
            bool hover = s.Interaction.HoverGroups.Count > 0 && !UiRules.PaintMode;
            var key = (s.Pipeline.Version, SettingsService.Revision, s.Interaction.HoverGroups.Count > 0 ? s.Interaction.HoverGroups[0] : -1,
                UVToView, size, s.Interaction.HoverMirrorGroup, s.Interaction.HoverWouldAdd);
            if (key.Equals(_renderKey) && _image.image != null) return;
            _renderKey = key;
            _image.image = s.UVView.Render(s, size, UVToView, hover, s.Interaction.HoverWouldAdd);
            _image.MarkDirtyRepaint();
        }

        private void DrawCursor(MeshGenerationContext ctx)
        {
            if (!_pointerInside || SessionController.Current == null || !UiRules.PaintMode) return;
            var p = ctx.painter2D;
            p.strokeColor = PaintCommit.PaintValue(SettingsService.Get(SettingKeys.PaintTool), CurrentMods) ? Color.white : new Color(1f, 0.45f, 0.45f, 1f);
            p.lineWidth = 1.5f;
            var tool = SettingsService.Get(SettingKeys.PaintTool);
            if ((tool == PaintSubMode.Rect || tool == PaintSubMode.Lasso) && _painting && _shape.Count >= 2)
            {
                var pts = tool == PaintSubMode.Rect ? PolygonFill.Rect(_shape[0], _shape[1]) : _shape;
                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Count; i++) p.LineTo(pts[i]);
                p.ClosePath();
                p.Stroke();
                return;
            }
            if (tool == PaintSubMode.Brush || tool == PaintSubMode.Eraser)
            {
                p.BeginPath();
                p.Arc(_pointer, SettingsService.Get(SettingKeys.BrushSize), 0f, 360f);
                p.Stroke();
            }
        }

        private EventModifiers _mods;
        private EventModifiers CurrentMods => _mods;

        // ── 入力 ──

        private void OnPointerDown(PointerDownEvent e)
        {
            var s = SessionController.Current;
            if (s == null) return;
            Focus();
            _pointer = e.localPosition;
            _mods = e.modifiers;
            if (e.button == 2)
            {
                _panning = true;
                this.CapturePointer(e.pointerId);
                e.StopPropagation();
                return;
            }
            if (e.button == 1 && e.ctrlKey && UiRules.PaintMode)
            {
                _resizing = true;
                _resizeStartX = e.localPosition.x;
                _resizeStartSize = SettingsService.Get(SettingKeys.BrushSize);
                this.CapturePointer(e.pointerId);
                e.StopPropagation();
                return;
            }
            if (e.button != 0) return;
            this.CapturePointer(e.pointerId);
            if (UiRules.PaintMode) BeginPaint(s, e);
            else
            {
                Query(s, e.localPosition);
                if (_tris.Count > 0) _select.Begin(s, _groups, _tris, e.modifiers, e.clickCount);
            }
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            var s = SessionController.Current;
            if (s == null) return;
            var delta = (Vector2)e.localPosition - _pointer;
            _pointer = e.localPosition;
            _mods = e.modifiers;
            if (_panning)
            {
                _pan += delta;
            }
            else if (_resizing)
            {
                SessionCommands.SetBrushSize(_resizeStartSize + Mathf.RoundToInt((e.localPosition.x - _resizeStartX) * 0.5f));
            }
            else if (_painting)
            {
                MovePaint(s);
            }
            else
            {
                Query(s, e.localPosition);
                if (_select.Active && _groups.Count > 0) _select.Move(s, _groups);
                UpdateHover(s);
            }
            _cursor.MarkDirtyRepaint();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            var s = SessionController.Current;
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            _panning = false;
            _resizing = false;
            if (s == null) return;
            if (_painting) EndPaint(s);
            if (_select.Active) _select.End();
            _cursor.MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent e)
        {
            if (SessionController.Current == null) return;
            if (e.shiftKey && !UiRules.PaintMode)
            {
                float d = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
                if (d != 0f) SessionCommands.Grow(d < 0f);
            }
            else
            {
                Vector2 uv = ViewToUV(e.localMousePosition);
                _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0 ? 1f / 1.15f : 1.15f), 0.5f, 32f);
                var m = UVToView;
                Vector2 after = new Vector2(uv.x * m.x + m.z, uv.y * m.y + m.w);
                _pan += (Vector2)e.localMousePosition - after;
            }
            e.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (SessionController.Current == null || e.actionKey) return;
            switch (e.keyCode)
            {
                case KeyCode.Tab: SessionCommands.TogglePaintMode(); break;
                case KeyCode.Alpha1: SessionCommands.SetGranularity(SelectionGranularity.UVIsland); break;
                case KeyCode.Alpha2: SessionCommands.SetGranularity(SelectionGranularity.ConnectedMesh); break;
                case KeyCode.Alpha3: SessionCommands.SetGranularity(SelectionGranularity.Polygon); break;
                case KeyCode.B: SessionCommands.SetPaintTool(PaintSubMode.Brush); break;
                case KeyCode.M: SessionCommands.SetPaintTool(PaintSubMode.Rect); break;
                case KeyCode.L: SessionCommands.SetPaintTool(PaintSubMode.Lasso); break;
                case KeyCode.E: SessionCommands.SetPaintTool(PaintSubMode.Eraser); break;
                case KeyCode.X: SessionCommands.ToggleSymmetry(); break;
                case KeyCode.F: _zoom = 1f; _pan = Vector2.zero; break;
                default: return;
            }
            e.StopPropagation();
        }

        // ── 選択・ホバー（重なった UV は全部に効く。Impl selection_system §6.2） ──

        private void Query(WorkSession s, Vector2 local)
        {
            s.UVGrid.Query(ViewToUV(local), s.Active, _tris);
            _groups.Clear();
            var table = s.Groups;
            foreach (int t in _tris)
            {
                int g = table.TriToGroup[t];
                if (!_groups.Contains(g)) _groups.Add(g);
            }
        }

        private void UpdateHover(WorkSession s)
        {
            var it = s.Interaction;
            it.HoverGroups.Clear();
            it.HoverMirrorGroup = -1;
            if (UiRules.PaintMode) return;
            it.HoverGroups.AddRange(_groups);
            it.HoverFromUVView = true;
            it.HoverWouldAdd = SelectGesture.DecideAdd(s, s.Groups, _groups, _mods);
            SceneView.RepaintAll();
        }

        private void ClearHover()
        {
            var s = SessionController.Current;
            if (s != null && s.Interaction.HoverFromUVView) s.Interaction.ClearHover();
            _cursor.MarkDirtyRepaint();
        }

        // ── ペイント（UV ビューのストロークは対称に反映しない。Impl mask_processing §6.6） ──

        private void BeginPaint(WorkSession s, PointerDownEvent e)
        {
            s.EnsureGpu();
            _painting = true;
            _tool = SettingsService.Get(SettingKeys.PaintTool);
            _paintValue = PaintCommit.PaintValue(_tool, e.modifiers);
            _prev = e.localPosition;
            _shape.Clear();
            if (_tool == PaintSubMode.Rect || _tool == PaintSubMode.Lasso) _shape.Add(e.localPosition);
            else s.PaintOps.ViewCapsule(_prev, _prev, SettingsService.Get(SettingKeys.BrushSize), _paintValue, UVToView);
        }

        private void MovePaint(WorkSession s)
        {
            if (_tool == PaintSubMode.Rect)
            {
                if (_shape.Count < 2) _shape.Add(_pointer);
                else _shape[1] = _pointer;
            }
            else if (_tool == PaintSubMode.Lasso)
            {
                if ((_shape[_shape.Count - 1] - _pointer).sqrMagnitude >= 4f) _shape.Add(_pointer);
            }
            else
            {
                s.PaintOps.ViewCapsule(_prev, _pointer, SettingsService.Get(SettingKeys.BrushSize), _paintValue, UVToView);
                _prev = _pointer;
            }
        }

        private void EndPaint(WorkSession s)
        {
            _painting = false;
            if (_tool == PaintSubMode.Rect || _tool == PaintSubMode.Lasso)
            {
                var poly = _tool == PaintSubMode.Rect ? (_shape.Count >= 2 ? PolygonFill.Rect(_shape[0], _shape[1]) : null) : _shape;
                if (poly != null && poly.Count >= 3)
                {
                    int w = Mathf.CeilToInt(contentRect.width), h = Mathf.CeilToInt(contentRect.height);
                    var tex = s.ShapeMask.Build(poly, w, h);
                    if (tex != null) s.PaintOps.ViewShape(tex, new Vector2(w, h), _paintValue, UVToView);
                }
                _shape.Clear();
            }
            PaintCommit.Commit(s);
        }

        // ── 既存マスクの取り込み（テクスチャのドロップ） ──

        private static string DraggedTexturePath()
        {
            foreach (var o in DragAndDrop.objectReferences)
                if (o is Texture2D) return AssetDatabase.GetAssetPath(o);
            return null;
        }

        private void OnDragUpdated(DragUpdatedEvent e)
        {
            if (SessionController.Current == null || DraggedTexturePath() == null) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            SetDropVisual(true);
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            var path = DraggedTexturePath();
            SetDropVisual(false);
            if (path == null) return;
            DragAndDrop.AcceptDrag();
            SessionCommands.ImportMask(path, MessageOrigin.Window);
        }

        private void SetDropVisual(bool on)
        {
            parent?.EnableInClassList("mm-drop-target", on);
            Bind.Show(_dropLabel, on);
        }
    }
}
