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
