using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ModSorter.Architect.Manual;

// 手動生成の「大分類 → 中分類 → 小分類」マスター表。
// ROADMAP.md のマスターリスト（確定版 v2）をコード側の単一の正とする。
// KPI1（網羅性）に従い未実装の小分類もすべて登録し、UI では「（未実装）」と明示する。
// 実装するときは該当行の factory に生成関数を渡すだけでよい（switch 分岐は不要）。
//
// 中分類は「港湾」「空港」のような下位グルーピング。1つのプルダウンに数十件が
// 並ぶとスクロールが必要になるため、中分類で一段絞ってから小分類を出す。
//
// ファイル分割（1ファイル9,000バイト以下の目安）:
//   ManualCatalog.cs          … 型定義（Category / MiddleCategory / SubCategory）と
//                               ヘルパ（Todo / Impl / Mid）・Categories・Find*
//   ManualCatalog.Building.cs … 大分類4 建築物の表
//   ManualCatalog.Hull.cs     … 大分類3 船体の表
//   ManualCatalog.Aero.cs     … 大分類1 プロペラ・大分類2 バルーンの表
// このファイルが14,248バイトになり、raw の1回の取得（10,000バイト）で全文を
// 読めなくなったので大分類ごとに割った。小分類を1件実装するときに触るのは
// 該当する大分類の1枚だけになる。
//
// Categories は静的コンストラクタで組む。partial の各部分が結合される順番は
// 言語仕様で未定義なので、Categories をフィールド初期化子のままにすると
// BuildingMiddles などの初期化より先に走って null が入る可能性がある。
// 静的コンストラクタは全フィールド初期化子のあとに実行されることが保証されて
// いるため、ファイルの並び順に依存しなくなる。
public static partial class ManualCatalog
{
    // 大分類。Id は設定保存やログ用の安定キー。
    public sealed class Category
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<MiddleCategory> Middles { get; }

        // 大分類配下の小分類を平坦に見たいとき用（検索・件数集計）。
        public IEnumerable<SubCategory> AllSubs => Middles.SelectMany(m => m.Subs);

        public Category(string id, string displayName, IReadOnlyList<MiddleCategory> middles)
        {
            Id = id;
            DisplayName = displayName;
            Middles = middles;
        }

        public override string ToString() => DisplayName;
    }

    // 中分類。小分類の束。
    public sealed class MiddleCategory
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<SubCategory> Subs { get; }

        public int ImplementedCount => Subs.Count(s => s.Implemented);
        public bool HasImplemented => ImplementedCount > 0;

        // ComboBox に出す表示名。実装状況を出して選ぶ手がかりにする。
        // 全部実装済み → "港湾" / 一部 → "空港（3/9）" / 皆無 → "鉄道（未実装）"
        public string Label
        {
            get
            {
                int done = ImplementedCount;
                if (done == 0) return DisplayName + "（未実装）";
                if (done == Subs.Count) return DisplayName;
                return $"{DisplayName}（{done}/{Subs.Count}）";
            }
        }

        public MiddleCategory(string id, string displayName, IReadOnlyList<SubCategory> subs)
        {
            Id = id;
            DisplayName = displayName;
            Subs = subs;
        }

        public override string ToString() => Label;
    }

    // 小分類1件。Factory が null なら未実装。
    public sealed class SubCategory
    {
        public string Id { get; }
        public string DisplayName { get; }

        // パラメータUIの生成関数。戻り値は IManualParamControl を実装した UserControl。
        // null = 未実装。
        public Func<UserControl>? Factory { get; }

        public bool Implemented => Factory != null;

        public string Label => Implemented ? DisplayName : DisplayName + "（未実装）";

        public SubCategory(string id, string displayName, Func<UserControl>? factory)
        {
            Id = id;
            DisplayName = displayName;
            Factory = factory;
        }

        public override string ToString() => Label;
    }

    // 未実装エントリを短く書くためのヘルパー。
    private static SubCategory Todo(string id, string name) => new(id, name, null);

    // 実装済みエントリ。
    private static SubCategory Impl(string id, string name, Func<UserControl> factory)
        => new(id, name, factory);

    // 中分類。
    private static MiddleCategory Mid(string id, string name, params SubCategory[] subs)
        => new(id, name, subs);

    // 表示順は ROADMAP のマスターリスト順ではなく、実装が進んでいる順に置く。
    // 既定選択（先頭）が実装済みになり、初回表示でプレビューが必ず出る。
    public static IReadOnlyList<Category> Categories { get; }

    static ManualCatalog()
    {
        Categories = new[]
        {
            new Category("building", "建築物", BuildingMiddles),
            new Category("hull", "船体", HullMiddles),
            new Category("propeller", "プロペラ", PropellerMiddles),
            new Category("balloon", "バルーン", BalloonMiddles),
        };
    }

    public static Category? FindCategory(string id)
        => Categories.FirstOrDefault(c => c.Id == id);

    public static MiddleCategory? FindMiddle(string categoryId, string middleId)
        => FindCategory(categoryId)?.Middles.FirstOrDefault(m => m.Id == middleId);

    // 小分類 Id は全体で一意。大分類だけ指定すれば中分類をまたいで見つかる。
    public static SubCategory? FindSub(string categoryId, string subId)
        => FindCategory(categoryId)?.AllSubs.FirstOrDefault(s => s.Id == subId);
}
