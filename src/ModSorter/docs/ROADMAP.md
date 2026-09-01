# modsorter-v2 パラメトリック生成機能 マスターリスト & ロードマップ（索引）

最終更新: 2026-08-30
対象リポジトリ: aonohayunoofficial-cloud/modsorter-v2

この文書は索引と進捗集計だけを持つ。小分類のチェックリスト・実物研究メモ・残課題は
`docs/roadmap/` 配下の節ファイルにある。**進捗の数字はこの文書だけが持ち、
チェックボックスは節ファイルだけが持つ**（二重管理を作らないため）。

---

## 0. 全体方針（確定事項）

### KPI（優先順位順）
1. **網羅性** — 中分類を妥協なく全登録する
2. **生成物の再現度** — 各中分類は実物研究に基づき実物に忠実に生成する
3. 最短性 — 上位KPIを損なわない範囲で成果を早く出す（下位）

### モード構成（トップレベル3分割）
- **手動生成**（選択式パラメトリック）
- **AI生成**（既存の簡易建築をリネーム。将来ローカルLLMのプロンプト変換生成のみ撤去。
  パイプラインは プロンプト→画像生成→3D化→ボクセル化→3Dプレビュー→.nbt出力 を温存）
- **クリエイト建築**（機能ブロック生成。今回ノータッチ。将来ロボット系へ）

### UI構造（確定・v3で更新）
- 手動生成タブの**上部に「大分類選択 → 中分類選択 → 小分類選択」を設置**し、選択に応じて
  下部のパラメータUI（スライダー/トグル/ブロック選択）を切り替える方式
- 大分類 ComboBox → 中分類 ComboBox → 小分類 ComboBox → パラメータパネル（動的差し替え）
- **v3変更点**: v2 までは大分類直下に中分類が最大45件並び、目的の項目までスクロールが必要だった。
  「港湾」「空港」のような下位グルーピングを正式な階層（中分類）へ昇格させ、
  従来の中分類を小分類に降格。どの階層でも一覧が最大12件に収まる
- 呼称の対応: v2「中分類」＝v3「小分類」。v2 の `Group` フィールド＝v3 の中分類

### 進め方の原則
- 既存を壊さず横付け（追加優先）。AI生成パイプラインは資産として温存
- 出力は全モード共通で `List<GeneratedBlock>` → PreviewHtml → StructureNbtWriter に流す
- 各小分類は「実物研究 → 実装」の2段構え
- LLM撤去はユーザーが「撤去」と指示したタイミングで実施

### 重要な技術的事実（コード確認済み）
- `GeneratedBlock` に nullable な `Properties`（ブロックステート辞書）を追加済み【フェーズ2完了】。
  null または空なら従来の単一立方体として扱われる（後方互換）
- `StructureNbtWriter` は `Properties` 辞書を受け取り任意のブロックステートを書ける（axis/facing 対応可能）
- `StructureNbtWriter.Save` は負座標を扱えない。全ブロックを非負に正規化してから書く
- `StructureExpander` は決定論的展開ロジック。手動生成モードの中核資産
- `StructureExpander.Civil.cs` が土木系（スロープ・橋）の別系統として稼働中。
  床/壁/屋根/開口部を通さず座標リストを直接返す。平面土木はこの系統に倣う
- `volumes`（VolumePart 合成）実装済み → 双胴船・複数構造物に活用可能
- `Genre.cs` + `GenreCatalog.cs` + `Genres/*.json` によるデータ駆動機構が既存
- 非矩形フットプリント時は屋根が flat に強制フォールバックする制約あり
- **分類の単一の正は `src/ModSorter/Architect/Manual/ManualCatalog.cs`**。
  節ファイルはその写しと進捗管理を担う。片方だけ更新しないこと

### 文書分割の規則
- 1ファイル9KB以下を目安とする（`.cs` と同じ）。大分類1ファイルから始め、
  9KBを超えたらその大分類を中分類ごとに割る
- ファイル名は `docs/roadmap/<大分類Id>-<中分類Id>.md`。中分類Id は `ManualCatalog` の Id と一致させる
- 集計はこの索引だけ、チェックボックスと研究メモは節ファイルだけ

---

## 1. マスターリスト（確定版 v3）── 節ファイルへの索引

小分類の一覧・実装状況・実物研究メモは各節ファイルにある。

### 進捗サマリ

| 大分類 | 中分類 | 小分類 | 実装済み |
|---|---:|---:|---:|
| 建築物 | 7 | 50 | 50 |
| 船体 | 8 | 31 | 16 |
| プロペラ | 3 | 10 | 0 |
| バルーン | 2 | 8 | 0 |
| **合計** | **20** | **99** | **66** |

### 大分類1: プロペラ（`propeller`） 0/10

| 中分類 | 小分類 | 実装済み | 節ファイル |
|---|---:|---:|---|
| 枚数 (`blade_count`) | 4 | 0 | [propeller.md](roadmap/propeller.md) |
| 翼形状 (`blade_shape`) | 3 | 0 | 同上 |
| 機構 (`blade_mech`) | 3 | 0 | 同上 |

### 大分類2: バルーン（`balloon`） 0/8

| 中分類 | 小分類 | 実装済み | 節ファイル |
|---|---:|---:|---|
| 形状 (`envelope_shape`) | 6 | 0 | [balloon.md](roadmap/balloon.md) |
| 構造形式 (`envelope_frame`) | 2 | 0 | 同上 |

### 大分類3: 船体（`hull`） 16/31

中分類は用途・帆走/機走・時代で切る。既存資産（`ShipExpander`）11種と createmod 21種、
追加要望2種を統合し重複を解消した結果が31種。

共通事項（断面生成器の設計・`HullExpander` / `HullPresets` のファイル分割一覧・
全船種にまたがる残課題）は [hull-common.md](roadmap/hull-common.md)。

| 中分類 | 小分類 | 実装済み | 節ファイル |
|---|---:|---:|---|
| 小型艇 (`small_craft`) | 5 | 0 | [hull-small_craft.md](roadmap/hull-small_craft.md) |
| 作業船 (`work_boat`) | 2 | 0 | [hull-work_boat.md](roadmap/hull-work_boat.md) |
| 帆船・中世〜大航海 (`sail_old`) | 8 | 8 ✅ | [hull-sail_old.md](roadmap/hull-sail_old.md) |
| 帆船・近代 (`sail_modern`) | 3 | 3 ✅ | [hull-sail_modern.md](roadmap/hull-sail_modern.md) |
| 帆走軍艦 (`warship_sail`) | 3 | 3 ✅ | [hull-warship_sail.md](roadmap/hull-warship_sail.md) |
| 商船 (`merchant`) | 2 | 2 ✅ | [hull-merchant.md](roadmap/hull-merchant.md) |
| 近代軍艦 (`warship_modern`) | 5 | 0 | [hull-warship_modern.md](roadmap/hull-warship_modern.md) |
| 特殊・空想 (`fantasy`) | 3 | 0 | [hull-fantasy.md](roadmap/hull-fantasy.md) |

共通パラメータ: 全長・全幅・喫水深・船底絞り・船体フレア・フレアカーブ・タンブルホーム・
シアーカーブ・船首鋭さ・船尾オーバーハング ＋船種固有の上部構造

### 大分類4: 建築物（`building`） 50/50 ✅

共通事項（共通パラメータ・屋根形式マスター・平面土木構造物の扱い）は
[building-common.md](roadmap/building-common.md)。

| 中分類 | 小分類 | 実装済み | 節ファイル |
|---|---:|---:|---|
| 建物 (`bldg`) | 12 | 12 ✅ | [building-bldg.md](roadmap/building-bldg.md) |
| 港湾 (`harbor`) | 9 | 9 ✅ | [building-harbor.md](roadmap/building-harbor.md) |
| 空港 (`airport`) | 9 | 9 ✅ | [building-airport.md](roadmap/building-airport.md) |
| 鉄道 (`railway`) | 5 | 5 ✅ | [building-railway.md](roadmap/building-railway.md) |
| 橋梁 (`bridge`) | 4 | 4 ✅ | [building-bridge.md](roadmap/building-bridge.md) |
| 産業 (`industry`) | 5 | 5 ✅ | [building-industry.md](roadmap/building-industry.md) |
| 発電所 (`power_plant`) | 6 | 6 ✅ | [building-power.md](roadmap/building-power.md) |

港・空港・駅・発電所は「集合体」ではなく、それを構成する単体構造物を小分類として登録する。
将来これらを並べて配置すると結果的に港/空港/駅/発電所になる。

---

## 2. ロードマップ（フェーズ順）

| フェーズ | 内容 | 状態 |
|---|---|---|
| 0 | 事実確定 | 完了 |
| 1 | モード分岐の土台 | 完了 |
| 1.5 | 手動生成UIの分類セレクタ（v3で3段化） | 完了 |
| 2 | 出力の器の拡張（`GeneratedBlock.Properties`） | 完了 |
| 3 | 建築物(家) 縦通し ＋ 屋根積み残し解消 | 完了 |
| 4 | 建築物 小分類の網羅実装 | 完了 50/50 |
| 5 | 船体 共通断面生成器 | 完了（詳細は hull-common.md） |
| 6 | 船種の網羅実装 31種 | 進行中 16/31 |
| 7 | プロペラ・バルーン | 未着手 |
| 8 | AI生成のLLM撤去（ユーザー指示時） | 未着手 |

### フェーズ0: 事実確定 ── 完了
- `ShipExpander` 船種別ビルダー末尾を読了。船は「形状パラメータ差し込み」ではなく
  共通の断面生成器を新設して作り直す方針とする
- 工場は建物の1バリアント（箱ベース＋鋸屋根/モニター屋根）として実装済み
- AI生成モードの呼び出し元配線を特定済み

### フェーズ1: モード分岐の土台（追加のみ・既存ゼロ改変） ── 完了
- `ArchitectModeHost` を3モードハブに拡張
- UIにモード切替（手動生成／AI生成／クリエイト建築）追加
- 既存の簡易建築を「AI生成」にリネーム

### フェーズ1.5: 手動生成UIの分類セレクタ ── 完了（v3で3段に再改修）
- 手動生成タブ上部に大分類/中分類 ComboBox を設置し、`ManualCatalog` 駆動で流し込み
- **v3改修**: 中分類 ComboBox を挿入して3段化。
  `ManualCategoryCombo` / `ManualMiddleCategoryCombo` / `ManualSubCategoryCombo` の連鎖更新。
  既定選択は「最初の実装済みを含む中分類 → その中の最初の実装済み小分類」

### フェーズ2: 出力の器の拡張（改修） ── 完了
- `GeneratedBlock` に nullable な `Properties`（ブロックステート辞書）追加済み
- PreviewHtml が Properties を無視しても壊れないことを確認済み

### フェーズ3: 建築物(家) 縦通し ＋ 屋根積み残し解消 ── 完了
- 既存パラメータ（箱・屋根・開口・柱・軒・煙突）で一本通し済み
- 片流れ屋根の妻側開口を妻壁立ち上げで塞ぐ（方式①）── 実装済み
- ピラミッド屋根をUI屋根タイプに接続 ── 実装済み

### フェーズ4: 建築物 小分類の網羅実装 ── 完了（50/50）
各中分類の実装内容と残課題は節ファイル参照。中分類をまたぐ継続項目のみここに置く。
- 屋根形式追加（hip/ギャンブレル/マンサード/ヘルム 等）・大型シャッターは
  小分類の網羅とは別枠の継続項目として残す

### フェーズ5: 船体 共通断面生成器 ── 完了
断面生成器の式・シア・入角・外板の境界抽出・フレーム間隔などの設計は
[hull-common.md](roadmap/hull-common.md) に移した。

### フェーズ6: 船種の網羅実装（31種） ── 進行中 16/31
残り: 小型艇5・作業船2・近代軍艦5・特殊・空想3。次の対象は小型艇。

### フェーズ7: プロペラ・バルーン（パラメトリック自前実装）
回転体・曲面の生成器が要る。バルーンは楕円体シェルの近似が中心。

### フェーズ8: AI生成のLLM撤去（ユーザー指示時）

### 将来（今回スコープ外）
- クリエイト建築 / ロボット（二足歩行・可動メタ本領）

---

## 3. 各フェーズ共通の完了条件
- 実物（写真/資料）と生成物を突き合わせ再現度確認（KPI2）
- 小分類はマスターに全登録、未実装はUIで明示（KPI1）
- 既存モード（AI生成）が壊れていないことを確認
- `ManualCatalog.cs` と節ファイルと本索引の集計を同じコミットで更新する

## 3.1 小分類1件あたりの実装手順
1. `StructureSpec.cs` に必要なプロパティを追加（既存で足りるなら追加しない）
2. 生成ロジックを中分類ごとの Expander に書く（例: `HarborExpander.cs` / `AirportExpander.cs`）
3. `StructureExpander.cs` に `XxxExpander.Handles(structureType)` の分岐を足す
4. `Manual/` に `IManualParamControl` 実装の UserControl を追加。
   パラメータが近い小分類は kind 引数で1クラスに束ねる（例: `CraneParamsControl("gantry")`）
5. `ManualCatalog.cs` の該当行を `Todo` から `Impl` に変える
6. 節ファイルの該当行を `[ ]` → `[x]` に変え、実物研究メモと残課題を書く
7. 本索引の進捗サマリと該当大分類の表の数字を直す
8. プレビューで実物写真と見比べ、骨格が成立しているか確認してから push

## 3.2 寸法データの扱い
実在の構造物は実寸を調べてから作る。当てずっぽうの比率で作らない。
出典と数値はパラメータUIの注記か Expander のコメントに残し、
節ファイルの「寸法データ（出典）」節にも控える。

---

## 4. 未確定・要判断事項

**解決済み**
- ~~船体: 既存 ShipExpander を活かすか作り直すか~~ → 共通断面生成器を新設（フェーズ0で決定）
- ~~工場: 建物バリアントか別Specか~~ → 建物バリアント（実装済み）
- ~~中分類のデータ構造: 既存 Genre(JSON)拡張か新テーブルか~~ → 新テーブル `ManualCatalog.cs`。
  Genre(JSON) はAI生成側の資産として据え置き
- ~~平面土木×パラメータ系統: 箱ベースと別UIをどう共存させるか~~ → 専用 Expander ＋
  専用 UserControl。`IManualParamControl` の口だけ揃えれば共存する（港湾系で実証済み）
- ~~ROADMAP.md が60KB級で読み切れない~~ → 中分類ごとに `docs/roadmap/` へ分割（本コミット）

**未解決**
- 非矩形フットプリント×勾配屋根の制約（現状 flat へ強制フォールバック）
- 可動メタ（`Properties` の axis/facing）を跳開橋・風車・水車でどこまで使うか
- 空港ターミナル系（旅客/貨物）を建物系の箱ベースに寄せるか、専用Specにするか
