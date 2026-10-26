# UV Mask Maker 機能仕様書 - メッシュ・UVトポロジー解析 (Mesh Analysis)

## 1. 概要

本モジュール（`UVAnalyzer`）は、入力されたメッシュの頂点座標および指定されたUVチャンネルの座標を解析し、以下のトポロジーデータを生成します。

1. **量子化による正準頂点ID（Canonical Vertex IDs）の生成**
2. **サブメッシュ単位での三角形の抽出**
3. **境界・UVシームエッジ（BorderEdges / SeamEdges3D）の抽出**
4. **3種類の選択粒度に応じたグループ分割（UVIsland / ConnectedMesh / Polygon）**

---

## 2. 前提要件と前処理

### 2.1 メッシュの Read/Write 有効化
メッシュデータの読み込み（`mesh.vertices`, `mesh.triangles`, `mesh.GetUVs`）を行うため、メッシュの `isReadable` が `true` である必要があります。
- **自動修復仕様**: `mesh.isReadable` が `false` の場合、アセットパス（`AssetDatabase.GetAssetPath`）から `ModelImporter` を取得し、`importer.isReadable = true` を設定して `SaveAndReimport()` を実行します。

### 2.2 UVチャンネルの検証とフォールバック
- 指定されたUVチャンネル（0〜7）を取得します。
- メッシュに対象チャンネルが存在しない（要素数が0）場合、即座に例外をスローするか、自動的に `UV0` にフォールバックして再解析を試みます。

---

## 3. 座標の量子化（Quantization）

Unityのインポート処理では、ハードエッジ（法線の不連続）やUVシームなどの理由で、3D空間上同一位置にある頂点であっても異なるインデックスに複製されます。単純な頂点インデックスベースで接続判定を行うと、法線が分かれているだけのポリゴンが別グループに切断されてしまいます。

これを解決するため、座標を一定の精度で量子化して正準ID（Canonical ID）を発行します。

### 3.1 量子化キーの算出
- **スケール係数**: `100,000`（有効精度: $1 \times 10^{-5}$）
```csharp
Vector3Int PosKey(Vector3 p) => new Vector3Int(
    Mathf.RoundToInt(p.x * 100000f),
    Mathf.RoundToInt(p.y * 100000f),
    Mathf.RoundToInt(p.z * 100000f)
);

Vector2Int UVKey(Vector2 uv) => new Vector2Int(
    Mathf.RoundToInt(uv.x * 100000f),
    Mathf.RoundToInt(uv.y * 100000f)
);
```

### 3.2 頂点IDマッピング (VertexIds)
全頂点に対して、以下の2系統のIDテーブルを作成します。
1. `Pos[vertexIndex] -> int`: 3D位置のみで同一視したID（位置が同じ頂点は同一ID）。
2. `PosUV[vertexIndex] -> int`: 3D位置およびUV座標の両方が一致したID。

---

## 4. エッジキーの定義

無向エッジを一意に識別するため、2つの正準ID $(a, b)$ から64ビット整数キーを生成します。
$$EdgeKey(a, b) = \begin{cases} (a \ll 32) \mid b & (a < b) \\ (b \ll 32) \mid a & (a \ge b) \end{cases}$$

---

## 5. 境界・UVシームエッジの抽出アルゴリズム

メッシュ全体の各三角形の3辺について、`(3D位置EdgeKey, サブメッシュIndex)` をキーとしてエッジリストを辞書に収集します。各エッジ要素には `(PosUVEdgeKey, 頂点a, 頂点b)` を格納します。

### 5.1 境界判定ロジック
収集したエッジグループについて、以下の条件を満たす場合に「境界（Border / Seam）」と判定します。
1. **オープン境界（Open Boundary）**: そのエッジを参照する三角形が1つしかない場合（メッシュの切れ端）。
2. **UVシーム（UV Seam）**: エッジを参照する三角形群の中で、`PosUVEdgeKey`（位置＋UVの組み合わせ）が複数種類存在する場合（3D上は繋がっているがUV上で裂けている）。

> [!NOTE]
> 法線のみが分かれているハードエッジ（位置・UVは一致）の場合、`PosUVEdgeKey` は1種類であるためシームとは判定されません。

### 5.2 出力エッジデータ
- **`BorderEdges` (List<UVBorderEdge>)**: UVプレビュー描画用。UV空間でシームの両側の辺を描画できるよう、ユニークな `PosUVEdgeKey` ごとに1本ずつ出力します。
- **`SeamEdges3D` (List<(int v0, int v1)>)**: SceneViewワイヤー描画用。サブメッシュやUV面に関わらず、3D空間上の位置エッジを1回だけ出力します（重複排除）。

---

## 6. 3種類の選択粒度とグルーピングアルゴリズム

解析時、すべての三角形は Union-Find アルゴリズムによって互いに素なグループに分割されます。

### 6.1 UVIsland（UVアイランド）
- **定義**: 同一サブメッシュ内で、**「位置＋UV端点」を共有するエッジ**で隣接する三角形群。
- **結合条件**:
  - 2つの三角形が共通のエッジを持ち、そのエッジの2頂点の `PosUV` が両方の三角形で一致している場合、Union-Find で結合。
- **特性**: ハードエッジ（法線スプリット）があっても島は分断されず、UVシームまたはサブメッシュ境界でのみ分割されます。

### 6.2 ConnectedMesh（接続メッシュ）
- **定義**: 同一サブメッシュ内で、**「位置」を共有する頂点**で接続されている三角形群。
- **結合条件**:
  - 2つの三角形が同一の `Pos` ID を持つ頂点を1つでも共有していれば結合（点接触でも同一グループ）。
- **特性**: UVシームを跨いでメッシュ全体の連結成分を抽出します。

### 6.3 Polygon（ポリゴン）
- **定義**: 個々の三角形。
- **結合条件**: なし。三角形のインデックス $0, 1, \dots, N-1$ がそれぞれ単独のグループになります。

### 6.4 グループIDの正規化 (Compact)
Union-Find の根（Root）番号を、出現順に $0, 1, \dots, K-1$ の連続した整数インデックスに再採番します。これにより、外部からは配列インデックスとして高速に参照可能になります。
- `Groups[groupIndex]`: そのグループに属する `UVTriangle` のリスト
- `TriangleToGroup[triIndex]`: 各三角形インデックスから所属グループIndexを引く辞書
