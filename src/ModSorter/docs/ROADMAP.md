# modsorter-v2 パラメトリック生成機能 マスターリスト & ロードマップ（索引）

最終更新: 2026-09-01
対象リポジトリ: aonohayunoofficial-cloud/modsorter-v2

この文書は索引と進捗集計だけを持つ。小分類のチェックリスト・実物研究メモ・残課題は
`docs/roadmap/` 配下の節ファイルにある。**進捗の数字はこの文書だけが持ち、
チェックボックスは節ファイルだけが持つ**（二重管理を作らないため）。

この索引が14.6KBになり raw の1回の取得（10,000バイト）で読み切れなくなったので、
集計と索引表以外を役割ごとに3枚へ出した。

| 文書 | 中身 |
|---|---|
| [policy.md](roadmap/policy.md) | 全体方針（KPI・モード構成・UI構造・技術的事実・文書分割の規則） |
| [phases.md](roadmap/phases.md) | ロードマップ（フェーズ0〜8・将来） |
| [process.md](roadmap/process.md) | 完了条件・実装手順・寸法データの扱い・未確定事項 |

---

## 1. マスターリスト（確定版 v3）── 節ファイルへの索引

小分類の一覧・実装状況・実物研究メモは各節ファイルにある。

### 進捗サマリ

| 大分類 | 中分類 | 小分類 | 実装済み |
|---|---:|---:|---:|
| 建築物 | 7 | 50 | 50 |
| 船体 | 8 | 31 | 18 |
| プロペラ | 3 | 10 | 0 |
| バルーン | 2 | 8 | 0 |
| **合計** | **20** | **99** | **68** |

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

### 大分類3: 船体（`hull`） 18/31

中分類は用途・帆走/機走・時代で切る。既存資産（`ShipExpander`）11種と createmod 21種、
追加要望2種を統合し重複を解消した結果が31種。

共通事項（断面生成器の設計・`HullExpander` / `HullPresets` のファイル分割一覧・
全船種にまたがる残課題）は [hull-common.md](roadmap/hull-common.md)。

| 中分類 | 小分類 | 実装済み | 節ファイル |
|---|---:|---:|---|
| 小型艇 (`small_craft`) | 5 | 2 | [hull-small_craft.md](roadmap/hull-small_craft.md) |
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
