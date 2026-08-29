namespace ModSorter.Architect.Manual;

// 帆船（近代）の既定値。スループ・スクーナー・クリッパーの3種。
// 中世〜大航海の6種は HullPresets.Sail.cs と HullPresets.Discovery.cs にあり、
// 1ファイル9KBの目安に収めるため時代でもう1枚に分ける。
// 分けたのは値だけで、Of の switch は HullPresets.cs 1か所に残す。
//
// スループとスクーナーは縦帆（Sail="fore"）。帆はマストの後ろへ張り、下辺を
// ブーム・上辺をガフが持つ。実船のブームは船尾材より後ろへ張り出すが、
// 生成側は船尾材で切っている（外寸と生成物を食い違わせないため）。
internal static partial class HullPresets
{
    // ハドソン・リバー・スループ。復元船クリアウォーターは甲板長76ft＝23m・
    // 型幅25ft＝7.6m・喫水8ft＝2.4m、帆面積4,305sq ft＝400m²。
    // 河川の浅瀬を走る貨物船なので長さ/幅が3:1と幅広で、船底は平底寄り（断面75）。
    // 船尾は幅の広い角形トランサム45%。帆装は1本マストのガフ帆で、
    // マストは甲板上26m と船体長より高い。船楼は持たず甲板は平ら。
    private static readonly HullPreset Sloop = new()
    {
        Jp = "スループ",
        Note = "クリアウォーターは甲板長23m・型幅7.6m・喫水2.4m、1本マストのガフ帆。",
        Len = 23,
        Beam = 8,
        Depth = 4,
        Draft = 2,
        Section = 75,
        Entry = 22,
        BowFull = 55,
        Run = 30,
        SternFull = 60,
        Transom = 45,
        Rake = 20,
        Rise = 2,
        Flare = 14,
        Tumble = 4,
        Sheer = 110,
        Frame = 2,
        Keel = 1,
        Bulwark = 1,
        BeamStep = 0,
        Masts = 1,
        MastH = 26,
        Sail = "fore",
        SailW = 12,
        SailH = 16,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        Deck = "minecraft:oak_planks",
        Railb = "minecraft:oak_planks",
        Fitb = "minecraft:stripped_oak_log",
    };

    // グランドバンクスの漁業スクーナー。ブルーノーズ（1921）は全長143ft1in＝43.6m・
    // 水線長34.1m・型幅26ft11in＝8.2m・喫水15ft11in＝4.85m・258t。
    // 深い喫水と細い水線（入角12度）で風上へ切り上がる船型。船体は黒く塗る。
    // 帆装は2本マストの縦帆で、主檣は甲板上126ft＝38.4m・前檣102ft8in＝31.3m。
    // 現状の生成器は全マストを同じ高さで立てるので、両者の中間の34を取る。
    // 主ブームは実物24.7mだが船尾材より後ろへ出せないので船尾で切れる。
    private static readonly HullPreset Schooner = new()
    {
        Jp = "スクーナー",
        Note = "ブルーノーズは全長43.6m・水線長34.1m・型幅8.2m・喫水4.85m、258t。",
        Len = 44,
        Beam = 8,
        Depth = 6,
        Draft = 5,
        Section = 45,
        Entry = 12,
        BowFull = 30,
        Run = 35,
        SternFull = 45,
        Transom = 30,
        Rake = 40,
        Rise = 3,
        Flare = 18,
        Tumble = 6,
        Sheer = 130,
        Frame = 3,
        Keel = 2,
        Bulwark = 1,
        BeamStep = 0,
        Masts = 2,
        MastH = 34,
        Sail = "fore",
        SailW = 18,
        SailH = 22,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        Shell = "minecraft:dark_oak_planks",
        Deck = "minecraft:spruce_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:dark_oak_log",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_spruce_log",
    };

    // 茶クリッパー。カティサーク（1869）は船体長212.5ft＝64.77m・型幅36ft＝10.97m・
    // 艙深21ft＝6.40m、満載喫水20ftで排水量2,100t、帆32,000sq ft＝3,000m²。
    // 長さ/幅6:1・角柱係数0.628で、茶クリッパーのなかでも最も痩せた船体。
    // 水線は船首で内へ凹むので船首の肥え15・入角10度。船尾は角形の counter。
    // 全長は生成器の上限64マスにちょうど収まる。
    // 主檣は甲板上47m（スカイセイル桁44.5m）で主檣下桁は23.8m。帆桁が型幅の
    // 倍以上あるのは実物どおり。実物は1本のマストへ下から5段の横帆を張るが、
    // 現状の Sail.cs は1本1枚しか組めないので、丈30で段の合計を近似する。
    private static readonly HullPreset Clipper = new()
    {
        Jp = "クリッパー",
        Note = "カティサークは船体長64.77m・型幅10.97m・艙深6.40m、満載喫水6.1mで2,100t。",
        Len = 64,
        Beam = 11,
        Depth = 8,
        Draft = 6,
        Section = 40,
        Entry = 10,
        BowFull = 15,
        Run = 40,
        SternFull = 35,
        Transom = 30,
        Rake = 35,
        Rise = 3,
        Flare = 16,
        Tumble = 8,
        Sheer = 120,
        Frame = 3,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 44,
        Sail = "set",
        SailW = 24,
        SailH = 30,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        Shell = "minecraft:dark_oak_planks",
        Deck = "minecraft:oak_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:dark_oak_log",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_spruce_log",
    };
}
