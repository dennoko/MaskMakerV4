# UV Mask Maker V4 実装設計 - GPU パイプライン

[../Impl/mask_processing.md](../Impl/mask_processing.md) で定義した画像（Island / Coverage / PaintLow / D、手塗り）を GPU で作る方法を定めます。

---

## 1. 方針

- **フラグメントシェーダーだけ**を使う（コンピュートシェーダーは使わない）。描画命令は `CommandBuffer` に積んで `Graphics.ExecuteCommandBuffer` で実行する。
- マスク系の RenderTexture は **R8・リニア・ポイントフィルタ・Repeat**。値は 0 か 1 だけ。
- 結果は Impl の定義と画素単位で一致させる。GPU のラスタライズ規則（画素中心・Top-Left rule）は Impl mask_processing §4 と同じなので、UV をそのままクリップ座標に写せば定義どおりになる。
- シェーダーは `Editor/Shaders/` に置く（ビルドに含まれない）。`Shader.Find("Hidden/UVMaskMaker/…")` で読み、見つからなければ作業開始を失敗させる。
- `#pragma target 4.5`（`StructuredBuffer` を頂点シェーダーで読むため）。Editor の対象 API: D3D11 / D3D12 / Metal / Vulkan。

---

## 2. GPU 資源

### 2.1 メッシュとバッファ

| 資源 | 内容 | 更新するとき |
| :--- | :--- | :--- |
| **TriMesh** | 三角形ごとに頂点を分けたメッシュ（3T 頂点）。`POSITION` = ローカル位置、`NORMAL` = ローカル法線、`TEXCOORD0` = 補正後 UV（`TriUV`）、`TEXCOORD1.x` = 三角形番号 | UV: 解析時 / 位置・法線: ポーズ変化時 |
| **TriFlags** | `GraphicsBuffer`（`uint` × T）。bit0 = 選択済み、bit1 = 有効（対象サブメッシュ） | 選択・対象サブメッシュの変更時（10 万三角形で 400KB の転送） |
| **SeamLines3D** | `SeamEdges3D` の太線メッシュ（線分ごとに 4 頂点。両端の位置と、どちら側の頂点かを属性に持つ） | ポーズ変化時 |
| **BorderLinesUV** / **WireLinesUV** | `BorderEdges` / 全三角形の辺（共有辺は 1 回）の UV 空間の太線メッシュ | 解析時 |
| **HoverLines** | ホバー中グループ（＋対称グループ）の外周 | ホバー対象が変わった時 |

TriMesh を 1 つ持てば、UV 空間への描画（UV を位置として使う）と、3D への描画（ローカル位置を使う）の両方を同じメッシュで行えます。三角形ごとのデータ（選択・有効・番号）も頂点属性とバッファで引けるので、`SV_PrimitiveID` に頼りません。

### 2.2 RenderTexture

| RT | サイズ | 形式 | 内容 |
| :--- | :--- | :--- | :--- |
| **PaintRT** | 4096² | R8 | 手塗りレイヤー |
| **CoverageRT** | W×H | R8 | Coverage |
| **IslandRT** | W×H | R8 | Island |
| **MergedRT** | W×H | R8 | M = Island ∨ PaintLow |
| **MaskRT** | W×H | R8 | D（表示と出力が読む唯一の RT） |
| **TempRT** | W×H | R8 | 余白の横方向パスの結果 |
| **UVViewRT** | UV ビューの表示サイズ | ARGB32 sRGB | UV ビューの表示 |
| **ShapeMask** | SceneView / UV ビューの画面サイズ | R8（Texture2D） | 矩形・投げ縄の内側 = 1 |

W×H は出力サイズ（Impl mask_processing §2）。4096² の時の合計はおよそ 16MB × 6 ≈ 96MB です。`SystemInfo.SupportsRenderTextureFormat(R8)` が false の環境では ARGB32 に切り替えます（シェーダーは .r だけを使うので変更不要）。

---

## 3. シェーダーとパス

| ファイル | パス | 描くもの |
| :--- | :--- | :--- |
| `UVRaster.shader` | `Island` | TriMesh を UV 空間へ。`TriFlags` が選択済み かつ 有効 の三角形だけ 1 |
| | `Coverage` | 同上。有効な三角形すべてを 1 |
| | `PaintWorld` | TriMesh を UV 空間へ。各画素の補間されたローカル位置・法線をワールドへ変換し、ブラシ（カプセル）または図形（ShapeMask を投影位置で引く）で判定（Impl mask_processing §6.4–6.6）。当たらなければ `discard`、当たれば `_PaintValue`（0 or 1） |
| | `ColorFill` | UV 展開図用。TriMesh を UV 空間へ、`_TriColors`（出力時だけ作る `float4` × T のバッファ）の島の色で塗る |
| `ImageOps.shader` | `Downsample` | PaintRT → MergedRT。N×N 標本の面積判定（Impl §6.2）し、IslandRT と OR を取って M にする |
| | `DilateH` | MergedRT → TempRT。横に ±m の最大値 |
| | `DilateV` | TempRT → MaskRT。縦に ±m の最大値を取り、CoverageRT が 1 の画素は MergedRT の値（元の M）を使う（D の完成） |
| | `PaintView` | 全画面で PaintRT へ。画素の UV を UV ビューの画面座標に写し、ブラシまたは ShapeMask で判定 |
| | `ImportOr` | 取り込み画像（2 値の Texture2D）を最近傍で PaintRT へ OR |
| | `Restore` | PaintRle を展開した Texture2D を PaintRT へコピー |
| `Overlay.shader` | `Scene` | TriMesh を 3D へ。MaskRT をポイントサンプリングし、D = 1 の所だけ `SelectedSceneColor × SceneMaskAlpha`。`Cull Off`、`ZTest [_ZTest]`、`Offset -1, -1` |
| | `UVView` | UVViewRT へ。ベーステクスチャ（任意）と MaskRT の重ね |
| `Line.shader` | `Line3D` / `LineUV` | 線分の両端を画面へ投影し、垂直方向に `_Thickness`/2 px ずつ広げた四角形を描く。縁は 1px で不透明度を落としてアンチエイリアス。`ZTest [_ZTest]`、`Offset -1, -1` |

合成の手順と RT の受け渡しは次のとおりです（読む RT と書く RT を必ず分ける）。

| 手順 | 入力 | 出力 |
| :--- | :--- | :--- |
| 1 `Downsample` | PaintRT、IslandRT | **MergedRT**（M） |
| 2 `DilateH` | MergedRT | **TempRT** |
| 3 `DilateV` | TempRT、MergedRT、CoverageRT | **MaskRT**（D） |

`m = 0` の時は手順 2・3 を飛ばし、MergedRT を MaskRT へコピーします（表示と出力は常に MaskRT を読む）。

### 3.1 共通 HLSL（UVMaskMakerCommon.hlsl）
- `float4 UVToClip(float2 uv, float2 offset)`: `uv + offset` を `[-1, 1]` に写し、`UNITY_UV_STARTS_AT_TOP` の環境で RT に描く時は y を反転する。**上下反転の扱いはこの 1 関数に閉じ込め**、GPU テスト（[implementation_plan.md](implementation_plan.md) §3）で確かめる。
- `float CapsuleDistance(float3 p, float3 a, float3 b)`。
- `float3 MirrorX(float3 p)`: `_MirrorMatrix`（ワールド → 基準空間で X 反転 → ワールド）を掛ける。
- フラグ定数 `FLAG_SELECTED = 1`、`FLAG_ACTIVE = 2`。

### 3.2 UV の回り込み
`NeedsWrap` の時、`Island`・`Coverage`・`PaintWorld` は TriMesh を `_UVOffset` を変えて 9 回描きます（(0,0) と 8 方向）。それ以外は 1 回です。

---

## 4. 合成（MaskPipeline.Ensure）

SceneView の再描画、UV ビューの描画、出力の直前に `MaskPipeline.Ensure()` を呼びます。入力のリビジョンを比べ（[state_and_lifecycle.md](state_and_lifecycle.md) §2.2）、必要な手順だけを実行します。

| 変わったもの | 実行する手順 |
| :--- | :--- |
| 出力サイズ | RT の作り直し → 全手順 |
| 対象サブメッシュ、解析 | Coverage → Island → 合成 |
| 選択 | TriFlags の転送 → Island → 合成 |
| 手塗り（ストローク中の描画、確定、復元） | 合成 |
| `PixelMargin` | 合成 |

「合成」は §3 の表の手順 1〜3 です。4096² でも数 ms で終わる想定なので、ストローク中もマウス移動のたびに実行します。

---

## 5. ペイント

### 5.1 ストローク中
| 場所 | 1 回のマウス移動での処理 |
| :--- | :--- |
| SceneView・ブラシ | CPU でレイを当て（`MeshRaycaster`）、前回と今回の交点から線分を作る → `PaintWorld` を PaintRT に描く |
| SceneView・矩形 / 投げ縄 | 図形の線だけを画面に描く。マウスを離した時に `PolygonFill` で ShapeMask を作り、`PaintWorld` を 1 回描く |
| UV ビュー | ブラシは `PaintView` を毎回、矩形 / 投げ縄は離した時に 1 回 |

`PaintWorld` に渡す値: `_ObjectToWorld`、カメラ位置と視線方向、ビュー射影行列と画面サイズ（図形の投影用）、線分 `a, b` と半径 `r`、`_XRay`（向きの判定を省く）、`_Mirror` と `_MirrorMatrix`、`_PaintValue`。

### 5.2 確定（マウスを離した時）
1. `GpuReadback.Read(PaintRT)` で 4096² のバイト列を得る（`AsyncGPUReadback.Request(...).WaitForCompletion()`、16MB）。
2. `PaintRle.Encode`。8 バイト単位で同じ値の並びを読み飛ばすと、ほとんどの領域を一気に進められる。
3. `EditCommands.CommitPaint(rle)` → Undo 記録、`PaintRevision++`。
4. `PaintSurface` は「GPU 側はこのリビジョンを反映済み」と記録する（自分で書いた変更を読み直して転送し直さない）。

### 5.3 復元（Undo / Redo、作業開始、デバイスロスト）
`PaintSurface.Ensure()` が `PaintRevision` の違いを見つけたら、PaintRle を展開して R8 の `Texture2D`（4096²、使い回し）に `LoadRawTextureData` → `Apply` し、`Restore` パスで PaintRT へ写します。

### 5.4 取り込み
`MaskImporter` が `SourceImage` で元ファイルを読み、CPU で 2 値化（`ImportThreshold`）して R8 の `Texture2D` にし、`ImportOr` で PaintRT へ OR → §5.2 と同じ確定処理で 1 件にします。

---

## 6. 出力時の読み戻し

| 出力 | 処理 |
| :--- | :--- |
| マスク PNG | `MaskPipeline.Ensure()` → MaskRT を読み戻し → `MaskEncoder` で白黒 / チャンネル書き込みの RGBA32 配列にする（Base PNG は `SourceImage` で読んだ配列）→ `ImageConversion.EncodeArrayToPNG` |
| UV 展開図 | sRGB の ARGB32 RT（`UVLayoutSize`²）に、背景 → 島の色分け（`UVRaster` に色を渡すパス）→ `WireLinesUV` → `BorderLinesUV` の順に描いて読み戻し、PNG にする |

色の扱い: マスクの RT は値そのもの（データ）なのでリニア。UV 展開図は見た目の画像なので sRGB の RT に描き、指定色（sRGB の 16 進）とベーステクスチャ（sRGB として読み込み）が書き出した PNG で元の値に戻るようにします。

---

## 7. CPU 側に残す処理と理由

| 処理 | CPU に置く理由 |
| :--- | :--- |
| メッシュ解析（量子化・辞書・Union-Find） | 辞書と分岐が主体で、GPU に向かない。作業開始時に 1 回だけ |
| 3D ピッキング | 結果を 1 つだけすぐに使う。GPU から読み戻すと待ちが発生する |
| UV ピッキング | 重なった全候補を返す必要がある |
| 選択演算・対称の投票 | ビット列とグループ表で十分速い |
| 図形（矩形・投げ縄）の塗りつぶし | 画面サイズで 1 回だけ。偶奇規則を CPU で素直に書ける |
| 連長圧縮 | 履歴と保存のための直列処理 |
