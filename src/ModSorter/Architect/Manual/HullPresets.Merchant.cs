namespace ModSorter.Architect.Manual;

// 商船の既定値。客船・貨物船の2種。
// 分けたのは値だけで、Of の switch は HullPresets.cs 1か所に残す。
//
// 商船は実物が100m級で、全長スライダーの上限を140マスへ上げてから入れている。
// 帆船（〜64マス）に比べてブロック数が1桁増えるので、生成に時間がかかる。
internal static partial class HullPresets
{
    // SS グレート・ブリテン（1845）。世界初の大型スクリュー客船で、ブリストルで
    // 保存されている。全長322ft＝98m・型幅50ft6in＝15.4m・喫水16ft＝4.9m・3,400t、
    // 蒸気機関1,000馬力、乗客252人。深さは喫水5に乾舷5を足して10。
    // 鉄船なので外板は鉄色（灰色のコンクリート）、甲板は白木。
    // 煙突1本と帆装用のマスト6本を持つ機帆船で、生成器のマスト上限に合わせて
    // 4本＋横帆で近似する（帆は畳んだ状態が実船の平常時に近い）。
    // 客室は甲板の下なので船楼は低く、船体中央に3層のデッキハウスを載せる。
    // 船首像・船首斜檣は PutHead が z 方向へ出せないので未再現。
    private static readonly HullPreset Liner = new()
    {
        Jp = "客船",
        Note = "SS グレート・ブリテンは全長98m・型幅15.4m・喫水4.9m、3,400t・乗客252人。",
        Len = 98,
        Beam = 15,
        Depth = 10,
        Draft = 5,
        Section = 70,
        Entry = 12,
        BowFull = 30,
        Run = 30,
        SternFull = 50,
        Transom = 30,
        Rake = 15,
        Rise = 2,
        Flare = 8,
        Tumble = 4,
        Sheer = 90,
        Frame = 2,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 4,
        MastH = 26,
        Sail = "furled",
        SailW = 16,
        SailH = 18,
        GunRows = 0,
        GunStep = 0,
        GunBase = 1,
        RowOars = 0,
        HouseDecks = 3,
        HouseLen = 20,
        HouseShift = 0,
        Funnel = 5,
        Holds = 0,
        Derrick = false,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        Shell = "minecraft:gray_concrete",
        Deck = "minecraft:birch_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:iron_block",
        Railb = "minecraft:gray_concrete",
        Mastb = "minecraft:spruce_log",
        Fitb = "minecraft:stripped_spruce_log",
        Castleb = "minecraft:white_concrete",
        Funnelb = "minecraft:black_concrete",
        Glassb = "minecraft:glass_pane",
    };

    // リバティ船（EC2型 1941〜45年に2,710隻）。全長441ft6in＝134.57m・
    // 型幅56ft10.75in＝17.34m・深さ37ft4in＝11.38m・満載喫水27ft9.25in＝8.46m、
    // 載貨重量10,856t。全長は上限140マスに収まる。
    // 貨物艙は5つで、船体中央に3層の船橋楼と煙突1本。艙口の前後に荷役デリックが
    // 並ぶ。船首楼・船尾楼を持つ三島型なので船尾楼2・船首楼2。
    // 溶接鋼船なので外板は灰色、甲板は鉄色。マストは持たず、デリックの柱だけ。
    // 深さは基線から上甲板までの11。船底は箱に近いので断面85。
    private static readonly HullPreset Cargo = new()
    {
        Jp = "貨物船",
        Note = "リバティ船は全長134.57m・型幅17.34m・深さ11.38m・喫水8.46m、載貨重量10,856t。",
        Len = 135,
        Beam = 17,
        Depth = 11,
        Draft = 8,
        Section = 85,
        Entry = 20,
        BowFull = 55,
        Run = 25,
        SternFull = 60,
        Transom = 35,
        Rake = 10,
        Rise = 1,
        Flare = 6,
        Tumble = 0,
        Sheer = 60,
        Frame = 2,
        Keel = 0,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 0,
        MastH = 12,
        Sail = "none",
        SailW = 10,
        SailH = 8,
        GunRows = 0,
        GunStep = 0,
        GunBase = 1,
        RowOars = 0,
        HouseDecks = 3,
        HouseLen = 15,
        HouseShift = 0,
        Funnel = 4,
        Holds = 5,
        Derrick = true,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 2,
        CastleFore = 2,
        CastleLen = 12,
        Shell = "minecraft:gray_concrete",
        Deck = "minecraft:light_gray_concrete",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:iron_block",
        Railb = "minecraft:gray_concrete",
        Mastb = "minecraft:iron_block",
        Fitb = "minecraft:iron_block",
        Castleb = "minecraft:white_concrete",
        Funnelb = "minecraft:black_concrete",
        Glassb = "minecraft:glass_pane",
    };
}
