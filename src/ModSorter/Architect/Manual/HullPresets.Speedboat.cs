namespace ModSorter.Architect.Manual;

// スピードボートの既定値。HullPresets.SmallCraft.cs が7.9KBあり、1件足すと
// 9KBの目安を超えるので分けた。Of の switch は HullPresets.cs 1か所に残す。
internal static partial class HullPresets
{
    // リーヴァ・アクアラマ・スペシャル（1972〜96）。全長8.78m・型幅2.60m・
    // 喫水0.63m（0.83mとする資料もある）、V8 350hp級の2基2軸で最高40ノット級
    // （classicboats.co / Sarnico Classic Boats）。マホガニーのニス仕上げの木造艇で、
    // 前から前甲板・中央のコックピット（座席2列と立ち位置）・機関室の上のサンパッド、
    // 座席の前に巻き込み風防（classicboats.co / Capri Luxury Boats）。
    //
    // 船種を選んだ理由は型幅。シガレット38（型幅2.44m）・ボストンホエラー170
    // （2.21m）は1マス=1mで型幅2になり、Form.Span が竜骨の1列に落ちて内法が消える。
    //
    // 1マス=1m での丸め:
    //   全長8.78m→9、型幅2.60m→3、喫水→1。深さは Form の下限2（乾舷1マス）。
    //   型幅3・喫水1では水面下が y=0（竜骨1列）と y=1（水線で幅3）の2段しかなく、
    //   断面のふくらみは形に効かない。滑走艇の直線V寄り（モーターボートと同じ15）にする。
    //   船尾の立ち上がり0・竜骨の張り出し0はモーターボートと同じ滑走艇の理由。
    //   入角30度・船首の肥え60: 入角20度では z=6 の半幅が0.53で幅1に落ち、前甲板が
    //   1列の稜になる。30度で z=0〜6 が幅3、z=7〜8 が船首材の1列になる。
    //   シア50%: 全長9の標準シアは船首で0.65m。100%だと船首の1マスだけが y=3 へ
    //   上がり、下の段を持たない1ブロックが斜めにしか接しない。50%で0.33m→0。
    //   船首材の傾斜35度: 深さ2×tan35°＝1.4→1マス。
    //   トランサム70%: 生成物の船尾は水線で幅3（半幅1.05）。
    // コックピットは全長の45%＝4マス（z=2〜5）。座席2列は z=2 と z=4、
    // 足元は z=3 と z=5、風防は z=6（前甲板の先頭、幅3）。前甲板は z=6〜8 の3m。
    // サンパッドは z=1 の1マス、z=0 は船尾の甲板。
    // 推進器は2軸（x=0 と x=2）で、プロペラの後ろにそれぞれ舵が立つ。径1マス。
    // デッキハウス・マスト・帆・砲門・貨物艙・船楼・盾は持たない。
    // 外板・甲板はマホガニーに近い赤茶としてマングローブの板材、
    // 金物はクロームとして鉄ブロック、風防はガラス板。
    // 座席・サンパッドの色は個体で違うので素材選択で変える。
    private static readonly HullPreset Speedboat = new()
    {
        Jp = "スピードボート",
        Note = "リーヴァ・アクアラマ・スペシャルは全長8.78m・型幅2.60m・喫水0.63m、"
             + "座席2列のコックピットと巻き込み風防、機関室上のサンパッド。1マス=1m。",
        Len = 9,
        Beam = 3,
        Depth = 2,
        Draft = 1,
        Section = 15,
        Entry = 30,
        BowFull = 60,
        Run = 25,
        SternFull = 80,
        Transom = 70,
        Rake = 35,
        Rise = 0,
        Flare = 20,
        Tumble = 0,
        Sheer = 50,
        Frame = 2,
        Keel = 0,
        Bulwark = 0,
        BeamStep = 0,
        OpenBoat = false,
        ThwartStep = 0,
        Masts = 0,
        MastH = 6,
        Sail = "none",
        SailW = 4,
        SailH = 4,
        Shields = 0,
        Oar = false,
        SternRudder = false,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        GunRows = 0,
        GunStep = 0,
        GunBase = 1,
        RowOars = 0,
        Screws = 2,
        HouseDecks = 0,
        HouseLen = 15,
        HouseShift = 0,
        Funnel = 0,
        Holds = 0,
        Derrick = false,
        CockpitLen = 45,
        CockpitShift = 0,
        CockpitSeats = 2,
        Windscreen = true,
        SunPad = 1,
        Shell = "minecraft:mangrove_planks",
        Deck = "minecraft:mangrove_planks",
        Keelb = "minecraft:mangrove_planks",
        Frameb = "minecraft:dark_oak_planks",
        Railb = "minecraft:mangrove_planks",
        Mastb = "minecraft:iron_block",
        Fitb = "minecraft:iron_block",
        Castleb = "minecraft:mangrove_planks",
        Glassb = "minecraft:glass_pane",
        Seatb = "minecraft:smooth_quartz_slab",
        Padb = "minecraft:white_carpet",
    };
}
