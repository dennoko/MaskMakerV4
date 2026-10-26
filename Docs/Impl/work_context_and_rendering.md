# UV Mask Maker 機能仕様書 - 作業コンテキスト・描画・設定データ (Work Context, Rendering & Settings)

## 1. 概要

本モジュールは、Modular Avatar等の非破壊変形プラグイン（NDMF）と連携して**その場（In-Place）で安全に作業するための作業環境（メッシュ孤立表示・一時ステージ）**、SceneView上での3Dオーバーレイ描画（シーム線・マスク投影）、およびツールの状態・設定データモデルを規定します。

---

## 2. 作業コンテキスト仕様 (In-Place Work Context & NDMF)

### 2.1 従来の「オフセットWorkCopy生成」の廃止理由
過去バージョンでは、元の位置から横にオフセット（`+1.5m` 等）した位置に変形のない静的メッシュ複製体を生成していました。しかし、以下の課題が判明したためこの方式は廃止されました：
1. **カメラ移動の手間**: 複製されたメッシュまでSceneViewの視点を移動・フォーカスし直す操作が必要。
2. **元メッシュ対比の不要性**: マスク作成の作業中に、元のメッシュを並べて見比べる必要性は皆無。
3. **FBX原点・向きの不整合**: モデルのFBX設定（軸の向きや親のTransform行列）によっては、複製メッシュの回転が崩れて意図しない向きに傾いてしまう。

---

### 2.2 新仕様: その場（In-Place）作業環境と NDMF プレビュー連携

#### A. アバター本来の位置・姿勢での作業 (In-Place Editing)
オブジェクトを別座標へ移動・複製せず、**アバター本来のワールド位置・姿勢のまま作業**を行います。

#### B. NDMF (Non-Destructive Modular Framework) との連携
Modular Avatar等を使用しているアバターの場合、エディタ上で変形前の生メッシュを直接参照すると、衣装の体型追従やメッシュ統合が未適用の状態になってしまいます。
- **仕様**: NDMFが提供するプレビュー機構（または `ApplyOnClone` による事前処理メッシュ）を取得し、モディファイア適用後の最終形状に対してUV解析および3Dピッキングを行います。
- ターゲットが `SkinnedMeshRenderer` の場合も、NDMFプレビュー適用済みの変形後メッシュ（あるいは現在のポーズが反映されたメッシュ）を基準とします。

---

### 2.3 作業メッシュの孤立表示仕様 (Isolation)

作業対象以外の服、装飾、背景オブジェクトなどが重なって作業の邪魔になるのを防ぐため、作業中は対象メッシュ以外の描画を遮断します。以下のいずれかの方式により実現します。

#### 方式1: シーン内孤立表示 (Scene Visibility Isolation)
- **開始時**:
  - Unity標準の `SceneVisibilityManager.instance.Isolate(targetGameObject, true)` を実行。
  - 対象オブジェクトとその子階層以外をSceneView上で自動的に不可視化・非アクティブ化。
- **終了時**:
  - `SceneVisibilityManager.instance.ExitIsolation()` を呼び出し、元の可視状態を完全復元。

#### 方式2: 一時作業ステージ (Temporary Preview Scene / Stage)
- **開始時**:
  - 専用の隔離された一時シーン（または `PreviewScene`）を生成。
  - 対象メッシュのみを適切なライティング環境とともにロードし、SceneViewまたは独立したステージビューに表示。
- **終了時**:
  - 作業完了と同時に一時シーンを破棄。シーン内のオリジナルヒエラルキーに一切変更を残さない。

---

## 3. SceneViewオーバーレイ描画仕様 (OverlayRenderer)

SceneView描画は `SceneView.duringSceneGui` イベント内の `EventType.Repaint` タイミングで実行されます。描画負荷を低減するため、ワールド空間頂点および法線ベクトルはローカル行列変更時のみ再計算されるキャッシュ機構（`EnsureWorldCache`）を持ちます。

### 3.1 UVシーム（境界線）の描画
- **参照データ**: `UVAnalysis.SeamEdges3D`（重複排除された3Dエッジリスト）
- **深度テスト (`ZTest`)**:
  - `OverlayOnTop = true`（X-Rayモード）: `CompareFunction.Always`（メッシュの裏側でも常に最前面描画）
  - `OverlayOnTop = false`: `CompareFunction.LessEqual`（遮蔽物がある場合は隠れる）
- **Z-fighting（チラつき）防止**:
  - 各頂点を頂点法線方向にわずかに押し出し:  
    $$\text{pos} = \text{worldPos} + \text{worldNormal} \times \text{OverlayDepthOffset}$$
- **アンチエイリアス (AA) 切替**:
  - `DisableAA = true`: `Handles.DrawLines` による高速描画。
  - `DisableAA = false`: `Handles.DrawAAPolyLine(thickness, p0, p1)` による高品質アンチエイリアス描画。

### 3.2 テクスチャ投影型マスクオーバーレイ
選択されたアイランドおよび手塗りペイントのストロークを、リアルタイムに3Dモデル表面へブレンド投影します。ワイヤーフレームではなく面として塗られるため、ペイント結果が3D上で直感的に確認できます。

#### 投影アルゴリズム
1. プレビュー用半透明オーバーレイテクスチャ（`Texture2D`）を取得。
   - 選択/ペイントされたピクセル: 指定色 `SelectedSceneColor`（Alpha値: `PreviewOverlayAlpha`）
   - 未選択ピクセル: 完全透明 `(0, 0, 0, 0)`
2. 専用シェーダー `Hidden/UVMaskMaker/MaskOverlay`（フォールバック: `Unlit/Transparent`）を適用。
   - ブレンドモード: `Blend SrcAlpha OneMinusSrcAlpha`
   - カリング: `Cull Off`（両面表示）
   - アルファテスト: `clip(col.a - 0.004)`（透明ピクセルを高速破棄）
3. **GL Immediate Mode** で全三角形を描画：
   ```csharp
   GL.PushMatrix();
   GL.LoadProjectionMatrix(cam.projectionMatrix);
   GL.modelview = cam.worldToCameraMatrix;
   GL.Begin(GL.TRIANGLES);
   foreach (var tri in analysis.Triangles)
   {
       GL.TexCoord2(tri.uv0.x, tri.uv0.y); GL.Vertex(worldPosA);
       GL.TexCoord2(tri.uv1.x, tri.uv1.y); GL.Vertex(worldPosB);
       GL.TexCoord2(tri.uv2.x, tri.uv2.y); GL.Vertex(worldPosC);
   }
   GL.End();
   GL.PopMatrix();
   ```

---

## 4. 設定データ構造 (MaskSettings)

ツールの状態およびユーザー設定を保持するシリアライズ可能なデータクラスです。エディタセッションを跨いで保持（JSONまたはEditorPrefs）されます。

```csharp
[System.Serializable]
public class MaskSettings
{
    // 出力テクスチャ設定
    public int TextureSize = 512;          // 512, 1024, 2048, 4096
    public int PixelMargin = 2;            // 0〜32 (px)
    public bool InvertMask = false;        // 黒白反転
    public bool SaveInvertedToo = false;   // 反転マスク (_inv.png) の同時出力
    public bool UseTextureFolder = false;  // メインテクスチャと同一フォルダに保存
    public string OutputDir = "Assets/GeneratedMasks";
    public string FileName = "uv_mask";

    // チャンネル別書き込み設定
    public bool ChannelWriteEnabled = false;
    public bool WriteR = true;
    public bool WriteG = false;
    public bool WriteB = false;
    public bool WriteA = false;

    // 操作モード・状態
    public bool AddMode = true;                           // true: 追加, false: 削除
    public SelectionGranularity Granularity = SelectionGranularity.UVIsland;
    public bool IsPaintMode = false;                      // 選択モード / ペイントモード
    public bool ScenePickPaused = false;                  // シーン選択一時停止フラグ
    public PaintSubMode PaintSubMode = PaintSubMode.Brush;
    public int BrushSize = 50;                            // 1〜100 (px)
    public KeyCode ModeToggleHotkey = KeyCode.R;          // Add/Remove切替ホットキー

    // UV / サブメッシュ設定
    public int UVChannel = 0;                             // UV0〜UV7
    public int TargetSubmesh = 0;                         // -1: 全体, 0以上: 特定サブメッシュ

    // SceneViewオーバーレイ設定
    public bool OverlayOnTop = false;                     // X-Ray表示
    public float OverlayDepthOffset = 0f;                 // Z-fighting回避オフセット
    public float OverlaySeamThickness = 2.5f;             // シーム線太さ
    public bool DisableAA = true;                         // アンチエイリアス無効化（高速化）
    public Color SelectedSceneColor = new Color(0f, 1f, 1f, 1f); // 選択面の強調色
    public Color SeamColor = new Color(1f, 0.15f, 0.15f, 1f);   // シーム線の色

    // プレビュー設定
    public bool PreviewOverlayBaseTex = false;            // ベーステクスチャの重ね表示
    public float PreviewOverlayAlpha = 0.6f;

    // 作業コンテキスト設定
    public bool IsolateTargetMesh = true;                 // 作業中に対象メッシュ以外を非表示にする

    // ローカライズ設定
    public string Language = "ja";
    public bool UseEnglish = false;
}
```
