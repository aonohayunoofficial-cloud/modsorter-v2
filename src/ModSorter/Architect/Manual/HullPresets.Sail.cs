namespace ModSorter.Architect.Manual;

// 帆船（中世〜大航海）の既定値のうち、ダウ船・ジャンク船・ピナスの3種。
// 本体の HullPresets.cs は HullPreset の定義・Of・ロングシップ・コグ船で5.8KBあり、
// 3種を足すと1ファイル9KBの目安を超えるので、追加する値だけをここへ分ける。
//
// 3種とも斜桁の帆（ダウのラティーン、ジャンクの横帆式ラグ）を張るが、
// 現状の HullExpander.Rig.cs は帆桁1本の横帆しか組めない。帆の形は横帆で
// 近似し、帆桁を斜めに寝かせる工作は Rig.cs 側の課題として残している。
internal static partial class HullPresets
{
    // サンブーク型のダウ。全長20〜25m・型幅6m級。船首材が鉛直から55度も前へ倒れて
    // 長い水切りを作り、船尾は角形トランサム（インド洋の縫合船がポルトガル船の
    // 船尾を取り入れた形）。船底はカーベル張りの丸ビルジで竜骨を持つ。
    // 舵は船尾材に付く中心線舵。帆装は2本マストのラティーン（settee）で、
    // 帆桁は船体長に迫る長さになるため帆の幅を全長の6割強まで取る。
    // 船尾は一段高い操舵甲板になるので船尾楼2。
    private static readonly HullPreset Dhow = new()
    {
        Jp = "ダウ船",
        Note = "サンブークは全長20〜25m・型幅6m級。船首材が大きく前へ傾き、船尾は角形。",
        Len = 20,
        Beam = 6,
        Depth = 3,
        Draft = 2,
        Section = 60,
        Entry = 14,
        BowFull = 35,
        Run = 35,
        SternFull = 55,
        Transom = 45,
        Rake = 55,
        Rise = 2,
        Flare = 16,
        Tumble = 4,
        Sheer = 140,
        Frame = 2,
        Keel = 1,
        Bulwark = 1,
        BeamStep = 0,
        Masts = 2,
        MastH = 14,
        Sail = "set",
        SailW = 13,
        SailH = 11,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 2,
        CastleFore = 0,
        CastleLen = 25,
        Shell = "minecraft:jungle_planks",
        Deck = "minecraft:jungle_planks",
        Keelb = "minecraft:stripped_jungle_log",
        Frameb = "minecraft:jungle_log",
        Railb = "minecraft:jungle_planks",
        Fitb = "minecraft:stripped_jungle_log",
        Castleb = "minecraft:jungle_planks",
    };

    // 福州のジャンク「耆英号」（1846年に英国へ渡った交易船）は全長160ft＝48.8m・
    // 幅33ft＝10.1m・艙深12ft＝3.66m・800t級で、3本マストの最も高いものが92ft＝28m。
    // 竜骨を持たない平底で、肋骨の代わりに水密隔壁を並べるので断面は矩形寄り85。
    // 船首・船尾ともトランサムで、船尾は大きく反り上がって幅広の甲板になる。
    // 舵は船尾材の中心線に吊る昇降式の舵。帆は竹の桟を通した網代の帆で、
    // 縦に長い1枚になるのでマストを高く（24）取って帆の丈18を確保する。
    private static readonly HullPreset Junk = new()
    {
        Jp = "ジャンク船",
        Note = "耆英号は全長48.8m・幅10.1m・艙深3.66m・800t級。竜骨を持たない平底。",
        Len = 49,
        Beam = 10,
        Depth = 6,
        Draft = 3,
        Section = 85,
        Entry = 35,
        BowFull = 75,
        Run = 30,
        SternFull = 70,
        Transom = 60,
        Rake = 40,
        Rise = 3,
        Flare = 10,
        Tumble = 6,
        Sheer = 180,
        Frame = 3,
        Keel = 0,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 24,
        Sail = "set",
        SailW = 12,
        SailH = 18,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 4,
        CastleFore = 0,
        CastleLen = 25,
        Shell = "minecraft:spruce_planks",
        Deck = "minecraft:spruce_planks",
        Keelb = "minecraft:stripped_spruce_log",
        Frameb = "minecraft:spruce_log",
        Railb = "minecraft:dark_oak_planks",
        Sailb = "minecraft:brown_wool",
        Fitb = "minecraft:stripped_spruce_log",
        Castleb = "minecraft:dark_oak_planks",
    };

    // ピナスはオランダの小型全装帆船。ダイフケン号の復元船で船首材〜船尾材19.94m・
    // 最大幅6.01m・排水量110t級・3本マストで帆は6枚、カーベル張りの浅喫水。
    // 船尾の水面上高さは5.5mで、後方が高く反り上がる強いシア（160%）を持つ。
    // 舷側は上へ行くほど内へ絞るタンブルホームが顕著（12度）で、これがオランダ船の顔。
    // 船尾は狭い角形のタック、舵は中心線舵。船尾楼3に加えて低い船首楼2を持つ。
    private static readonly HullPreset Pinnace = new()
    {
        Jp = "ピナス",
        Note = "ダイフケン号は船首材〜船尾材19.94m・最大幅6.01m・110t級、3本マスト6枚帆。",
        Len = 20,
        Beam = 6,
        Depth = 4,
        Draft = 2,
        Section = 55,
        Entry = 20,
        BowFull = 50,
        Run = 30,
        SternFull = 60,
        Transom = 35,
        Rake = 35,
        Rise = 3,
        Flare = 15,
        Tumble = 12,
        Sheer = 160,
        Frame = 2,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 15,
        Sail = "set",
        SailW = 10,
        SailH = 9,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 3,
        CastleFore = 2,
        CastleLen = 25,
        Deck = "minecraft:oak_planks",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_oak_log",
        Castleb = "minecraft:oak_planks",
    };
}
