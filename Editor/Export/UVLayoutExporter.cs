using System;
using System.IO;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>UV 展開図の書き出し（Impl export §4）。</summary>
    internal static class UVLayoutExporter
    {
        public static int OutputSize() => Mathf.Clamp(SettingsService.Get(SettingKeys.UVLayoutSize), 64, 8192);

        /// <summary>保存先のパス（同名のファイルがあれば番号付き）。フォルダは作らない。</summary>
        public static string PlannedPath(WorkSession s) =>
            OutputPaths.ResolveUnique(OutputPaths.FolderFor(s), s.EffectiveFileName + "_uv");

        /// <summary>書き出して true を返す。書き込みに失敗したら false（エラーは通知済み）。</summary>
        public static bool Export(WorkSession s)
        {
            var gpu = s.Gpu;
            int size = OutputSize();
            var rt = gpu.CreateColorRT("UVLayout", size, size);
            Texture2D baseTex = null;
            try
            {
                if (SettingsService.Get(SettingKeys.UVLayoutBackgroundMode) == UVLayoutBackground.BaseTexture)
                    baseTex = LoadBaseTexture(s);
                Render(s, rt, baseTex);

                byte[] rgba = GpuReadback.ReadRGBA32(rt);
                string folder = OutputPaths.ResolveFolder(s);
                string path = OutputPaths.ResolveUnique(folder, s.EffectiveFileName + "_uv");
                MaskExporter.Write(path, rgba, new Vector2Int(size, size));
                MaskTexturePostprocessor.Register(path, MaskTexturePostprocessor.Kind.Layout);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Notifier.Post(new UserMessage
                {
                    Level = MessageLevel.Success,
                    Text = L10n.F("status.layoutSaved", Path.GetFileName(path)),
                    Origin = MessageOrigin.Window,
                    AssetPath = path,
                });
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                Debug.LogException(e);
                Notifier.Post(MessageLevel.Error, L10n.T("status.writeFailed"));
                return false;
            }
            finally
            {
                if (baseTex != null) UnityEngine.Object.DestroyImmediate(baseTex);
                gpu.Release(rt);
            }
        }

        /// <summary>プレビューの線幅の倍率（縮小表示でも線が見えるように太くする）。</summary>
        private const float PreviewLineScale = 1.5f;

        /// <summary>
        /// 書き出しウィンドウのプレビュー。表示サイズの rt に、線を太めにして描く。
        /// 背景はベーステクスチャのアセットをそのまま使う（元ファイルの読み込みは書き出し時だけ）。
        /// </summary>
        public static void RenderPreview(WorkSession s, RenderTexture rt)
        {
            Texture background = SettingsService.Get(SettingKeys.UVLayoutBackgroundMode) == UVLayoutBackground.BaseTexture ? s.BaseTexture : null;
            Render(s, rt, background, PreviewLineScale);
        }

        /// <summary>展開図を rt に描く（背景 → アイランド着色 → ワイヤー → 縁）。background が null なら透過。</summary>
        private static void Render(WorkSession s, RenderTexture rt, Texture background, float lineScale = 1f)
        {
            var gpu = s.Gpu;
            int size = rt.width;
            bool selectionOnly = SettingsService.Get(SettingKeys.UVLayoutSelectionOnly);
            // 線は有効三角形（対象サブメッシュ）の物だけ。選択限定ならさらに選択済みに絞る
            var filter = selectionOnly ? TriangleBits.And(EditState.instance.SelectedBits, s.Active) : s.EdgeFilter;

            var wire = new LineMesh(gpu, "LayoutWire");
            var seams = new LineMesh(gpu, "LayoutSeams");
            GraphicsBuffer colors = null;
            try
            {
                s.EnsureGpu();
                DrawBackground(rt, background);

                using (new RenderTargetScope(rt))
                {
                    if (SettingsService.Get(SettingKeys.UVLayoutColorFill))
                    {
                        colors = gpu.CreateBuffer(s.Analysis.TriangleCount, 16);
                        colors.SetData(FillColors(s));
                        var m = gpu.UVRaster;
                        s.Tri.Bind(m);
                        m.SetBuffer(ShaderIds.TriColors, colors);
                        m.SetFloat(ShaderIds.RequireSelected, selectionOnly ? 1f : 0f);
                        foreach (var offset in TriMesh.WrapOffsets(s.Analysis.NeedsWrap))
                        {
                            m.SetVector(ShaderIds.UVOffset, offset);
                            m.SetPass(ShaderIds.RasterColorFill);
                            Graphics.DrawMeshNow(s.Tri.Mesh, Matrix4x4.identity);
                        }
                    }

                    var line = gpu.Line;
                    line.SetVector(ShaderIds.UVToClip, new Vector4(2f, 2f, -1f, -1f));
                    line.SetVector(ShaderIds.TargetSize, new Vector4(size, size, 0, 0));
                    if (SettingsService.Get(SettingKeys.UVLayoutWireframe))
                    {
                        wire.SetUV(s.Analysis.WireEdges, filter);
                        Draw(line, wire, LineColor(SettingsService.Get(SettingKeys.UVLayoutWireColor)), 1f * lineScale);
                    }
                    if (SettingsService.Get(SettingKeys.UVLayoutSeams))
                    {
                        seams.SetUV(s.Analysis.BorderEdges, filter);
                        Draw(line, seams, LineColor(SettingsService.Get(SettingKeys.UVLayoutSeamColor)), 2f * lineScale);
                    }
                }
            }
            finally
            {
                gpu.Release(colors);
                gpu.Release(wire.Mesh);
                gpu.Release(seams.Mesh);
            }
        }

        /// <summary>ベーステクスチャの元ファイルを読む（インポート時の圧縮・縮小を避ける）。読めなければ null。</summary>
        private static Texture2D LoadBaseTexture(WorkSession s)
        {
            if (s.BaseTexture == null
                || !SourceImage.TryLoadRgba(AssetDatabase.GetAssetPath(s.BaseTexture), out byte[] rgba, out int w, out int h))
                return null;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.LoadRawTextureData(rgba);
            tex.Apply(false, false);
            return tex;
        }

        private static void DrawBackground(RenderTexture rt, Texture background)
        {
            var prev = RenderTexture.active;
            if (background != null)
            {
                Graphics.Blit(background, rt);
            }
            else
            {
                Graphics.SetRenderTarget(rt);
                GL.Clear(false, true, new Color(0, 0, 0, 0));
            }
            RenderTexture.active = prev;
        }

        /// <summary>ポリゴン（面）の塗りつぶし色。島ごと（黄金比色相）または単色。</summary>
        private static Vector4[] FillColors(WorkSession s)
        {
            var mode = SettingsService.Get(SettingKeys.UVLayoutFill);
            if (mode == UVLayoutFillColor.Islands) return IslandColors(s);

            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color baseColor;
            switch (mode)
            {
                case UVLayoutFillColor.White: baseColor = Color.white; break;
                case UVLayoutFillColor.Black: baseColor = Color.black; break;
                case UVLayoutFillColor.Cyan:  baseColor = new Color(0f, 0.898f, 1f, 1f); break;
                default:                      baseColor = Color.white; break;
            }
            baseColor.a = 0.35f;
            Color c = linear ? baseColor.linear : baseColor;
            var v = (Vector4)c;

            var result = new Vector4[s.Analysis.TriangleCount];
            for (int t = 0; t < result.Length; t++) result[t] = v;
            return result;
        }

        /// <summary>島ごとの色。黄金比で色相を進めて、隣の番号ほど色が離れるようにする。</summary>
        private static Vector4[] IslandColors(WorkSession s)
        {
            var islands = s.Analysis.Islands;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var perIsland = new Color[islands.Count];
            for (int g = 0; g < islands.Count; g++)
            {
                float hue = (g * 0.618034f) % 1f;
                var c = Color.HSVToRGB(hue, 0.45f, 1f);
                c.a = 0.35f;
                perIsland[g] = linear ? c.linear : c;
            }
            var result = new Vector4[s.Analysis.TriangleCount];
            for (int t = 0; t < result.Length; t++) result[t] = perIsland[islands.TriToGroup[t]];
            return result;
        }

        private static Color LineColor(UVLayoutLineColor c)
        {
            switch (c)
            {
                case UVLayoutLineColor.Black: return Color.black;
                case UVLayoutLineColor.Cyan: return new Color(0f, 0.898f, 1f, 1f); // #00E5FF
                case UVLayoutLineColor.Red: return new Color(1f, 0.2f, 0.2f, 1f);  // #FF3333
                default: return Color.white;
            }
        }

        private static void Draw(Material line, LineMesh mesh, Color color, float thickness)
        {
            if (mesh.SegmentCount == 0) return;
            line.SetColor(ShaderIds.Color, color);
            line.SetFloat(ShaderIds.Thickness, thickness);
            line.SetPass(ShaderIds.LineUV);
            Graphics.DrawMeshNow(mesh.Mesh, Matrix4x4.identity);
        }
    }
}
