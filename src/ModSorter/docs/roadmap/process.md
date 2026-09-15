# 作業手順と未確定事項

親: [../ROADMAP.md](../ROADMAP.md)

完了条件・実装手順・寸法データの扱い・未確定事項を置く。
方針は [policy.md](policy.md)、工程は [phases.md](phases.md)。

## 1. 各フェーズ共通の完了条件
- 実物（写真/資料）と生成物を突き合わせ再現度確認（KPI2）
- 小分類はマスターに全登録、未実装はUIで明示（KPI1）
- 既存モード（AI生成）が壊れていないことを確認
- `ManualCatalog.cs` と節ファイルと本索引の集計を同じコミットで更新する

## 2. 小分類1件あたりの実装手順
1. `StructureSpec.cs` に必要なプロパティを追加（既存で足りるなら追加しない）
2. 生成ロジックを中分類ごとの Expander に書く（例: `HarborExpander.cs` / `AirportExpander.cs`）
3. `StructureExpander.cs` に `XxxExpander.Handles(structureType)` の分岐を足す
4. `Manual/` に `IManualParamControl` 実装の UserControl を追加。
   パラメータが近い小分類は kind 引数で1クラスに束ねる（例: `CraneParamsControl("gantry")`）
5. `ManualCatalog.cs` の該当行を `Todo` から `Impl` に変える
6. 節ファイルの該当行を `[ ]` → `[x]` に変え、実物研究メモと残課題を書く
7. 索引の進捗サマリと該当大分類の表の数字を直す
8. プレビューで実物写真と見比べ、骨格が成立しているか確認してから push

## 3. 寸法データの扱い
実在の構造物は実寸を調べてから作る。当てずっぽうの比率で作らない。
出典と数値はパラメータUIの注記か Expander のコメントに残し、
節ファイルの「寸法データ（出典）」節にも控える。

## 4. 未確定・要判断事項

**解決済み**
- ~~船体: 既存 ShipExpander を活かすか作り直すか~~ → 共通断面生成器を新設（フェーズ0で決定）
- ~~工場: 建物バリアントか別Specか~~ → 建物バリアント（実装済み）
- ~~中分類のデータ構造: 既存 Genre(JSON)拡張か新テーブルか~~ → 新テーブル `ManualCatalog.cs`。
  Genre(JSON) はAI生成側の資産として据え置き
- ~~平面土木×パラメータ系統: 箱ベースと別UIをどう共存させるか~~ → 専用 Expander ＋
  専用 UserControl。`IManualParamControl` の口だけ揃えれば共存する（港湾系で実証済み）
- ~~ROADMAP.md が60KB級で読み切れない~~ → 中分類ごとに `docs/roadmap/` へ分割
- ~~分割後も ROADMAP.md が14.6KBあり raw の1回の取得（10,000バイト）で読み切れない~~ →
  索引を集計と索引表だけに絞り、方針・工程・作業手順を `policy.md` / `phases.md` /
  `process.md` へ分けた

**未解決**
- 非矩形フットプリント×勾配屋根の制約（現状 flat へ強制フォールバック）
- 可動メタ（`Properties` の axis/facing）を跳開橋・風車・水車でどこまで使うか
- 空港ターミナル系（旅客/貨物）を建物系の箱ベースに寄せるか、専用Specにするか
