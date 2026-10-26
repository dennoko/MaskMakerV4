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
        /// <summary>書き出して true を返す。書き込みに失敗したら false（エラーは通知済み）。</summary>
        public static bool Export(WorkSession s)
        {
            var gpu = s.Gpu;
            int size = Mathf.Clamp(SettingsService.Get(SettingKeys.UVLayoutSize), 64, 8192);
            bool selectionOnly = SettingsService.Get(SettingKeys.UVLayoutSelectionOnly);
            // 線は有効三角形（対象サブメッシュ）の物だけ。選択限定ならさらに選択済みに絞る
            var filter = selectionOnly ? TriangleBits.And(EditState.instance.SelectedBits, s.Active) : s.EdgeFilter;

            var rt = gpu.CreateColorRT("UVLayout", size, size);
            var wire = new LineMesh(gpu, "LayoutWire");
            var seams = new LineMesh(gpu, "LayoutSeams");
            GraphicsBuffer colors = null;
            try
            {
                s.EnsureGpu();
                DrawBackground(s, rt);

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
                        Draw(line, wire, LineColor(SettingsService.Get(SettingKeys.UVLayoutWireColor)), 1f);
                    }
                    if (SettingsService.Get(SettingKeys.UVLayoutSeams))
                    {
                        seams.SetUV(s.Analysis.BorderEdges, filter);
                        Draw(line, seams, LineColor(SettingsService.Get(SettingKeys.UVLayoutSeamColor)), 2f);
                    }
                }

                byte[] rgba = GpuReadback.ReadRGBA32(rt);
                string folder = OutputPaths.ResolveFolder(s);
                string path = OutputPaths.ResolveSingle(folder, s.EffectiveFileName + "_uv", SettingKeys.LastUVLayoutPath);
                MaskExporter.Write(path, rgba, new Vector2Int(size, size));
                MaskTexturePostprocessor.Register(path, MaskTexturePostprocessor.Kind.Layout);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                SettingsService.Set(SettingKeys.LastUVLayoutPath, path);
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
                gpu.Release(colors);
                gpu.Release(wire.Mesh);
                gpu.Release(seams.Mesh);
                gpu.Release(rt);
            }
        }

        private static void DrawBackground(WorkSession s, RenderTexture rt)
        {
            Texture2D baseTex = null;
            if (SettingsService.Get(SettingKeys.UVLayoutBackgroundMode) == UVLayoutBackground.BaseTexture && s.BaseTexture != null
                && SourceImage.TryLoadRgba(AssetDatabase.GetAssetPath(s.BaseTexture), out byte[] rgba, out int w, out int h))
            {
                baseTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                baseTex.LoadRawTextureData(rgba);
                baseTex.Apply(false, false);
            }
            var prev = RenderTexture.active;
            if (baseTex != null)
            {
                Graphics.Blit(baseTex, rt);
                UnityEngine.Object.DestroyImmediate(baseTex);
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
