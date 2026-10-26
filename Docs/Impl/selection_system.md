# UV Mask Maker 機能仕様書 - 選択システムとピッキング (Selection System & Picking)

## 1. 概要

本モジュールは、解析されたトポロジー情報に基づき、ユーザーによるグループの選択状態（`selectedGroups`）の管理、集合演算、粒度間での選択状態引き継ぎ変換、およびSceneViewおよび2Dプレビューからのピッキング処理を規定します。

---

## 2. 選択状態のデータモデル

現在の粒度における選択状態は、整数インデックスのハッシュセット（`HashSet<int>`）として保持されます。各インデックスは、現在の粒度の `SelectionGroups.Groups` の要素インデックスに対応します。

```csharp
HashSet<int> selectedGroups; // 現在選択中のグループインデックス集合
```

---

## 3. 選択集合演算仕様

各操作の実行前には、[mask_processing.md](file:///c:/Users/dennn/Programming/UnityExtension/UVMaskMaker/Docs/Impl/mask_processing.md) で規定する統合編集履歴（Unified History）の Undo スタックに現在のスナップショットが退避されます。

| 操作 | 動作仕様 |
| :--- | :--- |
| **追加 (Add)** | 指定されたグループインデックスを `selectedGroups` に追加 (`Add`)。 |
| **除外 (Remove)** | 指定されたグループインデックスを `selectedGroups` から削除 (`Remove`)。 |
| **トグル (Toggle)** | グループが既に対象に含まれていれば除外、含まれていなければ追加。 |
| **反転 (Invert)** | 現在の粒度の全グループ $0 \le i < \text{Count}$ のうち、選択されていないインデックスのみで構成される新しい `HashSet<int>` に置き換え。 |
| **全選択 (Select All)** | $0 \le i < \text{Count}$ のすべてのインデックスを含む `HashSet<int>` に置き換え。 |
| **統合クリア (Clear All)** | `selectedGroups.Clear()` を実行すると同時に、手塗りレイヤーも全消去（統合クリア）。 |

---

## 4. 粒度間変換アルゴリズム (ConvertSelection)

作業中に選択単位（粒度）を切り替えた場合、選択状態は以下の**「全包含（All-or-Nothing）ルール」**に従って変換されます。

### 4.1 変換規則
> **ルール**:  
> 変換先（`to`）のグループが選択状態となるのは、**そのグループを構成するすべての三角形（`triIndex`）が変換元（`from`）の選択セットに完全に含まれている場合のみ**とする。

### 4.2 アルゴリズム
1. 変換元（`from`）の選択グループに含まれるすべての三角形インデックスを集合 `selectedTris`（`HashSet<int>`）に展開。
2. 変換先（`to`）の各グループ $g$ についてループ処理：
   - グループ $g$ に属する全三角形が `selectedTris` に含まれているか検査。
   - すべて含まれている場合のみ、変換後の選択セットに $g$ を追加。

### 4.3 挙動の具体例
- **粗い粒度 $\rightarrow$ 細かい粒度**（例: `UVIsland` $\rightarrow$ `Polygon`）:  
  選択されていたアイランドの全ポリゴンが選択状態として100%保持されます。
- **細かい粒度 $\rightarrow$ 粗い粒度**（例: `Polygon` $\rightarrow$ `UVIsland`）:  
  アイランド内のポリゴンが1つでも欠けている場合、そのアイランドは選択解除されます。完全にすべてのポリゴンが選ばれていたアイランドのみが選択を維持します。

---

## 5. 3Dピッキング仕様 (PickingService)

SceneView上の3Dモデル表面をクリックした際に対象グループを特定する機構です。

### 5.1 一時コライダーの生成とライフサイクル
- **一時オブジェクト**: `__UVMaskPickerCollider__` という名前の `GameObject` を生成し、`HideFlags.HideAndDontSave` を付与してヒエラルキーやシーン保存から不可視化します。
- **配置**: ターゲットオブジェクトのワールド座標・回転・スケールに完全同期させます。
- **コライダー**: `MeshCollider` コンポーネントを付与し、`convex = false`（凹形状対応）とします。
- **破棄**: ターゲット変更時、ツール終了時、および破棄時には即座に `DestroyImmediate` で後始末を行います。

### 5.2 SkinnedMeshRenderer (ボーン変形) への対応
対象が `SkinnedMeshRenderer` の場合、バインドポーズのメッシュではなく、現在のポーズで変形されたメッシュ（`smr.BakeMesh` または NDMFプレビュー適用後のメッシュ）を `MeshCollider.sharedMesh` に割り当てます。これにより、アニメーションやポーズ変更後のメッシュ表面に対しても正確にRaycastがヒットします。

### 5.3 レイキャストとグループ逆引き
1. SceneViewのマウス位置から `HandleUtility.GUIPointToWorldRay(guiPos)` でワールド空間のレイを生成。
2. `Physics.Raycast` を実行し、自身の一時 `MeshCollider` とヒットするか判定。
3. ヒット情報の `RaycastHit.triangleIndex` を取得。
4. 現在の粒度の `TriangleToGroup` 辞書を引いて、該当するグループインデックスを取得。
5. 選択モード（Add / Remove）に応じて選択セットを更新。

### 5.4 シーン選択の一時停止機能 (Pause Scene Picking)
SceneView上のクリックイベントをツールが消費（`Event.current.Use()`）すると、Unity標準のギズモ操作や他オブジェクトの選択が行えなくなります。
- **一時停止フラグ (`ScenePickPaused = true`)**:
  - レイキャスト判定およびクリックイベントの消費をスキップ。
  - シーンビュー描画（オーバーレイやシーム線）は維持したまま、ユーザーはUnity標準のTransform操作やシーン移動を自由に行えるようにします。

---

## 6. 2D UV空間でのピッキング仕様

UVプレビュー領域をクリックした際に対象グループを特定する機構です。

### 6.1 ラベルマップ (Label Map)
解像度 $W \times H$ の整数配列 `int[] labelMap` を用意します（初期値 `-1`）。
- 解析結果の各グループについて、属する三角形をUV空間上にラスタライズし、該当ピクセルにグループインデックスを書き込みます。
- マウス位置のUV座標 $(u, v)$ をテクスチャピクセル座標 $(x, y)$ に変換し、`labelMap[y * W + x]` を参照することで $O(1)$ でグループを即座に特定します。
- 粒度や解析結果が変化した際はキャッシュを破棄（Invalidate）して再生成します。
