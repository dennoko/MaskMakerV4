using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// SceneView の入力の解釈（UI 設計 §5.1）。Alt・中ボタン・Ctrl なしの右ドラッグは Unity のカメラ操作に任せる。
    /// </summary>
    internal static class SceneInput
    {
        private static readonly SelectGesture Select = new SelectGesture();
        private static readonly ScenePaintGesture Paint = new ScenePaintGesture();
        private static readonly List<int> Groups = new List<int>(1);
        private static readonly List<int> Tris = new List<int>(1);

        private static bool _resizing;
        private static float _resizeStartX;
        private static int _resizeStartSize;

        public static void Handle(SceneView view, WorkSession s)
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            s.Interaction.PointerGui = e.mousePosition;
            s.Interaction.PointerView = view;

            if (SessionCommands.IsPaused)
            {
                if (s.Interaction.HoverValid) { s.Interaction.ClearHover(); view.Repaint(); }
                EndGestures(view, s);
                return;
            }

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(id);
                return;
            }
            if (e.type == EventType.MouseEnterWindow) view.Focus();
            if (e.alt && GUIUtility.hotControl != id) return;

            bool paintMode = SettingsService.Get(SettingKeys.IsPaintMode);
            switch (e.type)
            {
                case EventType.MouseMove:
                    UpdateHover(view, s, e, paintMode);
                    SceneHud.OnPointerMoved(view, e.mousePosition);
                    view.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button == 1 && e.control && paintMode)
                    {
                        _resizing = true;
                        _resizeStartX = e.mousePosition.x;
                        _resizeStartSize = SettingsService.Get(SettingKeys.BrushSize);
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    else if (e.button == 0)
                    {
                        UpdateHover(view, s, e, paintMode);
                        GUIUtility.hotControl = id;
                        if (paintMode) Paint.Begin(view, s, e);
                        else if (s.Interaction.HoverTriangle >= 0)
                        {
                            Tris.Clear();
                            Tris.Add(s.Interaction.HoverTriangle);
                            Select.Begin(s, s.Interaction.HoverGroups, Tris, e.modifiers, e.clickCount);
                        }
                        Hints.OnClick();
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    if (_resizing)
                    {
                        SessionCommands.SetBrushSize(_resizeStartSize + Mathf.RoundToInt((e.mousePosition.x - _resizeStartX) * 0.5f));
                    }
                    else
                    {
                        UpdateHover(view, s, e, paintMode);
                        if (Paint.Active) Paint.Move(view, s, e);
                        else if (Select.Active && s.Interaction.HoverTriangle >= 0) Select.Move(s, s.Interaction.HoverGroups);
                    }
                    view.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    EndGestures(view, s);
                    UpdateHover(view, s, e, paintMode);
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    if (e.shift && !paintMode)
                    {
                        float d = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
                        if (d != 0f) SessionCommands.Grow(d < 0f);
                        e.Use();
                    }
                    break;

                case EventType.KeyDown:
                case EventType.KeyUp:
                    if (e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift
                        || e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl)
                    {
                        UpdateHover(view, s, e, paintMode);
                        view.Repaint();
                    }
                    break;
            }
        }

        private static void EndGestures(SceneView view, WorkSession s)
        {
            _resizing = false;
            if (Paint.Active) Paint.End(view, s);
            if (Select.Active) Select.End();
        }

        /// <summary>カーソル下の三角形・グループ・クリック結果（追加 / 除外）を求める。</summary>
        public static void UpdateHover(SceneView view, WorkSession s, Event e, bool paintMode)
        {
            var it = s.Interaction;
            it.HoverFromUVView = false;
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var w2l = s.Target.transform.worldToLocalMatrix;
            var l2w = s.Mesh.LocalToWorld;
            Vector3 o = w2l.MultiplyPoint3x4(ray.origin);
            Vector3 d = w2l.MultiplyVector(ray.direction).normalized;
            if (!s.Raycaster.Raycast(o, d, s.Active, out var hit))
            {
                it.ClearHover();
                return;
            }
            it.HoverValid = true;
            it.HoverTriangle = hit.Triangle;
            it.HoverPoint = l2w.MultiplyPoint3x4(hit.Point);
            it.HoverNormal = w2l.transpose.MultiplyVector(hit.Normal).normalized;

            it.HoverGroups.Clear();
            it.HoverMirrorGroup = -1;
            if (paintMode) return;
            var table = s.Groups;
            int g = table.TriToGroup[hit.Triangle];
            it.HoverGroups.Add(g);
            it.HoverMirrorGroup = SessionCommands.MirrorOf(g, table);
            it.HoverWouldAdd = SelectGesture.DecideAdd(s, table, it.HoverGroups, e.modifiers);
        }

        /// <summary>BrushSize（画面上の半径 pt）を交点の深度でのワールド長にする（Impl mask_processing §6.4）。</summary>
        public static float BrushWorldRadius(Camera cam, Vector3 point)
        {
            float px = SettingsService.Get(SettingKeys.BrushSize) * EditorGUIUtility.pixelsPerPoint;
            float perPixel;
            if (cam.orthographic) perPixel = 2f * cam.orthographicSize / Mathf.Max(1, cam.pixelHeight);
            else
            {
                float depth = Mathf.Max(1e-4f, Vector3.Dot(point - cam.transform.position, cam.transform.forward));
                perPixel = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            }
            return px * perPixel;
        }
    }

    /// <summary>迷い検知ヒント（UI 設計 §2.6.3 段階 3）。各ヒントは 3 回まで。</summary>
    internal static class Hints
    {
        private const int MaxShown = 3;
        private static double _workStart = -1;
        private static bool _clicked;

        public static void OnSessionStarted()
        {
            _workStart = EditorApplication.timeSinceStartup;
            _clicked = false;
        }

        public static void OnClick() => _clicked = true;

        /// <summary>今出すべきヒント（無ければ null）。SceneHud が定期的に問い合わせる。</summary>
        public static string Current(WorkSession s)
        {
            if (s == null || _workStart < 0) return null;
            if (!_clicked && s.Interaction.HoverValid && !SettingsService.Get(SettingKeys.IsPaintMode)
                && EditorApplication.timeSinceStartup - _workStart > 3.0)
                return TryShow("click", "hint.click");
            return null;
        }

        private static string TryShow(string id, string textKey)
        {
            var key = SettingKeys.HintShownCount(id);
            int n = SettingsService.Get(key);
            if (n >= MaxShown) return null;
            SettingsService.Set(key, n + 1);
            _clicked = true; // 同じ作業中には 1 回だけ
            return L10n.T(textKey);
        }
    }
}
