namespace ModSorter.Architect.Manual;

// 船体の船種ごとの既定値。HullParamsControl はスライダーの並びを共通で持ち、
// 初期値と実物の説明文だけをここから受け取る。船種を増やすときは HullPreset を
// 1件足して Of に1行加えるだけでよい。
//
// 位置引数のレコードにすると項目が40近くになって順番を数え違えるので、
// 既定値付きのフィールドで持つ。船種ごとに違う値だけを書けばよい。
internal sealed class HullPreset
{
    public string Jp = "";
    public string Note = "";

    // 主要目。
    public int Len = 24, Beam = 6, Depth = 4, Draft = 2;

    // 横断面・水線の平面形。
    public int Section = 45, Entry = 18, BowFull = 45, Run = 30, SternFull = 60, Transom = 35;

    // 前後の立ち上がり・喫水線より上。
    public int Rake = 15, Rise = 1, Flare = 12, Tumble = 0, Sheer = 100;

    // 構造・付帯。
    public int Frame = 4, Keel = 1, Bulwark = 1, BeamStep = 0;

    // 艤装。
    public int Masts = 1, MastH = 12;
    public string Sail = "set";
    public int SailW = 10, SailH = 8;
    public int Shields = 0;
    public bool Oar = false, SternRudder = false;
    public string Head = "none";
    public int CastleAft = 0, CastleFore = 0, CastleLen = 20;

    // 素材。
    public string Shell = "minecraft:oak_planks";
    public string Deck = "minecraft:spruce_planks";
    public string Keelb = "minecraft:stripped_oak_log";
    public string Frameb = "minecraft:oak_log";
    public string Railb = "minecraft:spruce_planks";
    public string Mastb = "minecraft:spruce_log";
    public string Sailb = "minecraft:white_wool";
    public string Shieldb = "minecraft:birch_trapdoor";
    public string Shieldb2 = "minecraft:dark_oak_trapdoor";
    public string Fitb = "minecraft:stripped_spruce_log";
    public string Castleb = "minecraft:oak_planks";
}

// ダウ船・ジャンク船・ピナスの既定値は HullPresets.Sail.cs にある（1ファイル9KBの目安）。
// 分けたのは値だけで、Of の switch はここ1か所に残す。
internal static partial class HullPresets
{
    public static HullPreset Of(string kind) => kind switch
    {
        "cog" => Cog,
        "dhow" => Dhow,
        "junk" => Junk,
        "pinnace" => Pinnace,
        _ => Longship,
    };

    // ゴクスタ船は全長23.24m・型幅5.20m・深さ2.02m・喫水0.85m級。外板はクリンカー張りの
    // 16列で、肋骨の間隔は約0.96m（1マス=1mでは表せないので見える最小の2マスへ丸める）。
    // 竜骨は外板より下へ張り出す。船首材・船尾材が高く立ち上がるダブルエンダーで、
    // 舷側の反り（シア）が強い。櫂穴は片舷16で盾は計32枚。舷墻は持たず最上列の外板
    // （シアストレーク）が舷縁を兼ねる。
    private static readonly HullPreset Longship = new()
    {
        Jp = "ロングシップ",
        Note = "ゴクスタ船は全長23.24m・型幅5.20m・深さ2.02m・喫水0.85m。1マス=1m。",
        Len = 23,
        Beam = 5,
        Depth = 2,
        Draft = 1,
        Section = 50,
        Entry = 15,
        BowFull = 30,
        Run = 40,
        SternFull = 30,
        Transom = 0,
        Rake = 45,
        Rise = 1,
        Flare = 8,
        Tumble = 0,
        Sheer = 250,
        Frame = 2,
        Keel = 1,
        Bulwark = 0,
        BeamStep = 0,
        Masts = 1,
        MastH = 11,
        Sail = "set",
        SailW = 11,
        SailH = 10,
        Shields = 16,
        Oar = true,
        SternRudder = false,
        Head = "spiral",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
    };

    // ブレーメン・コグ（1380年建造・1962年ヴェーザー川で発見）は全長23.27m・最大幅7.62m・
    // 舷側高4m級で、喫水2.25mのとき排水量139t。船底は平らなカーベル張りなので断面は
    // 矩形寄り、舷側はクリンカー張りの外開き。船首材・船尾材はともに直線で強く傾斜し
    // トランサムを持たない。外板を貫いて横梁の木口が外へ突き出すのが外見上の要点。
    // 舵は船尾材に付く中心線舵で、帆装は単檣の横帆1枚。船尾に高い船楼を載せる。
    // 帆は舷墻の天端へ食い込まないようマストを16にして帆桁ごと2段持ち上げている。
    //
    // 船楼の高さは舷縁より上へ3マス。実物は竜骨から舷縁まで4.26mに対し、船楼と
    // 巻き上げ機を含めた全高が7.02mなので、舷縁から上は2.7m級。内訳は船室・
    // 船楼甲板・手すりの3マス。5マスでは実物の倍近くなり塔のように見える。
    private static readonly HullPreset Cog = new()
    {
        Jp = "コグ船",
        Note = "ブレーメン・コグは全長23.27m・最大幅7.62m・舷側高4m級、喫水2.25mで排水量139t。",
        Len = 23,
        Beam = 8,
        Depth = 4,
        Draft = 2,
        Section = 90,
        Entry = 30,
        BowFull = 70,
        Run = 35,
        SternFull = 65,
        Transom = 0,
        Rake = 50,
        Rise = 2,
        Flare = 14,
        Tumble = 0,
        Sheer = 120,
        Frame = 2,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 4,
        Masts = 1,
        MastH = 16,
        Sail = "set",
        SailW = 12,
        SailH = 11,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 3,
        CastleFore = 0,
        // 船楼の長さは全長の30%。実物の船楼は船体の後ろ3割ほどを占める。20%だと
        // 範囲内の甲板が幅4マスまでしか広がらず、箱も細いまま船尾に乗る。
        CastleLen = 30,
        Deck = "minecraft:oak_planks",
        Railb = "minecraft:oak_planks",
        Fitb = "minecraft:stripped_oak_log",
        Castleb = "minecraft:dark_oak_planks",
    };
}
