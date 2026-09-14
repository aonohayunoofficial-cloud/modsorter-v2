namespace ModSorter.Architect.Manual;

// 大分類3「船体」の表。ManualCatalog の partial。
// 型定義とヘルパ（Todo / Impl / Mid）・Categories は ManualCatalog.cs にある。
//
// ManualCatalog.cs が14,248バイトになり raw の1回の取得で全文を読めなくなったので、
// 大分類ごとに割った。船種は31件あり、実装が進むたびに Todo から Impl へ変わる行が
// ここに集まる。
//
// 中分類は船種の実分類（用途・帆走/機走・時代）で切る。パラメータUIは全船種で
// 共通の HullParamsControl で、船種ごとに変わるのは kind 引数から引く既定値だけ
// （既定値は HullPresets.cs とその分割ファイルにある）。
public static partial class ManualCatalog
{
    private static readonly MiddleCategory[] HullMiddles =
    {
        Mid("small_craft", "小型艇",
            Impl("rowboat", "手漕ぎボート", () => new HullParamsControl("rowboat")),
            Impl("motorboat", "モーターボート", () => new HullParamsControl("motorboat")),
            Todo("speedboat", "スピードボート"),
            Todo("yacht", "ヨット"),
            Todo("catamaran", "双胴船")),

        Mid("work_boat", "作業船",
            Todo("tugboat", "タグボート"),
            Todo("trawler", "トロール船")),

        Mid("sail_old", "帆船（中世〜大航海）",
            Impl("cog", "コグ船", () => new HullParamsControl("cog")),
            Impl("longship", "ロングシップ", () => new HullParamsControl("longship")),
            Impl("dhow", "ダウ船", () => new HullParamsControl("dhow")),
            Impl("junk", "ジャンク船", () => new HullParamsControl("junk")),
            Impl("pinnace", "ピナス", () => new HullParamsControl("pinnace")),
            Impl("caravel", "キャラベル", () => new HullParamsControl("caravel")),
            Impl("carrack", "キャラック", () => new HullParamsControl("carrack")),
            Impl("galleon", "ガレオン", () => new HullParamsControl("galleon"))),

        Mid("sail_modern", "帆船（近代）",
            Impl("sloop", "スループ", () => new HullParamsControl("sloop")),
            Impl("schooner", "スクーナー", () => new HullParamsControl("schooner")),
            Impl("clipper", "クリッパー", () => new HullParamsControl("clipper"))),

        Mid("warship_sail", "帆走軍艦",
            Impl("frigate", "フリゲート", () => new HullParamsControl("frigate")),
            Impl("ship_of_the_line", "戦列艦", () => new HullParamsControl("ship_of_the_line")),
            Impl("war_galley", "軍用ガレー", () => new HullParamsControl("war_galley"))),

        Mid("merchant", "商船",
            Impl("liner", "客船", () => new HullParamsControl("liner")),
            Impl("cargo", "貨物船", () => new HullParamsControl("cargo"))),

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
}
