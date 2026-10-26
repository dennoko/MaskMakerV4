# UV Mask Maker 機能仕様書 - 画像エクスポート仕様 (Export Specification)

## 1. 概要

本モジュール（`PngExporter` および `UVMaskExport`）は、合成されたマスク情報を画像ファイル（PNG）としてUnityプロジェクト内のストレージに出力する機能を規定します。

> [!NOTE]
> 過去バージョンに存在した「頂点カラーへのベイク機能」は廃止され、テクスチャマスクのPNGエクスポートに特化しています。

---

## 2. PNG画像エクスポート仕様

### 2.1 出力モード

#### A. 通常マスク出力 (Classic Mode)
- **色仕様**:
  - 選択・ペイント領域: **黒** `(R:0, G:0, B:0, A:255)`
  - 非選択領域: **白** `(R:255, G:255, B:255, A:255)`
  - （※`InvertMask` 有効時は黒白が反転）

#### B. チャンネル別書き込み (Channel Write Mode)
既存のテクスチャ（Base PNG）または白背景に対して、指定された特定チャンネル（R / G / B / A）のみにマスク値を書き込みます。

| 状態 | 選択ピクセルの挙動 | 非選択ピクセルの挙動 |
| :--- | :--- | :--- |
| **Base PNG あり** | 有効化されたチャンネル（R/G/B）に `0` を書き込み。Aチャンネル指定時は `255` を書き込み。 | Base PNG の既存ピクセル値をそのまま維持。 |
| **Base PNG なし** | 有効化されたチャンネルに `0` を書き込み。 | 有効化されたチャンネルに `255` を書き込み。 |

### 2.2 出力テクスチャのインポーター設定自動化 (TextureImporter Automation)

マスクテクスチャはシェーダー等でデータテクスチャとして扱われることが前提となるため、**sRGBを無効化（Linear）**し、テクスチャストリーミング・最適化のために**Mipmapを有効化**します。

PNG出力完了後、以下のインポート設定を自動適用して `SaveAndReimport()` を実行します：
```csharp
var importer = AssetImporter.GetAtPath(path) as TextureImporter;
if (importer != null)
{
    importer.sRGBTexture = false;       // デフォルトで無効 (Linearデータとして扱う)
    importer.mipmapEnabled = true;      // Mipmapを有効化
    importer.streamingMipmaps = true;   // Streaming Mipmapsを有効化
    importer.isReadable = true;         // スクリプトからの再読み込みを許可
    importer.textureCompression = TextureImporterCompression.Uncompressed; // 画質劣化防止
    importer.SaveAndReimport();
}
```

---

## 3. 保存先パス解決と同時出力の競合回避

### 3.1 保存先フォルダの解決
1. **メインテクスチャ連動 (`UseTextureFolder = true`)**:
   - ターゲットモデルのマテリアルからベーステクスチャ（`_BaseMap` または `_MainTex`）のアセットパスを取得し、その同一ディレクトリを出力先とします。
2. **通常フォルダ指定 (`UseTextureFolder = false`)**:
   - 設定された `OutputDir`（デフォルト: `Assets/GeneratedMasks`）を使用。フォルダが存在しない場合は階層を自動作成（`EnsureAssetFolderPath`）。

### 3.2 元マスク・反転マスクの同時出力時のファイル名競合回避アルゴリズム
`SaveInvertedToo = true`（反転マスクも同時に保存）が指定されている場合、単にUnity標準の `GenerateUniqueAssetPath` を個別に呼ぶと、元マスクと反転マスクで番号がずれたり、既存の `_inv.png` を上書きしてしまうリスクがあります。

そのため、**元マスクと反転マスクの双方が同時に競合しなくなるまでインデックス番号をインクリメント**して決定します。

#### アルゴリズム
```csharp
public static (string mainPath, string invPath) ResolveUniqueMaskPaths(
    string folder, string baseName, bool saveInvertedToo)
{
    int counter = 0;
    while (true)
    {
        string suffix = (counter == 0) ? "" : $" {counter}";
        string mainCandidate = $"{folder}/{baseName}{suffix}.png";
        
        if (!saveInvertedToo)
        {
            if (!AssetExists(mainCandidate))
                return (mainCandidate, null);
        }
        else
        {
            string invCandidate = $"{folder}/{baseName}{suffix}_inv.png";
            // 元マスクと反転マスクの両方が存在しない場合のみ確定
            if (!AssetExists(mainCandidate) && !AssetExists(invCandidate))
            {
                return (mainCandidate, invCandidate);
            }
        }
        counter++;
    }
}

private static bool AssetExists(string path)
{
    return File.Exists(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;
}
```

この処理により、例えば `uv_mask.png` と `uv_mask_inv.png` が常に同じサフィックス番号のペアとして安全に出力されます。

---

## 4. UV展開図エクスポート仕様 (UV Layout Export)

外部ペイントソフト（CLIP STUDIO PAINT, Photoshop, Substance 3D 等）でテクスチャを描く際の下絵・ガイド用として、メッシュのUVワイヤーフレーム画像をPNG出力する機能です。

### 4.1 パラメータ仕様

| パラメータ | 型 / 選択肢 | 既定値 | 説明 |
| :--- | :--- | :--- | :--- |
| `UVLayoutSize` | `int` (512, 1024, 2048, 4096) | 2048 | 出力解像度（マスク解像度とは独立） |
| `DrawWireframe` | `bool` | `true` | 全三角形（ポリゴン）のエッジ線を描画 |
| `DrawSeams` | `bool` | `true` | UVアイランドの外形境界線を強調色（赤系）で太線描画 |
| `SelectionOnly` | `bool` | `false` | 現在選択されているアイランド・パーツのみを描画 |
| `ColorFillIslands` | `bool` | `false` | 各UVアイランドを半透明の識別カラーで塗り分け（パーツ判別用） |
| `BackgroundMode` | `enum` (Transparent, BaseTexture) | `Transparent` | 透過背景、またはモデルのベーステクスチャ重ね |
| `LineColor` | `enum` (White, Black, Cyan) | `White` | ワイヤーフレーム線の色（白: `#FFFFFF`, 黒: `#000000`, シアン: `#00E5FF`） |

### 4.2 レンダリングパイプライン

1. **バッファ初期化**:
   - `BackgroundMode == Transparent`: 全ピクセル完全透明 `(0, 0, 0, 0)` の `Texture2D`（RGBA32）を用意。
   - `BackgroundMode == BaseTexture`: モデルのベーステクスチャを解像度 $W \times H$ へ `Graphics.Blit` で取り込み。
2. **アイランド色分け充填 (ColorFillIslands == true)**:
   - 各アイランドごとに、インデックスのハッシュ値から生成したパステルカラー（Alpha: 0.35）を算出。
   - アイランド内の三角形をスキャンライン/Edge関数でラスタライズ充填。
3. **ワイヤーフレーム描画 (DrawWireframe == true)**:
   - 対象三角形の各UV辺（$(u_0, v_0) \to (u_1, v_1) \to (u_2, v_2) \to (u_0, v_0)$）をピクセル座標にマッピング。
   - Bresenhamアルゴリズムで `LineColor` のピクセルを描画（線幅 1px）。
4. **シーム境界線描画 (DrawSeams == true)**:
   - `UVAnalysis.SeamEdges2D` の各エッジを描画（線幅 2px、`SeamColor` 赤系 `#FF3333`）。
5. **ファイル出力とインポート設定**:
   - パス: `{folder}/{FileName}_uv.png`（競合時は連番で解決）。
   - PNGバイト配列を書き出し後、テクスチャインポーターを設定：
     - `sRGBTexture = true`（カラー画像として表示するためsRGB有効）
     - `textureCompression = TextureImporterCompression.Uncompressed`
     - `alphaIsTransparency = true`

