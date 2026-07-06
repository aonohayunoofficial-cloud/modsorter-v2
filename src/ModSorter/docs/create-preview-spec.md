# Create機械プレビュー ＋ ブロック寸法一般化 仕様書

## 目的

Create機械モードの生成結果に3Dボクセルプレビューを付ける。あわせてプレビュー描画を「ブロックごとに正しい見た目寸法で描く」方式へ一般化し、大型歯車のはみ出しとシャフト等の痩せを解消する。副次的に、普通建築で放置されている形状ブロック（フェンス・ハーフ・階段・壁）をプレビューで正しく描けるようにし、現役化への道を開く。

## 決定事項

### 1. プレビューは1系統に統一
既存の `PreviewHtml` と `PreviewWindow` を拡張し、普通建築とCreate機械の両方が同じプレビューを使う。別系統は作らない。

### 2. ブロック寸法テーブルはC#側に持つ
ブロックIDごとに以下を定義するテーブルをC#側に持つ。

- 見た目寸法 `(sx, sy, sz)`
- オフセット `(ox, oy, oz)`
- 向き依存か否か

`ConnectionCatalog` と同じくC#に知識を集約する。テーブルに未登録のIDは **寸法1×1×1・オフセット0・向き非依存** として扱う。これにより既存の普通建築の見た目は一切変わらない（完全な後方互換）。

### 3. 向きの解決はC#が行う
既存の `ConnectionCatalog.GetRotationAxis()` を使って軸 (`x` / `y` / `z`) を解決し、プレビューへ渡すブロックデータに解決済みの軸フィールドを付与する。JS側は `facing → 軸` の変換ロジックを持たず、渡された軸をそのまま使う。

### 4. プレビューへ渡すデータ形式
`{ x, y, z, id, axis? }` とする。

- `axis` は任意フィールド。向き非依存または未解決なら省略。
- 普通建築は従来通り `axis` 無しで渡す（`GeneratedBlock` は向きを持たないため）。
- Create機械は解決済みの軸を `axis` に入れて渡す。
- 生の `properties` は渡さない（軸だけで足りる）。

### 5. JS側 `renderBlocks` の描画
各ブロックについて寸法テーブルを引き、`BoxGeometry` のサイズとオフセットを差し替えて描く。向き依存ブロックは渡された `axis` に従ってサイズを回転させる。未登録IDは従来通り 1×1×1。テクスチャ・色フォールバック・カメラ操作・バウンディングボックス計算など既存の仕組みはそのまま流用する。

### 6. Create機械モードにプレビュー呼び出しを新設
既存の `RenderArchPreviewAsync` を参考に、`PlacedBlock` のリストを受け取り、`GetRotationAxis` で軸を解決し、テクスチャ辞書を送ってから `{ x, y, z, id, axis }` を `RenderAsync` に渡すCreate版メソッドを作る。`MachineGenerate_Click` の生成成功後に呼ぶ。

### 7. 対応ブロックは順次追加
- **第一弾（Create系）**: 大型歯車、シャフト、ガントリーシャフト、普通の歯車。はみ出し・痩せを解消する。
- **続けて（普通建築）**: フェンス、ハーフブロック、階段、壁など、形状ブロックも今回のうちに順番に登録していく。

## スコープ外（今回やらないこと）

- **可動ブロックの可動域表示は行わない。** ピストンやガントリーは静止状態の見た目のみ描画し、伸縮範囲や移動経路のゴースト表示は作らない。
- **形状ブロックの実配置対応は別タスク。** 今回のスコープは「プレビューで正しく描けるようにする」ところまで。実際のボクセル配置（NBT出力）や色マッチの `shape` 除外を外す作業は別タスクに切り出す（プレビュー対応と配置対応を分離し、「まず見える」段階を配置崩れ問題に巻き込ませない）。

## 作業段階

| ステップ | 内容 |
|---|---|
| **A** | Create機械にプレビューを繋ぐ最小配線（全ブロック 1×1×1 のまま、まず3Dで見える状態にする） |
| **B** | 寸法テーブルと `axis` 受け渡しを実装し、はみ出し・痩せを解消する |
| **C** | 普通建築の形状ブロックをテーブルへ順次追加する（プレビュー描画のみ。実配置は別タスク） |

## 補足：調査で判明した発見

普通建築で形状ブロックが使えない直接原因は、`RunSculptureFromImagesAsync` 内の色マッチ候補から `shape`（階段・ハーフ・柵・壁・鉄格子）を明示除外していること。理由はコメントに「ボクセルに置くと向き/状態で崩れるため」とある。寸法テーブル方式が安定すれば、この除外を見直せる可能性がある（ステップC以降・別タスクの検討事項）。

## 関連ファイル（実装時の参照先）

- `src/ModSorter/Architect/Preview/PreviewHtml.cs` — Three.js 描画本体。`renderBlocks` を拡張。
- `src/ModSorter/Architect/Preview/PreviewWindow.xaml.cs` — WebView2 ラッパー。`RenderAsync` / `SetTexturesAsync`。
- `src/ModSorter/MainWindow.Architect.cs` — 普通建築のプレビュー呼び出し（`RenderArchPreviewAsync`）。Create版の参考元。
- `src/ModSorter/MainWindow.Machine.cs` — Create機械の生成処理（`MachineGenerate_Click`）。プレビュー呼び出しの新設先。
- `src/ModSorter/Architect/Generation/ConnectionSpec.cs` — `ConnectionCatalog.GetRotationAxis()`。軸解決に使用。
- `src/ModSorter/Clients/ModuleGenerator.cs` — `PlacedBlock`（`Properties` に axis/facing を保持）。
