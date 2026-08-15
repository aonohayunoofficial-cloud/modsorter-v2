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
public static class ManualCatalog
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

    // ===== 大分類4: 建築物（フェーズ4の対象） =====
    private static readonly MiddleCategory[] BuildingMiddles =
    {
        // 建物系（箱ベース: 幅・奥行き・高さ・屋根形式）
        Mid("bldg", "建物",
            Impl("house", "戸建て住宅", () => new HouseParamsControl()),
            Impl("apartment", "集合住宅", () => new ApartmentParamsControl()),
            Impl("factory", "工場", () => new FactoryParamsControl()),
            Impl("laboratory", "研究所", () => new LaboratoryParamsControl()),
            Impl("warehouse", "倉庫", () => new WarehouseParamsControl()),
            Impl("shop", "店舗・商業施設", () => new ShopParamsControl()),
            Impl("office", "オフィスビル", () => new OfficeParamsControl()),
            Impl("religious", "宗教建築", () => new ReligiousParamsControl()),
            Impl("tower", "塔", () => new TowerParamsControl()),
            Impl("monument", "記念建築", () => new MonumentParamsControl()),
            Impl("event_venue", "屋外イベント会場", () => new EventVenueParamsControl()),
            Impl("public_facility", "公共施設", () => new PublicFacilityParamsControl())),

        // 港湾（国交省 港湾空港部 分類準拠）
        Mid("harbor", "港湾",
            Impl("quay", "岸壁", () => new HarborParamsControl("quay")),
            Impl("pier", "桟橋", () => new HarborParamsControl("pier")),
            Impl("breakwater", "防波堤", () => new HarborParamsControl("breakwater")),
            Impl("transit_shed", "上屋", () => new TransitShedParamsControl()),
            Impl("drydock", "ドライドック", () => new DryDockParamsControl()),
            Impl("gantry_crane", "ガントリークレーン", () => new CraneParamsControl("gantry")),
            Impl("bridge_crane", "橋形クレーン", () => new CraneParamsControl("bridgecrane")),
            Impl("bollard", "係船柱", () => new BollardParamsControl()),
            Impl("lighthouse", "灯台", () => new LighthouseParamsControl())),

        // 空港（国交省 空港土木/建築施設 分類準拠）
        Mid("airport", "空港",
            Impl("control_tower", "管制塔", () => new ControlTowerParamsControl()),
            Impl("passenger_terminal", "旅客ターミナル", () => new PassengerTerminalParamsControl()),
            Impl("cargo_terminal", "貨物ターミナル", () => new CargoTerminalParamsControl()),
            Impl("hangar", "格納庫", () => new HangarParamsControl()),
            Impl("runway", "滑走路", () => new AirportPavementParamsControl("runway")),
            Impl("taxiway", "誘導路", () => new AirportPavementParamsControl("taxiway")),
            Impl("apron", "エプロン", () => new AirportPavementParamsControl("apron")),
            Impl("approach_light", "進入灯", () => new ApproachLightParamsControl()),
            Impl("helipad", "ヘリポート", () => new HelipadParamsControl())),

        // 鉄道駅
        Mid("railway", "鉄道",
            Impl("station_building", "駅舎", () => new StationParamsControl()),
            Impl("platform", "プラットフォーム", () => new PlatformParamsControl()),
            Impl("platform_canopy", "ホーム上屋", () => new PlatformCanopyParamsControl()),
            Impl("overpass", "跨線橋", () => new OverpassParamsControl()),
            Impl("depot", "車庫", () => new DepotParamsControl())),

        // 橋梁
        Mid("bridge", "橋梁",
            Impl("girder_bridge", "桁橋", () => new GirderBridgeParamsControl()),
            Todo("suspension_bridge", "吊り橋"),
            Todo("arch_bridge", "アーチ橋"),
            Todo("bascule_bridge", "跳開橋")),

        // 産業インフラ
        Mid("industry", "産業",
            Todo("power_plant", "発電所"),
            Todo("wind_turbine", "風車"),
            Todo("water_wheel", "水車"),
            Todo("silo", "サイロ"),
            Todo("water_tower", "給水塔"),
            Todo("tank", "タンク")),
    };

    // ===== 大分類3: 船体（フェーズ5〜6） =====
    // 中分類は船種の実分類（用途・帆走/機走・時代）で切る。
    private static readonly MiddleCategory[] HullMiddles =
    {
        Mid("small_craft", "小型艇",
            Todo("rowboat", "手漕ぎボート"),
            Todo("motorboat", "モーターボート"),
            Todo("speedboat", "スピードボート"),
            Todo("yacht", "ヨット"),
            Todo("catamaran", "双胴船")),

        Mid("work_boat", "作業船",
            Todo("tugboat", "タグボート"),
            Todo("trawler", "トロール船")),

        Mid("sail_old", "帆船（中世〜大航海）",
            Todo("cog", "コグ船"),
            Todo("longship", "ロングシップ"),
            Todo("dhow", "ダウ船"),
            Todo("junk", "ジャンク船"),
            Todo("pinnace", "ピナス"),
            Todo("caravel", "キャラベル"),
            Todo("carrack", "キャラック"),
            Todo("galleon", "ガレオン")),

        Mid("sail_modern", "帆船（近代）",
            Todo("sloop", "スループ"),
            Todo("schooner", "スクーナー"),
            Todo("clipper", "クリッパー")),

        Mid("warship_sail", "帆走軍艦",
            Todo("frigate", "フリゲート"),
            Todo("ship_of_the_line", "戦列艦"),
            Todo("war_galley", "軍用ガレー")),

        Mid("merchant", "商船",
            Todo("liner", "客船"),
            Todo("cargo", "貨物船")),

        Mid("warship_modern", "近代軍艦",
            Todo("destroyer", "駆逐艦"),
            Todo("battleship", "戦艦"),
            Todo("carrier", "空母"),
            Todo("submarine", "潜水艦"),
            Todo("submarine_tender", "潜水母艦")),

        Mid("fantasy", "特殊・空想",
            Todo("ark", "方舟"),
            Todo("flying_ship", "飛行船体"),
            Todo("dragon_ship", "ドラゴンシップ")),
    };

    // ===== 大分類1: プロペラ（フェーズ7） =====
    private static readonly MiddleCategory[] PropellerMiddles =
    {
        Mid("blade_count", "枚数",
            Todo("blade2", "2枚羽"),
            Todo("blade3", "3枚羽"),
            Todo("blade4", "4枚羽"),
            Todo("blade_multi", "5枚以上（多翼）")),

        Mid("blade_shape", "翼形状",
            Todo("swept", "後退翼"),
            Todo("straight", "直線翼"),
            Todo("curved", "曲線翼")),

        Mid("blade_mech", "機構",
            Todo("variable_pitch", "可変ピッチ翼"),
            Todo("ducted", "ダクテッド"),
            Todo("contra_rotating", "二重反転")),
    };

    // ===== 大分類2: バルーン（フェーズ7） =====
    private static readonly MiddleCategory[] BalloonMiddles =
    {
        Mid("envelope_shape", "形状",
            Todo("cigar", "葉巻型（ツェッペリン式）"),
            Todo("teardrop", "涙滴型"),
            Todo("sphere", "球形"),
            Todo("hemisphere", "半球（熱気球式）"),
            Todo("spindle", "紡錘型"),
            Todo("multi_cell", "多気嚢（連結）")),

        Mid("envelope_frame", "構造形式",
            Todo("blimp", "ブリンプ（軟式）"),
            Todo("rigid", "硬式飛行船（リブ骨格あり）")),
    };

    // 表示順は ROADMAP のマスターリスト順ではなく、実装が進んでいる順に置く。
    // 既定選択（先頭）が実装済みになり、初回表示でプレビューが必ず出る。
    public static IReadOnlyList<Category> Categories { get; } = new[]
    {
        new Category("building", "建築物", BuildingMiddles),
        new Category("hull", "船体", HullMiddles),
        new Category("propeller", "プロペラ", PropellerMiddles),
        new Category("balloon", "バルーン", BalloonMiddles),
    };

    public static Category? FindCategory(string id)
        => Categories.FirstOrDefault(c => c.Id == id);

    public static MiddleCategory? FindMiddle(string categoryId, string middleId)
        => FindCategory(categoryId)?.Middles.FirstOrDefault(m => m.Id == middleId);

    // 小分類 Id は全体で一意。大分類だけ指定すれば中分類をまたいで見つかる。
    public static SubCategory? FindSub(string categoryId, string subId)
        => FindCategory(categoryId)?.AllSubs.FirstOrDefault(s => s.Id == subId);
}
