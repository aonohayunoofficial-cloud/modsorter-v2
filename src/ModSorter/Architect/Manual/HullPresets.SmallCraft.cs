namespace ModSorter.Architect.Manual;

// 小型艇の既定値。手漕ぎボート1種。
// 分けたのは値だけで、Of の switch は HullPresets.cs 1か所に残す。
internal static partial class HullPresets
{
    // イギリス海軍の 32ft カッター。ブライトリングシー建造（1942）の "Minion"
    // （メドウェイ海事トラスト所蔵）が全長32ft＝9.75m・型幅9ft＝2.74m。
    // 端艇のうちカッターは船尾がトランサム、ホエラー（27ft）は前後とも尖った
    // ダブルエンダーで、ギグ（30ft）は長さに対して細い。ここはカッターを取る。
    //
    // 1マス=1m での丸め:
    //   全長9.75m→10、型幅2.74m→3。型幅3が櫂と漕ぎ座が成り立つ最小で、
    //   2では Form.Span が竜骨の1列に落ちて内法が消える。
    //   深さは実艇1.1m級だが Form の下限が2なので2を取る。喫水0.6m→1。
    //   実艇の肋骨は0.25m間隔だがフレームは2マスへ丸める（hull-common.md の既定）。
    //   漕ぎ座は6つで間隔1.1m級だが、1マス=1mで隣接させると座が連なって甲板と
    //   見分けが付かないので2へ丸め、4つになる。
    // 甲板は張らない開放艇。最上列の外板が舷縁（ガンネル）を兼ねるので舷墻は0。
    // 櫂は12挺（片舷6）。1つの座に左右1挺ずつを配る二段掛け（double-banked）。
    // 乾舷が1マスしかないので櫂は舷の外へ1マスだけ出て水面の手前で止まる。
    // 帆走型（ディッピングラグの2本檣）もあるが、小分類は手漕ぎボートなので
    // マスト0・帆なしを既定にする。マストのスライダーを上げれば帆走型になる。
    // 舵は船尾のトランサムに吊る中心線舵。
    private static readonly HullPreset Rowboat = new()
    {
        Jp = "手漕ぎボート",
        Note = "イギリス海軍32ftカッターは全長9.75m・型幅2.74m・櫂12挺（片舷6）。1マス=1m。",
        Len = 10,
        Beam = 3,
        Depth = 2,
        Draft = 1,
        Section = 40,
        Entry = 20,
        BowFull = 30,
        Run = 30,
        SternFull = 45,
        Transom = 40,
        Rake = 20,
        Rise = 1,
        Flare = 15,
        Tumble = 0,
        Sheer = 150,
        Frame = 2,
        Keel = 1,
        Bulwark = 0,
        BeamStep = 0,
        OpenBoat = true,
        ThwartStep = 2,
        Masts = 0,
        MastH = 6,
        Sail = "none",
        SailW = 4,
        SailH = 4,
        Shields = 0,
        Oar = false,
        SternRudder = true,
        Head = "none",
        CastleAft = 0,
        CastleFore = 0,
        CastleLen = 20,
        GunRows = 0,
        GunStep = 0,
        GunBase = 1,
        RowOars = 6,
        HouseDecks = 0,
        Holds = 0,
        Derrick = false,
        Shell = "minecraft:birch_planks",
        Deck = "minecraft:dark_oak_planks",
        Keelb = "minecraft:stripped_dark_oak_log",
        Frameb = "minecraft:oak_planks",
        Railb = "minecraft:dark_oak_planks",
        Mastb = "minecraft:stripped_spruce_log",
        Fitb = "minecraft:spruce_planks",
    };
}
