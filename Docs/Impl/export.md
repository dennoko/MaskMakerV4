# UV Mask Maker 機能仕様書 - 画像エクスポート仕様 (Export Specification)

## 1. 概要

本モジュールは、合成済みマスク D（[mask_processing.md](mask_processing.md) §2）を PNG としてプロジェクト内に書き出す処理、UV 展開図の書き出し、および出力したマスクのマテリアルへの適用を規定します。

> [!NOTE]
> 過去バージョンに存在した「頂点カラーへのベイク機能」は廃止され、テクスチャマスクのPNGエクスポートに特化しています。

---

## 2. マスク PNG 出力

### 2.1 出力モード

`InvertMask` が ON の時は、以下の「選択」と「非選択」を入れ替えて扱います（D の代わりに ¬D を選択とみなす）。

#### A. 通常マスク出力 (Classic Mode)
- 選択画素: **黒** `(R:0, G:0, B:0, A:255)`
- 非選択画素: **白** `(R:255, G:255, B:255, A:255)`

#### B. チャンネル別書き込み (Channel Write Mode)
既存のテクスチャ（Base PNG）または白背景に対して、有効にしたチャンネル（R / G / B / A）にだけマスク値を書き込みます。

| 状態 | 選択画素 | 非選択画素 |
| :--- | :--- | :--- |
| **Base PNG あり** | 有効なチャンネル（R/G/B）に `0` を書く。A を有効にした時は A に `255` を書く。 | Base PNG の値をそのまま残す。 |
| **Base PNG なし** | 有効なチャンネルに `0` を書く。 | 有効なチャンネルに `255` を書く。 |

- **Base PNG の読み込み**: テクスチャアセットからではなく、**元ファイルのバイト列**を `ImageConversion.LoadImage` で読む。インポート時の圧縮・sRGB 変換・最大サイズの縮小が元画像に書き戻されるのを防ぎ、書き込まないチャンネルと非選択画素はビット単位で元のまま残る。
- **出力サイズ**: Base PNG がある時は、出力解像度を Base PNG のサイズに固定する（正方形でなくてもよい）。マスクの合成もそのサイズで行うので、プレビューと出力は一致する（UI では解像度ボタンを無効にしてサイズを表示）。

### 2.2 インポーター設定

マスクはシェーダーでデータとして読まれるため、sRGB を無効にします。設定は書き出し後に `SaveAndReimport` で当て直すのではなく、`AssetPostprocessor.OnPreprocessTexture` で**最初のインポート時に**当てます。

- 旧仕様の「書き出し直後に `AssetImporter.GetAtPath` で設定する」方式は、新規ファイルがまだインポートされておらず importer が null になるため、初回だけ sRGB ON・圧縮ありで取り込まれていた。また 2 回インポートが走っていた。
- 本ツールが書き出したパスを登録しておき、そのパスが**初めてインポートされる時**（`assetImporter.importSettingsMissing == true`、つまり .meta がまだ無い時）だけ設定を当てる。再出力（上書き）では当て直さないので、ユーザーが後から変えたインポート設定は保たれる。

| 項目 | 値 |
| :--- | :--- |
| `sRGBTexture` | `false`（リニアデータ） |
| `mipmapEnabled` | `true` |
| `streamingMipmaps` | `true` |
| `isReadable` | `false`（本ツールは読み戻す時もファイルから読むので不要。ビルドに CPU 側のコピーを残さない） |
| `textureCompression` | `Uncompressed` |
| `textureType` | `Default`。Project 設定 `ImportAsSingleChannel` が ON の時は `SingleChannel`（Red）にしてメモリを 1/4 にする（シェーダーが .r 以外を読む場合は使えないので既定 OFF） |

### 2.3 空の出力
選択も手塗りも空の時も出力は止めず、警告 `選択なし（全白で出力）` を出します。

---

## 3. 保存先パスと上書き・連番

### 3.1 保存先フォルダの解決
1. **テクスチャと同じ場所 (`UseTextureFolder = true`、既定)**: 対象マテリアルのベーステクスチャ（`_BaseMap` → `_MainTex` の順）のアセットのフォルダ。ベーステクスチャが無い、またはフォルダが `Packages/` 以下（パッケージ更新で消える）の時は 2 にフォールバック。
2. **フォルダ指定 (`UseTextureFolder = false`)**: `OutputDir`（既定 `Assets/GeneratedMasks`）。無ければ階層ごと作成。

### 3.2 上書きと連番
調整 → 再出力のくり返しでファイルが増えず、マテリアルの割り当て直しも要らないように、**同じ対象からの再出力は前回のファイルを上書き**します。

1. 対象（Target）ごとに、前回出力したパス（`LastOutputPath`、反転同時保存時は `LastOutputInvPath`）を記録する。
2. 今回のパス候補 `{folder}/{FileName}.png` が記録と同じなら、そのまま上書きする。
3. 記録と違い、かつ既存ファイルと衝突する時（ファイル名を変えた、他のツールのファイルがある）だけ、次の規則で連番を付ける。
4. 確定したパスを記録し直す。

#### 元マスクと反転マスクの連番
`SaveInvertedToo = true` の時、`GenerateUniqueAssetPath` を個別に呼ぶと元と反転で番号がずれるため、**両方が同時に空いている番号**を探します。

```csharp
public static (string mainPath, string invPath) ResolveUniqueMaskPaths(
    string folder, string baseName, bool saveInvertedToo)
{
    for (int counter = 0; ; counter++)
    {
        string suffix = counter == 0 ? "" : $" {counter}";
        string main = $"{folder}/{baseName}{suffix}.png";
        string inv = saveInvertedToo ? $"{folder}/{baseName}{suffix}_inv.png" : null;
        if (!File.Exists(main) && (inv == null || !File.Exists(inv)))
            return (main, inv);
    }
}
```

これにより、`uv_mask.png` と `uv_mask_inv.png` は常に同じ番号のペアになります。

---

## 4. UV展開図エクスポート (UV Layout Export)

外部ペイントソフト（CLIP STUDIO PAINT, Photoshop, Substance 3D 等）でテクスチャを描く際の下絵として、UV のワイヤーフレーム画像を出力します。

### 4.1 パラメータ

| パラメータ | 型 / 選択肢 | 既定値 | 説明 |
| :--- | :--- | :--- | :--- |
| `UVLayoutSize` | `int` (512, 1024, 2048, 4096) | 2048 | 出力解像度（マスク解像度とは独立） |
| `UVLayoutWireframe` | `bool` | `true` | 全三角形の辺を描く |
| `UVLayoutSeams` | `bool` | `true` | UV 境界（`BorderEdges`）を強調色で太く描く |
| `UVLayoutSelectionOnly` | `bool` | `false` | 選択済み三角形だけを描く |
| `UVLayoutColorFill` | `bool` | `false` | 島ごとに半透明の識別色で塗り分ける |
| `UVLayoutBackground` | `enum` (Transparent, BaseTexture) | `Transparent` | 透過背景、またはベーステクスチャ |
| `UVLayoutLineColor` | `enum` (White, Black, Cyan) | `White` | 線の色（白 `#FFFFFF` / 黒 `#000000` / シアン `#00E5FF`） |

描く対象は有効三角形（対象サブメッシュ）だけです。

### 4.2 描画手順
1. **背景**: Transparent は全画素 `(0,0,0,0)`。BaseTexture はベーステクスチャの元ファイルを `LoadImage` で読み、`UVLayoutSize` の正方形に引き伸ばして敷く。
2. **島の塗り分け**（`UVLayoutColorFill`）: 島 $g$ の色相を $h_g = \text{frac}(g \times 0.618034)$（黄金比で隣の番号ほど色相が離れる）、彩度 0.45、明度 1.0、不透明度 0.35 とし、[mask_processing.md](mask_processing.md) §4 と同じ規則でラスタライズして重ねる。
3. **ワイヤーフレーム**（`UVLayoutWireframe`）: 各三角形の 3 辺を線幅 1px のアンチエイリアス付き線で描く。共有辺は 1 回だけ描く。
4. **UV 境界**（`UVLayoutSeams`）: `BorderEdges`（[mesh_analysis.md](mesh_analysis.md) §5.2）を線幅 2px、`#FF3333` で描く。
5. **書き出し**: `{folder}/{FileName}_uv.png`。上書き・連番の規則は §3.2 と同じ（`LastUVLayoutPath` を記録）。インポーター設定は §2.2 と同じ仕組みで、初回インポート時に `sRGBTexture = true`、`alphaIsTransparency = true`、`mipmapEnabled = false`、`textureCompression = Uncompressed` を当てる。

---

## 5. マテリアル適用 (Smart Apply)

出力したマスクを、対象のマテリアルのテクスチャスロットに割り当てます。

### 5.1 適用先
- **マテリアル**: `TargetSubmesh` のマテリアル（全体を対象にしている時は先頭のマテリアル）。FBX に内蔵されたマテリアルなど編集できないものには適用しない（[適用] を出さない）。
- **候補スロット**: シェーダーのテクスチャプロパティのうち、名前または表示名に `Mask`（大文字小文字を区別しない）を含み、`HideInInspector` でないもの。例: lilToon の `_MainColor2ndBlendMask`、Poiyomi の `_DecalMask`。
- 候補が無いシェーダーでは [適用] を出さない。

### 5.2 スロットの決め方
1. Target に記憶したスロット（`MaterialSlot`）があり、そのシェーダーに存在すれば、それを使う（1 クリック）。
2. 無ければ、候補が 1 つなら即適用。複数なら候補リストを出し、選んだ時点で適用して `MaterialSlot` に記憶する。
3. Project 設定 `AutoApplyMaterial` が ON で、記憶したスロットがある時は、出力の直後に自動で適用する。

### 5.3 適用処理
- マテリアルの変更は統合履歴に 1 件として積む（[mask_processing.md](mask_processing.md) §3.2）。直後の Ctrl+Z で割り当て前に戻る。
- スロットに既に同じテクスチャが入っている時（上書きの再出力）は何もしない。トーストは `更新: Body_mask.png` だけにする。
- マテリアルはアセットなので、同じマテリアルを使う他のオブジェクトにも反映される。
