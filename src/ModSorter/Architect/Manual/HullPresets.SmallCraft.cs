namespace ModSorter.Architect.Manual;

// 小型艇の既定値。手漕ぎボート・モーターボートの2種。
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

    // バートラム46コンバーチブル（1971〜87）。全長46ft6in＝14.17m・型幅16ft＝4.88m・
    // 喫水4ft6in＝1.37m・重量44,900lb＝20.4t、船底の後部デッドライズ19度のディープV。
    // コックピットは117sq ft＝10.9m²、ブリッジクリアランス15ft6in＝4.72m
    // （HMY Powerboat Guide）。レイ・ハント設計のディープV滑走船型を大型化した
    // 系譜のスポーツフィッシャーマンで、船尾にコックピット・その前に操舵室（サロン）・
    // 上にフライブリッジが載る。深いVなのでビームシーでの横揺れは浅いVより大きい
    // （David Pascoe の実走評）。
    //
    // 1マス=1m での丸め:
    //   全長14.17m→14、型幅4.88m→5、喫水1.37m→1。
    //   深さは喫水1.37m＋乾舷1.4m級＝2.8m→3。甲板は y=3、水線は y=1。
    //   断面15は k=1.16 で直線V寄り。滑走艇の断面は排水量型の丸ビルジ（k=2・断面40）
    //   より小さい側で、コグ船の平底（断面90）とは逆向きになる。
    //   船尾の立ち上がりは0。滑走艇の船底はトランサムまで直線で走るので、
    //   排水量型のように船尾で船底が上がらない。竜骨の張り出しも0（FRPの一体成形で
    //   外板より下へ出る竜骨を持たない）。
    //   トランサム75%＝3.66mで、生成物の船尾は幅3マス（1.875×2＝3.75m）。
    // 舷墻1はコックピットのコーミング0.7m級に当たる。船尾の station は横へ通して
    // 塞ぐので、そこがトランサム上端になる。
    // デッキハウスは1層・前後長36%。全長14に対し箱は5マスで z=4〜8 を占め、
    // 船尾側の z=1〜3 が幅3マス×長さ3マス＝9m²のコックピット（実物10.9m²）、
    // 船首側の z=9〜13 が前甲板になる。箱の屋根は y=6＝水面上5mで、実物の
    // クリアランス4.72m に合う。2層にすると天端が y=9＝水面上8mになり、
    // 1層3マス固定（HouseFloorH）のため実物の倍近くになるので取らない。
    // 戸口は箱の船尾側の面に開くので、実物と同じくサロンからコックピットへ出る。
    // マスト・帆・砲門・貨物艙・船楼・盾は持たない。
    // 舵とスクリューは水線下なので未再現（推進器は12船種の共通部品として別に作る）。
    // 外板はゲルコートの白（白コンクリート）、甲板はノンスキッドの薄灰、
    // 金物はステンレス（鉄ブロック）、窓はガラス板。
    private static readonly HullPreset Motorboat = new()
    {
        Jp = "モーターボート",
        Note = "バートラム46コンバーチブルは全長14.17m・型幅4.88m・喫水1.37m、"
             + "船底19度のディープV、コックピット10.9m²。1マス=1m。",
        Len = 14,
        Beam = 5,
        Depth = 3,
        Draft = 1,
        Section = 15,
        Entry = 25,
        BowFull = 40,
        Run = 25,
        SternFull = 80,
        Transom = 75,
        Rake = 25,
        Rise = 0,
        Flare = 20,
        Tumble = 0,
        Sheer = 90,
        Frame = 2,
        Keel = 0,
        Bulwark = 1,
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
        HouseDecks = 1,
        HouseLen = 36,
        HouseShift = 0,
        Funnel = 0,
        Holds = 0,
        Derrick = false,
        Shell = "minecraft:white_concrete",
        Deck = "minecraft:light_gray_concrete",
        Keelb = "minecraft:white_concrete",
        Frameb = "minecraft:light_gray_concrete",
        Railb = "minecraft:white_concrete",
        Mastb = "minecraft:iron_block",
        Fitb = "minecraft:iron_block",
        Castleb = "minecraft:white_concrete",
        Glassb = "minecraft:glass_pane",
    };
}
