namespace ModSorter.Architect.Manual;

// 大分類4「建築物」の表。ManualCatalog の partial。
// 型定義とヘルパ（Todo / Impl / Mid）・Categories は ManualCatalog.cs にある。
//
// ManualCatalog.cs が14,248バイトになり raw の1回の取得で全文を読めなくなったので、
// 大分類ごとに割った。ここは 建物・港湾・空港・鉄道・橋梁・産業・発電所 の7中分類・
// 小分類50件で、全件実装済み。
//
// 並び順がそのまま ComboBox の並びになる。先頭の中分類・小分類が既定選択に
// なるので、実装済みのものを先頭に置く。
public static partial class ManualCatalog
{
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
            Impl("suspension_bridge", "吊り橋", () => new SuspensionBridgeParamsControl()),
            Impl("arch_bridge", "アーチ橋", () => new ArchBridgeParamsControl()),
            Impl("bascule_bridge", "跳開橋", () => new BasculeBridgeParamsControl())),

        // 産業インフラ
        Mid("industry", "産業",
            Impl("wind_turbine", "風車", () => new RotorParamsControl("wind_turbine")),
            Impl("water_wheel", "水車", () => new RotorParamsControl("water_wheel")),
            Impl("silo", "サイロ", () => new VesselParamsControl("silo")),
            Impl("water_tower", "給水塔", () => new VesselParamsControl("water_tower")),
            Impl("tank", "タンク", () => new VesselParamsControl("tank"))),

        // 発電所。1基まるごとではなく構成する単体構造物ごとに並べる（港湾・空港と同じ扱い）。
        Mid("power_plant", "発電所",
            Impl("boiler_house", "ボイラ建屋", () => new PowerPlantParamsControl("boiler_house")),
            Impl("turbine_hall", "タービン建屋", () => new PowerPlantParamsControl("turbine_hall")),
            Impl("stack", "煙突", () => new PowerPlantParamsControl("stack")),
            Impl("cooling_tower", "冷却塔", () => new PowerPlantParamsControl("cooling_tower")),
            Impl("containment", "原子炉格納容器", () => new PowerPlantParamsControl("containment")),
            Impl("switchyard", "変電ヤード", () => new PowerPlantParamsControl("switchyard"))),
    };
}
