using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ModSorter.Architect.Manual;

// 手動生成の「大分類 → 中分類」マスター表。
// ROADMAP.md のマスターリスト（確定版 v2）をコード側の単一の正とする。
// KPI1（網羅性）に従い未実装の中分類もすべて登録し、UI では「（未実装）」と明示する。
// 実装するときは該当行の factory に生成関数を渡すだけでよい（switch 分岐は不要）。
public static class ManualCatalog
{
    // 大分類。Id は設定保存やログ用の安定キー。
    public sealed class Category
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<SubCategory> Subs { get; }

        public Category(string id, string displayName, IReadOnlyList<SubCategory> subs)
        {
            Id = id;
            DisplayName = displayName;
            Subs = subs;
        }

        // ComboBox の DisplayMemberPath 用。
        public override string ToString() => DisplayName;
    }

    // 中分類1件。Factory が null なら未実装。
    public sealed class SubCategory
    {
        public string Id { get; }
        public string DisplayName { get; }

        // 交通インフラのような下位グルーピング（"港湾" 等）。無ければ空文字。
        public string Group { get; }

        // パラメータUIの生成関数。戻り値は IManualParamControl を実装した UserControl。
        // null = 未実装。
        public Func<UserControl>? Factory { get; }

        public bool Implemented => Factory != null;

        // ComboBox に出す表示名。グループがあれば頭に付け、未実装なら明示する。
        public string Label
        {
            get
            {
                string name = Group.Length == 0 ? DisplayName : $"{Group}／{DisplayName}";
                return Implemented ? name : name + "（未実装）";
            }
        }

        public SubCategory(string id, string displayName, string group, Func<UserControl>? factory)
        {
            Id = id;
            DisplayName = displayName;
            Group = group;
            Factory = factory;
        }

        public override string ToString() => Label;
    }

    // 未実装エントリを短く書くためのヘルパー。
    private static SubCategory Todo(string id, string name, string group = "")
        => new(id, name, group, null);

    // 実装済みエントリ。
    private static SubCategory Impl(string id, string name, Func<UserControl> factory, string group = "")
        => new(id, name, group, factory);

    // ===== 大分類4: 建築物（フェーズ4の対象） =====
    private static readonly SubCategory[] BuildingSubs =
    {
        // 建物系（箱ベース: 幅・奥行き・高さ・屋根形式）
        Impl("house", "戸建て住宅", () => new HouseParamsControl(), "建物"),
        Impl("apartment", "集合住宅", () => new ApartmentParamsControl(), "建物"),
        Impl("factory", "工場", () => new FactoryParamsControl(), "建物"),
        Todo("laboratory", "研究所", "建物"),
        Todo("warehouse", "倉庫", "建物"),
        Todo("shop", "店舗・商業施設", "建物"),
        Todo("office", "オフィスビル", "建物"),
        Todo("religious", "宗教建築", "建物"),
        Todo("tower", "塔", "建物"),
        Todo("monument", "記念建築", "建物"),
        Todo("event_venue", "屋外イベント会場", "建物"),
        Todo("public_facility", "公共施設", "建物"),

        // 港湾（国交省 港湾空港部 分類準拠）
        Todo("quay", "岸壁", "港湾"),
        Todo("pier", "桟橋", "港湾"),
        Todo("breakwater", "防波堤", "港湾"),
        Todo("transit_shed", "上屋", "港湾"),
        Todo("drydock", "ドライドック", "港湾"),
        Todo("gantry_crane", "ガントリークレーン", "港湾"),
        Todo("bridge_crane", "橋形クレーン", "港湾"),
        Todo("bollard", "係船柱", "港湾"),
        Todo("lighthouse", "灯台", "港湾"),

        // 空港（国交省 空港土木/建築施設 分類準拠）
        Todo("control_tower", "管制塔", "空港"),
        Todo("passenger_terminal", "旅客ターミナル", "空港"),
        Todo("cargo_terminal", "貨物ターミナル", "空港"),
        Todo("hangar", "格納庫", "空港"),
        Todo("runway", "滑走路", "空港"),
        Todo("taxiway", "誘導路", "空港"),
        Todo("apron", "エプロン", "空港"),
        Todo("approach_light", "進入灯", "空港"),
        Todo("helipad", "ヘリポート", "空港"),

        // 鉄道駅
        Todo("station_building", "駅舎", "鉄道"),
        Todo("platform", "プラットフォーム", "鉄道"),
        Todo("platform_canopy", "ホーム上屋", "鉄道"),
        Todo("overpass", "跨線橋", "鉄道"),
        Todo("depot", "車庫", "鉄道"),

        // 橋梁
        Todo("girder_bridge", "桁橋", "橋梁"),
        Todo("suspension_bridge", "吊り橋", "橋梁"),
        Todo("arch_bridge", "アーチ橋", "橋梁"),
        Todo("bascule_bridge", "跳開橋", "橋梁"),

        // 産業インフラ
        Todo("power_plant", "発電所", "産業"),
        Todo("wind_turbine", "風車", "産業"),
        Todo("water_wheel", "水車", "産業"),
        Todo("silo", "サイロ", "産業"),
        Todo("water_tower", "給水塔", "産業"),
        Todo("tank", "タンク", "産業"),
    };

    // ===== 大分類3: 船体（フェーズ5〜6） =====
    private static readonly SubCategory[] HullSubs =
    {
        Todo("rowboat", "手漕ぎボート"),
        Todo("motorboat", "モーターボート"),
        Todo("speedboat", "スピードボート"),
        Todo("yacht", "ヨット"),
        Todo("tugboat", "タグボート"),
        Todo("trawler", "トロール船"),
        Todo("catamaran", "双胴船"),
        Todo("cog", "コグ船"),
        Todo("longship", "ロングシップ"),
        Todo("dhow", "ダウ船"),
        Todo("junk", "ジャンク船"),
        Todo("pinnace", "ピナス"),
        Todo("caravel", "キャラベル"),
        Todo("carrack", "キャラック"),
        Todo("galleon", "ガレオン"),
        Todo("sloop", "スループ"),
        Todo("schooner", "スクーナー"),
        Todo("clipper", "クリッパー"),
        Todo("frigate", "フリゲート"),
        Todo("ship_of_the_line", "戦列艦"),
        Todo("war_galley", "軍用ガレー"),
        Todo("liner", "客船"),
        Todo("cargo", "貨物船"),
        Todo("destroyer", "駆逐艦"),
        Todo("battleship", "戦艦"),
        Todo("carrier", "空母"),
        Todo("submarine", "潜水艦"),
        Todo("submarine_tender", "潜水母艦"),
        Todo("ark", "方舟"),
        Todo("flying_ship", "飛行船体"),
        Todo("dragon_ship", "ドラゴンシップ"),
    };

    // ===== 大分類1: プロペラ（フェーズ7） =====
    private static readonly SubCategory[] PropellerSubs =
    {
        Todo("blade2", "2枚羽"),
        Todo("blade3", "3枚羽"),
        Todo("blade4", "4枚羽"),
        Todo("blade_multi", "5枚以上（多翼）"),
        Todo("swept", "後退翼"),
        Todo("straight", "直線翼"),
        Todo("curved", "曲線翼"),
        Todo("variable_pitch", "可変ピッチ翼"),
        Todo("ducted", "ダクテッド"),
        Todo("contra_rotating", "二重反転"),
    };

    // ===== 大分類2: バルーン（フェーズ7） =====
    private static readonly SubCategory[] BalloonSubs =
    {
        Todo("cigar", "葉巻型（ツェッペリン式）"),
        Todo("teardrop", "涙滴型"),
        Todo("sphere", "球形"),
        Todo("hemisphere", "半球（熱気球式）"),
        Todo("spindle", "紡錘型"),
        Todo("multi_cell", "多気嚢（連結）"),
        Todo("blimp", "ブリンプ（軟式）"),
        Todo("rigid", "硬式飛行船（リブ骨格あり）"),
    };

    // 表示順は ROADMAP のマスターリスト順ではなく、実装が進んでいる順に置く。
    // 既定選択（先頭）が実装済みになり、初回表示でプレビューが必ず出る。
    public static IReadOnlyList<Category> Categories { get; } = new[]
    {
        new Category("building", "建築物", BuildingSubs),
        new Category("hull", "船体", HullSubs),
        new Category("propeller", "プロペラ", PropellerSubs),
        new Category("balloon", "バルーン", BalloonSubs),
    };

    public static Category? FindCategory(string id)
        => Categories.FirstOrDefault(c => c.Id == id);

    public static SubCategory? FindSub(string categoryId, string subId)
        => FindCategory(categoryId)?.Subs.FirstOrDefault(s => s.Id == subId);
}
