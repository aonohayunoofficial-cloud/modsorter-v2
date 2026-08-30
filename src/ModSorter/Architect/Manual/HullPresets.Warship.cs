namespace ModSorter.Architect.Manual;

// 帆走軍艦の既定値。フリゲート・戦列艦・軍用ガレーの3種。
// 分けたのは値だけで、Of の switch は HullPresets.cs 1か所に残す。
//
// 3種とも砲を持つので砲門（GunRows / GunStep / GunBase）を使う。砲門は喫水線から
// GunBase マス上を最下段として、上へ2マスおきに重ねる。甲板のすぐ下には外板を
// 1列残すので、深さが足りない段は開かない。
internal static partial class HullPresets
{
    // レダ級フリゲート。HMS トリンコマリー（1817）は砲甲板長150ft4.5in＝45.83m・
    // 型幅39ft11.25in＝12.17m・艙深12ft9in＝3.89m・1065t（BM）で、18ポンド砲28門＝
    // 片舷14門。深さは基線から上甲板までを取り、喫水5に乾舷4を足して9。
    // 砲門は間隔3で片舷14門ぶん（46÷3）並び、実物の門数と一致する。
    // 砲甲板が水面から高いのがフリゲートの利点なので最下段は喫水線上2。
    // 船体は黒、砲門帯の黄色は外板が1種類なので表せない。甲板は白木のマツ材に
    // 近い白樺。船楼を持たない平甲板で、船首楼・後甲板の舷墻は表さない。
    // 主檣は甲板上45m級・前檣40m・後檣32m級なので、全マスト同高の生成器では
    // 中間の38を取る。主帆桁は76ft＝23mで型幅12の倍近くあり、舷の外へ出る。
    private static readonly HullPreset Frigate = new()
    {
        Jp = "フリゲート",
        Note = "トリンコマリーは砲甲板長45.83m・型幅12.17m・艙深3.89m、18ポンド砲28門。",
        Len = 46,
        Beam = 12,
        Depth = 9,
        Draft = 5,
        Section = 60,
        Entry = 14,
        BowFull = 35,
        Run = 35,
        SternFull = 50,
        Transom = 35,
        Rake = 25,
        Rise = 3,
        Flare = 14,
        Tumble = 6,
        Sheer = 120,
        Frame = 2,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 38,
        Sail = "set",
        SailW = 22,
        SailH = 26,
        GunRows = 1,
        GunStep = 3,
        GunBase = 2,
        RowOars = 0,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        Shell = "minecraft:dark_oak_planks",
        Deck = "minecraft:birch_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:dark_oak_log",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_spruce_log",
    };

    // 一等戦列艦。HMS ヴィクトリー（1765・104門）は砲甲板長186ft＝56.7m・
    // 型幅51ft10in＝15.80m・喫水28ft9in＝8.76m。全長227ft6in＝69.34mは船首斜檣を
    // 含む値なので、斜檣を持たない生成器では砲甲板長の57を全長に取る。
    // 上甲板は水面上7.5m級なので深さは喫水9＋7で16。
    // 砲門は3段。下甲板30門＝片舷15門で中心間12.4ft＝3.8mなので間隔4、
    // 最下段は水面上4ft9in＝1.4mなので喫水線上1。上へ2マスおきに y=10・12・14と
    // 重なり、甲板16の下に外板が1列残る。
    // 太い船体（断面75）と強いタンブルホーム14度が戦列艦の顔。船尾は角形40%。
    // 水面からメインマスト頂まで205ft＝62.5m、甲板上では55m級。前檣・後檣を
    // 均した48を取る。主帆桁102ft＝31mは型幅16の倍近くあり、舷の外へ出る。
    // 船尾楼（プープ）だけを2で載せる。後甲板・船首楼は舷墻の高い平甲板として扱う。
    private static readonly HullPreset ShipOfTheLine = new()
    {
        Jp = "戦列艦",
        Note = "ヴィクトリーは砲甲板長56.7m・型幅15.80m・喫水8.76m、104門・3段の砲門。",
        Len = 57,
        Beam = 16,
        Depth = 16,
        Draft = 9,
        Section = 75,
        Entry = 25,
        BowFull = 60,
        Run = 30,
        SternFull = 55,
        Transom = 40,
        Rake = 25,
        Rise = 4,
        Flare = 10,
        Tumble = 14,
        Sheer = 130,
        Frame = 2,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 48,
        Sail = "set",
        SailW = 30,
        SailH = 34,
        GunRows = 3,
        GunStep = 4,
        GunBase = 1,
        RowOars = 0,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 2,
        CastleFore = 0,
        CastleLen = 20,
        Shell = "minecraft:dark_oak_planks",
        Deck = "minecraft:birch_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:dark_oak_log",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_spruce_log",
        Castleb = "minecraft:dark_oak_planks",
    };

    // 軍用ガレー。レパント（1571）のヴェネツィア・ガレア・ソッティレは全長42m・
    // 型幅5.1mで長さ/幅8:1。細長く浅い船体なので入角8度・断面55・深さ3・喫水1。
    // 漕ぎ座は片舷24。3人が1座に並んで各自1挺を漕ぐ alla sensile 式で漕ぎ手は
    // 計144人。座の間隔約1.2mを1 station 1挺として片舷24挺並べる。
    // 櫂は舷縁の外へ3マス出て先端が水面の1マス上で止まる（幅が左右6マス増える）。
    // 帆装は2本マストのラティーン。生成器は三角帆を持たないので横帆で近似する。
    // 船尾に操舵と船長のための一段高い床（カロッツァ）、船首に砲を据える張り出し
    // （ランバーデ）が載るので、船尾楼2・船首楼2・前後長25%。
    // 船首の衝角（スプロン）は PutHead が z 方向へ出せないので未再現。
    private static readonly HullPreset WarGalley = new()
    {
        Jp = "軍用ガレー",
        Note = "レパントのヴェネツィア・ガレーは全長42m・型幅5.1m、漕ぎ座 片舷24・漕ぎ手144人。",
        Len = 42,
        Beam = 5,
        Depth = 3,
        Draft = 1,
        Section = 55,
        Entry = 8,
        BowFull = 30,
        Run = 25,
        SternFull = 45,
        Transom = 25,
        Rake = 30,
        Rise = 1,
        Flare = 10,
        Tumble = 4,
        Sheer = 120,
        Frame = 2,
        Keel = 1,
        Bulwark = 1,
        BeamStep = 0,
        Masts = 2,
        MastH = 18,
        Sail = "set",
        SailW = 12,
        SailH = 10,
        GunRows = 0,
        GunStep = 0,
        GunBase = 1,
        RowOars = 24,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 2,
        CastleFore = 2,
        CastleLen = 25,
        Deck = "minecraft:oak_planks",
        Keelb = "minecraft:stripped_spruce_log",
        Frameb = "minecraft:spruce_log",
        Railb = "minecraft:spruce_planks",
        Fitb = "minecraft:stripped_oak_log",
        Castleb = "minecraft:dark_oak_planks",
    };
}
