namespace ModSorter.Architect.Manual;

// 大分類1「プロペラ」と大分類2「バルーン」の表。ManualCatalog の partial。
// 型定義とヘルパ（Todo / Impl / Mid）・Categories は ManualCatalog.cs にある。
//
// ManualCatalog.cs が14,248バイトになり raw の1回の取得で全文を読めなくなったので、
// 大分類ごとに割った。この2つはどちらも小分類が10件・8件と少なく、フェーズ7で
// 同時に着手する（回転体・曲面の生成器が要るという同じ前提を持つ）ので1枚にまとめた。
// 片方が実装で伸びて9,000バイトに近づいたら ManualCatalog.Propeller.cs /
// ManualCatalog.Balloon.cs へ割る。
public static partial class ManualCatalog
{
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
}
