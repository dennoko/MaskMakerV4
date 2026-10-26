# UV Mask Maker 機能仕様書 - 作業コンテキスト・描画・設定データ (Work Context, Rendering & Settings)

## 1. 概要

本モジュールは、Modular Avatar 等の非破壊変形プラグイン（NDMF）と連携して**その場（In-Place）で安全に作業するための作業環境**、作業の開始・終了・自動再開、SceneView 上のオーバーレイ描画、および設定項目とその保存階層を規定します。

---

## 2. 作業コンテキスト

### 2.1 従来の「オフセットWorkCopy生成」の廃止理由
過去バージョンでは、元の位置から横にオフセット（`+1.5m` 等）した位置に変形のない静的メッシュ複製体を生成していました。以下の理由で廃止しました。
1. **カメラ移動の手間**: 複製体まで SceneView の視点を移動し直す操作が必要。
2. **元メッシュ対比の不要性**: 作業中に元のメッシュと見比べる必要は無い。
3. **FBX原点・向きの不整合**: FBX の軸設定や親の Transform によって、複製体が意図しない向きに傾く。

### 2.2 その場（In-Place）作業と NDMF 連携
- オブジェクトを移動・複製せず、**アバター本来のワールド位置・姿勢のまま**作業します。
- Modular Avatar 等を使うアバターでは、生のメッシュには衣装の体型追従やメッシュ統合が反映されていません。NDMF のプレビュー機構から**変形適用後のメッシュ**を取得できる場合はそれを作業メッシュとし、取得できない場合は元のメッシュで作業します（情報 `元のメッシュで作業中`）。
- NDMF は任意依存です。NDMF が入っていないプロジェクトでも動作します。

### 2.3 作業メッシュの孤立表示 (Isolation)
作業対象以外の服・装飾・背景が重なって作業の邪魔になるのを防ぎます。

- **開始時**: `SceneVisibilityManager.instance.Isolate(target, true)`。対象とその子階層以外を SceneView 上で非表示にする。
- **終了時**: 本ツールが孤立表示を始めた場合だけ `ExitIsolation()` を呼ぶ。作業開始前からユーザーが孤立表示していた場合はその状態に触れない。
- カプセルの孤立表示ボタンで作業中に ON/OFF できる（`IsolateTargetMesh`）。
- 旧仕様の方式2（一時作業ステージ / PreviewScene）は採用しない。

### 2.4 作業メッシュとポーズ追従
- **MeshRenderer**: `MeshFilter.sharedMesh`（NDMF 適用後があればそれ）を作業メッシュとする。
- **SkinnedMeshRenderer**: 現在のポーズで変形したメッシュを `BakeMesh(mesh, useScale: true)`（スケールを含む Renderer ローカル）で作り、Renderer の `localToWorldMatrix` で配置する。
- 作業メッシュの頂点は Renderer のローカル空間で保持する（ピッキング・ワールド空間ブラシ・オーバーレイ描画が共有）。
- **ポーズの変化**: Renderer とボーンの `localToWorldMatrix` から作る軽いハッシュを SceneView の再描画ごとに比べ、変わっていたら焼き直す（ドラッグ中は 0.2 秒まとめる）。三角形の並びは変わらないので、解析・選択・手塗りはそのまま使える。

### 2.5 対象の識別子 (TargetKey)
作業状態と Target 設定を対象ごとに保存するための識別子です。

- 元の Renderer の `sharedMesh` のアセット GUID と localFileId、Renderer 名から作る（`{Renderer名}_{8桁ハッシュ}`）。同じ FBX の同じメッシュなら、別のシーンや別のアバター複製から開いても同じ作業が復元される。
- メッシュがアセットでない場合（スクリプトで生成された物など）は、Renderer の `GlobalObjectId` から作る。

### 2.6 作業の開始・終了・自動再開
状態は **Idle** と **Working** の 2 つだけです（UI 設計 §3）。

**開始**（タブの `編集 ▶` と作業開始、Hierarchy の右クリックのいずれも同じ処理）
1. 対象の Renderer を確定する。
2. Read/Write を確認する（[mesh_analysis.md](mesh_analysis.md) §2.1）。
3. 作業メッシュを取得する（§2.4）。
4. 解析する（[mesh_analysis.md](mesh_analysis.md)）。
5. 作業状態を復元する（[mask_processing.md](mask_processing.md) §3.5）。
6. 孤立表示、カメラフォーカス（`SceneView.Frame` で対象の bounds へ）、作業用 EditorTool の有効化。

途中で失敗した時は、それまでに行った処理をすべて戻して Idle に戻り、原因を 1 行表示します。

**終了**: 作業状態を保存 → 孤立表示の解除（§2.3）→ オーバーレイの解除 → GPU 資源の解放 → 作業前のツールに戻す。

**自動終了と自動再開**

| きっかけ | 動作 |
| :--- | :--- |
| スクリプトの再コンパイル（ドメインリロード） | 直前に終了し、リロード後に同じ対象で自動的に再開する |
| Play モードに入る | 終了し、Edit モードに戻った時に自動的に再開する |
| シーンを閉じる / Prefab ステージの出入り | 終了（再開しない） |
| 対象が削除された | 終了（再開しない） |
| ツールウィンドウを閉じた | 終了（再開しない） |
| Unity の終了 | 作業状態を保存 |

自動再開は通常の開始と同じ処理で行い、作業状態はディスクから復元します。対象が見つからない時は何も表示せず Idle のままにします。孤立表示や一時オブジェクトをシーンに残しません。

---

## 3. SceneView オーバーレイ描画

### 3.1 マスクの投影表示
- 合成済みマスク D（[mask_processing.md](mask_processing.md) §2）を、作業メッシュの有効三角形に UV でテクスチャとして貼って表示する。**出力解像度の画像をポイントサンプリングで貼る**ので、解像度を下げた時の粗さがモデル上でそのまま見える。
- 色: `SelectedSceneColor`、不透明度 `SceneMaskAlpha`。D = 0 の画素は描かない。
- 両面表示（Cull Off）。
- **深度**: X-Ray OFF は通常の深度テストに、シェーダーの深度バイアス（`Offset -1, -1`）を加えてメッシュ自身とのちらつきを防ぐ。X-Ray ON は深度テスト無し（常に最前面）。
- 旧仕様の「頂点を法線方向に `OverlayDepthOffset` だけ押し出す」方式と、その設定は廃止（メッシュのスケールによって適切な値が変わるため）。

### 3.2 UV シームの描画
- `SeamEdges3D`（[mesh_analysis.md](mesh_analysis.md) §5.2）のうち対象サブメッシュの物を、画面上の太さ `OverlaySeamThickness` px のアンチエイリアス付き線で描く。色は `SeamColor`。
- 深度の扱いは §3.1 と同じ（X-Ray で最前面）。
- 旧仕様の「AA 無効化（`DisableAA`）」設定は廃止（線の描画が常に 1 回の描画命令で済み、速度差が無いため）。

### 3.3 ホバー表示
- 選択モード: カーソル下のグループの外周（[mesh_analysis.md](mesh_analysis.md) §6.5）を 2px の線で縁取る。クリックで追加になる時は白、除外になる時は赤。対称が ON なら対称グループも同時に縁取る。
- ペイントモード: Brush / Eraser は、交点を中心に表面の向きに沿った円（半径 $r$、[mask_processing.md](mask_processing.md) §6.4）を描く。メッシュから外れている時は画面上の円。Rect / Lasso はドラッグ中の図形を画面上に描く。

### 3.4 UV ビューの描画
UV ビュー（ツールタブ）は SceneView と同じ D を表示し、その上に有効三角形（対象サブメッシュ）の辺（薄い線）と `BorderEdges`、ホバーの縁取り、ブラシの円を描きます。

### 3.5 対象サブメッシュでの絞り込み
サブメッシュが複数あるメッシュでは、シーンと UV ビューのどちらも、対象サブメッシュ（選んでいるマテリアルが割り当てられた三角形）の物だけを描きます。マスクは有効三角形だけで作り（§3.1）、シーム・境界・ワイヤーは辺ごとに持つ三角形番号で絞ります。対象を切り替えると線を作り直します（再解析はしない）。全体（-1）を対象にしている時は、別のサブメッシュと重なる同じ辺を 1 回だけ描きます。`PreviewOverlayBaseTex` が ON の時はベーステクスチャを不透明度 `PreviewOverlayAlpha` で下に敷きます。

---

## 4. 設定項目と保存階層

### 4.1 階層と解決順
値は上の層から順に探し、最初に見つかった値を使います。各層は「その層で変えた値」だけを持ちます。

$$\text{Session} \;>\; \text{Target} \;>\; \text{Project} \;>\; \text{User} \;>\; \text{組み込み既定}$$

各設定には**書き込み先の層**が 1 つ決まっています。UI で値を変えるとその層に書き、書き込み先の層の値が下の層と異なる時は上書きマーク ● を出します（クリックで書き込み先の値を消して下の層の値に戻す）。

### 4.2 保存先

| 層 | 保存先 | 寿命・共有範囲 |
| :--- | :--- | :--- |
| **Session** | メモリ（ドメインリロードをまたいで保持） | 作業開始時に空にする。作業中のみ |
| **Target** | `UserSettings/UVMaskMaker/targets/{TargetKey}/settings.json` | 個人。同じ対象を開き直すと復元 |
| （作業状態） | `UserSettings/UVMaskMaker/targets/{TargetKey}/session.bin` | 個人（[mask_processing.md](mask_processing.md) §3.5） |
| **Project** | `ProjectSettings/UVMaskMaker.json` | プロジェクト単位。バージョン管理でチーム共有できる |
| **User** | `EditorPrefs`（キー `UVMaskMaker.User`、JSON） | 全 Unity プロジェクト共通 |

### 4.3 設定一覧

「書込」は書き込み先の層です。UI 上の置き場所（ランク）は UI 設計 §2.5.3 / §7 を参照。

| キー | 型 | 既定 | 書込 | 備考 |
| :--- | :--- | :--- | :-: | :--- |
| `IsPaintMode` | bool | false | Session | 作業開始時は常に選択モード |
| `Granularity` | enum | UVIsland | User | 前回の値を覚える |
| `PaintSubMode` | enum | Brush | Session | |
| `BrushSize` | int | 30 | User | 画面上の半径 px、1〜300 |
| `SymmetryEnabled` | bool | 自動 | Session | 開始時、`MirrorMatchRate ≥ 0.9` なら true |
| `ScenePickPaused` | bool | false | Session | |
| `OverlayOnTop`（X-Ray） | bool | false | Session | 初期値は User 層の値（Preferences で変更） |
| `IsolateTargetMesh` | bool | true | Session | 初期値は User 層の値 |
| `UVChannel` | int | 0 | Target | UV が複数ある時だけ表示 |
| `TargetSubmesh` | int | 0 | Target | -1 は全体。サブメッシュが複数ある時だけ表示 |
| `FileName` | string | `{Renderer名}_mask` | Target | |
| `TextureSize` | int | 自動 | Target | ベーステクスチャのサイズ、無ければ `DefaultTextureSize` |
| `ChannelWriteEnabled` | bool | false | Target | |
| `WriteR` / `WriteG` / `WriteB` / `WriteA` | bool | true / false / false / false | Target | |
| `BasePng` | asset GUID | なし | Target | 候補はマテリアルから提示 |
| `PreviewOverlayBaseTex` | bool | false | Target | UV ビューのベース重ね |
| `PreviewOverlayAlpha` | float | 0.6 | Target | ベース重ねの不透明度 |
| `UVLayoutSize` | int | 2048 | Target | [export.md](export.md) §4.1 |
| `UVLayoutWireframe` / `UVLayoutSeams` | bool | true | Target | |
| `UVLayoutSelectionOnly` / `UVLayoutColorFill` | bool | false | Target | |
| `UVLayoutFillColor` | enum | Islands | Target | [export.md](export.md) §4.1 |
| `UVLayoutBackground` | enum | Transparent | Target | |
| `UVLayoutLineColor` | enum | White | Target | |
| `MaterialSlot` | string | なし | Target | [export.md](export.md) §5.2 |
| `LastOutputPath` / `LastOutputInvPath` / `LastUVLayoutPath` | string | なし | Target | 上書き判定用（UI には出さない） |
| `InvertMask` | bool | false | Target | 下の層（Project）が既定 |
| `PixelMargin` | int | 自動 | Target | 自動 = `TextureSize / 256`。下の層（Project）が既定 |
| `SaveInvertedToo` | bool | false | Target | 下の層（Project）が既定 |
| `DefaultTextureSize` | int | 512 | Project | ベーステクスチャが無い時の解像度 |
| `UseTextureFolder` | bool | true | Project | |
| `OutputDir` | string | `Assets/GeneratedMasks` | Project | |
| `AutoApplyMaterial` | bool | false | Project | |
| `BlackThreshold` | int | 128 | Project | 取り込みのしきい値 |
| `MirrorTolerance` | float | 自動 | Project | 自動 = メッシュ最大辺長 × 0.002 |
| `ImportAsSingleChannel` | bool | false | Project | [export.md](export.md) §2.2 |
| `SelectedSceneColor` | Color | (0, 1, 1, 1) | User | |
| `SceneMaskAlpha` | float | 0.6 | User | シーンのマスク表示の不透明度 |
| `SeamColor` | Color | (1, 0.15, 0.15, 1) | User | |
| `OverlaySeamThickness` | float | 2.5 | User | px |
| `Language` | enum | Auto | User | Auto = Editor の言語。ja / en |
| `HintShownCount.*` | int | 0 | User | 迷い検知ヒントの表示回数 |

`InvertMask` / `PixelMargin` / `SaveInvertedToo` は Project 層にも値を持てます（Project Settings で変更）。Target 層に値が無ければ Project 層の値を使います。

**廃止した設定**: `AddMode`（スマートトグルで不要）、`ModeToggleHotkey`（Shortcut Manager に移行）、`OverlayDepthOffset`（§3.1）、`DisableAA`（§3.2）、`UseEnglish`（`Language` に統合）。

ショートカットは設定項目に含めず、Unity の Shortcut Manager に登録します（UI 設計 §2.6.7）。
