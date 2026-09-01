# 船体 / 共通事項（`hull`）

親: [../ROADMAP.md](../ROADMAP.md) ｜ 進捗: 16/31

## 共通パラメータ

全長・全幅・喫水深・船底絞り・船体フレア・フレアカーブ・タンブルホーム・
シアーカーブ・船首鋭さ・船尾オーバーハング ＋船種固有の上部構造

## 断面生成器の設計（フェーズ5・完了）

共通の断面生成器を先に作る。竜骨→フレーム→外板→甲板→上部構造の順。
帆装（マスト・ヤード・索具）は帆船系の共通モジュールとして切り出す。

- 断面生成器 ── 実装済み。`HullExpander.cs` / `HullExpander.Form.cs` /
  `HullExpander.Shell.cs` と `StructureSpec.Hull.cs`。structure_type は "hull:<船種>" で、
  AI生成側の "ship"（ShipExpander）とは接頭辞もプロパティ（ship_* と hull_*）も分ける。
  素材スロットは共通のもの（hull_block/deck_block/base_block/accent_block/parapet_block）を
  流用し、二重宣言を作らない。
- 船型は `HullExpander.Form` の1か所に集約し、外寸は `HullExpander.Extent(spec)` を
  UI からも呼ぶ。桁橋で起きた「UIと展開側で式が二重になる」状態を作らない。
- 断面は超楕円 (|x|/b)^k + ((喫水線-y)/(喫水線-船底))^k = 1。k=1 が直線V（デッドライズの
  深い滑走艇）、k=2 が円弧（丸ビルジの排水量型）、k=8 がほぼ矩形（Cm≈0.98 のタンカー）。
- 水線の平面形は 船首テーパー／平行部／船尾の絞り の3区間。船首テーパー長＝最大半幅÷
  tan(入角)。入角（水線の半角）は実船8〜20度、肥えた船で30度超。
- シアは ICLL 1966 の標準シア。船尾垂線 25(L/3+10) mm・L/6 で11.1・L/3 で2.8・船体中央 0・
  前方 L/3 で5.6・L/6 で22.2・船首垂線 50(L/3+10) mm で、船首側が船尾側の2倍。
  倍率100%が標準で、反りの強いロングシップは200〜300%を指定する。
- 船首材の傾斜は現代の直立船首0〜10度・快速帆船45度超。水平方向の走り＝深さ×tan(傾斜)
  だけ船底の前端が甲板の前端より後ろへ下がる。
- 外板は体積の境界抽出で置く。面ごとに条件を書き分けると船首テーパーや船尾の
  立ち上がりの斜面に穴が空くため、内外判定から拾って必ず閉じるようにした。
- フレーム間隔は実船0.5〜0.9m級だが1マス=1mでは表現できないので、見えるように
  2マス以上へ丸める。ブルワークは満載喫水線規則の1m以上を最小とする。

## ファイル分割（1ファイル9KB以下の目安）

- `HullExpander.cs` … 入口・素材・回転・正規化・外寸
- `HullExpander.Form.cs` … 断面生成器
- `HullExpander.Shell.cs` … 竜骨・フレーム・外板・甲板・ブルワーク
- `HullExpander.Top.cs` … 上部構造のパレットと寸法（`TopPalette` / `Top`）
- `HullExpander.Rig.cs` … マスト・盾掛け・側舵・船首材の飾り
- `HullExpander.Sail.cs` … 横帆・縦帆と帆桁（Rig.cs が8.9KBになったので帆だけ分離）
- `HullExpander.Gun.cs` … 砲門と砲身
- `HullExpander.Oar.cs` … 櫂
- `HullExpander.House.cs` … 船体中央のデッキハウス（船橋楼）と煙突
- `HullExpander.Cargo.cs` … 貨物艙口・コーミング・荷役デリック
- `HullExpander.Castle.cs` … 船楼
- `HullExpander.Beam.cs` … 貫通横梁・中心線舵（Castle.cs が9.5KBになったので分離）
- `HullParamsControl.cs` … UI の組み立てと `BuildSpec`
- `HullParamsControl.Panel.cs` … スライダー・選択肢の並び
- `HullParamsControl.Blocks.cs` … 使用ブロックの並び（Panel.cs が7.8KBになったので分離）
- `HullParamsControl.Summary.cs` … 要約文
- `HullPresets.cs` … 船種ごとの既定値の本体（HullPreset の定義・`Of`・ロングシップ・コグ船）。
  船種追加は `HullPreset` を1件足して `Of` に1行加える
- `HullPresets.Sail.cs` … 帆船（中世〜大航海）の追加分（ダウ船・ジャンク船・ピナス）の既定値
- `HullPresets.Discovery.cs` … 同じく追加分（キャラベル・キャラック・ガレオン）の既定値
- `HullPresets.Modern.cs` … 帆船（近代）の既定値（スループ・スクーナー・クリッパー）
- `HullPresets.Warship.cs` … 帆走軍艦の既定値（フリゲート・戦列艦・軍用ガレー）
- `HullPresets.Merchant.cs` … 商船の既定値（客船・貨物船）

## 全船種にまたがる残課題

- `PutHead` が z 方向へ出せないため、船首斜檣（バウスプリット）・ビークヘッド・
  衝角（スプロン）・船首像が未再現。
- `BuildCastle` が z 方向へ出せないため、船首楼が船首材より前へ張り出す形が未再現。
- タンブルホームは船体中央より後ろでしか効かない（`Form.HalfAt` の wf）。
  全長で内へ絞る船（戦列艦など）は前半分が実物より張っている。
- ラティーン（三角帆）を持たない。ガレー・ダウ船は横帆で近似している。
- スライダー上限は全長140・型幅32・深さ24（商船対応で引き上げ済み）。
  `HullExpander.Form` はもとから 4〜400 を許すので、上げたのはUI側の上限だけ。
  全長100マス級ではブロック数が1万を超えるため、生成に時間がかかる注記をUIへ入れてある。
