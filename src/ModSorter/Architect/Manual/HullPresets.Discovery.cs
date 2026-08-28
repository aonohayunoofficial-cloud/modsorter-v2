namespace ModSorter.Architect.Manual;

// 帆船（中世〜大航海）の既定値のうち、キャラベル・キャラック・ガレオンの3種。
// HullPresets.Sail.cs（ダウ船・ジャンク船・ピナス）が約5KBあり、3種を足すと
// 1ファイル9KBの目安を超えるのでもう1枚に分ける。分けたのは値だけで、
// Of の switch は HullPresets.cs 1か所に残す。
//
// 3種とも船首楼が船首材より前へ張り出し、ガレオンは長い船首の張り出し
// （ビークヘッド）を持つが、現状の BuildCastle・PutHead は z 方向へ出せない。
// 前へ突き出す工作は Castle.cs / Rig.cs 側の課題として残している。
internal static partial class HullPresets
{
    // キャラベラ・レドンダ。ピンタ号は甲板長17m・型幅5.36m・60〜70t級で、
    // 竜骨長13m・深さ2m。船体が浅く軽いので喫水は2m弱。
    // 名の通りカーベル張り（外板を突き合わせる平滑張り）で断面50。
    // 探検用の小型船なので船首楼を持たず、船尾に低い操舵甲板を1つ載せるだけ。
    // 帆装は3本マストで、前2本が横帆・後ろ1本がラティーンの混成。
    // 横帆は1枚が小さいので帆は幅8×丈9に収める。
    private static readonly HullPreset Caravel = new()
    {
        Jp = "キャラベル",
        Note = "ピンタ号は甲板長17m・型幅5.36m・竜骨長13m・深さ2m、60〜70t級。",
        Len = 17,
        Beam = 5,
        Depth = 3,
        Draft = 2,
        Section = 50,
        Entry = 16,
        BowFull = 45,
        Run = 30,
        SternFull = 55,
        Transom = 30,
        Rake = 30,
        Rise = 2,
        Flare = 14,
        Tumble = 6,
        Sheer = 130,
        Frame = 2,
        Keel = 1,
        Bulwark = 1,
        BeamStep = 0,
        Masts = 3,
        MastH = 13,
        Sail = "set",
        SailW = 8,
        SailH = 9,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 2,
        CastleFore = 0,
        CastleLen = 25,
        Deck = "minecraft:oak_planks",
        Railb = "minecraft:oak_planks",
        Fitb = "minecraft:stripped_oak_log",
        Castleb = "minecraft:oak_planks",
    };

    // キャラック（ナオ）。復元されたナオ・サンタマリア号は全長93ft＝28.3m・
    // 型幅26ft＝7.9m・200t級。船体が太く（長さ/幅3.5:1）貨物を積むための
    // 深い船倉を持つので深さ6・喫水3。断面は丸みのある65。
    // 船首楼が船首材の上へ高くせり上がり、船尾楼はさらに高い2層で、
    // 舷側は上へ行くほど内へ絞る（タンブルホーム10度）。この前後の塔が
    // キャラックの顔なので船首楼4・船尾楼5、シアは220%と強い。
    // 船尾下部は丸いタックなのでトランサムは小さく25%。舵は中心線舵。
    private static readonly HullPreset Carrack = new()
    {
        Jp = "キャラック",
        Note = "ナオ・サンタマリア号は全長28.3m・型幅7.9m・200t級。前後に高い楼を持つ。",
        Len = 28,
        Beam = 8,
        Depth = 6,
        Draft = 3,
        Section = 65,
        Entry = 28,
        BowFull = 70,
        Run = 30,
        SternFull = 60,
        Transom = 25,
        Rake = 45,
        Rise = 4,
        Flare = 18,
        Tumble = 10,
        Sheer = 220,
        Frame = 3,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 18,
        Sail = "set",
        SailW = 11,
        SailH = 12,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 5,
        CastleFore = 4,
        CastleLen = 30,
        Deck = "minecraft:oak_planks",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_oak_log",
        Castleb = "minecraft:dark_oak_planks",
    };

    // ガレオン。ゴールデン・ハインド号は甲板長102ft＝31m・型幅20ft＝6.1m・
    // 喫水13ft＝4m・100〜150t（排水量300t）。キャラックの太い船体を細長く
    // 引き伸ばした形（長さ/幅5:1）で、これが速さと凌波性の違いになる。
    // 船首楼はキャラックのように船首材の上へ盛り上げず、後ろへ下げて低くし
    // （船首楼3）、代わりに前へ長い張り出しを出す。船尾は角形で高く（船尾楼5）、
    // タンブルホーム14度は3種でいちばん強い。3本マストで帆装は横帆＋ラティーン。
    private static readonly HullPreset Galleon = new()
    {
        Jp = "ガレオン",
        Note = "ゴールデン・ハインド号は甲板長31m・型幅6.1m・喫水4m、100〜150t級。",
        Len = 31,
        Beam = 6,
        Depth = 6,
        Draft = 4,
        Section = 55,
        Entry = 15,
        BowFull = 45,
        Run = 25,
        SternFull = 50,
        Transom = 40,
        Rake = 50,
        Rise = 4,
        Flare = 12,
        Tumble = 14,
        Sheer = 190,
        Frame = 3,
        Keel = 1,
        Bulwark = 2,
        BeamStep = 0,
        Masts = 3,
        MastH = 20,
        Sail = "set",
        SailW = 11,
        SailH = 13,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 5,
        CastleFore = 3,
        CastleLen = 30,
        Shell = "minecraft:dark_oak_planks",
        Deck = "minecraft:oak_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:dark_oak_log",
        Railb = "minecraft:dark_oak_planks",
        Fitb = "minecraft:stripped_dark_oak_log",
        Castleb = "minecraft:dark_oak_planks",
    };
}
