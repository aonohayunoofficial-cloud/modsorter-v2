using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ブロックの回転軸が「どこから決まるか」。
public enum AxisSource
{
    None,          // 回転接続を持たない
    AxisProperty,  // properties["axis"] から (shaft, cogwheel 等)
    FacingAxis,    // properties["facing"] を軸に変換 (water_wheel, hand_crank 等)
    FixedY,        // 常に y 軸 (millstone, press の縦動作など)
}

// 動力をどの面から入力できるか。面ごとに「許可する接続種別」を持つ。
public enum PowerInputKind
{
    None,      // その面からは動力入力不可
    ShaftOnly, // shaft を軸合わせで直結(同軸)
    CogOnly,   // cogwheel を噛み合わせ
    ShaftOrCog // どちらでも可
}

// 6方向。
public enum Dir { Up, Down, North, South, East, West }

public sealed class RotationSpec
{
    public string BlockId = "";
    public AxisSource AxisSource = AxisSource.None;
    public bool MeshesPerpendicular = false; // cogwheel 系: 軸に垂直で噛み合う
    public bool IsLargeCog = false;
    public bool IsSource = false;            // 動力源(自分で回転を生む)

    // 動力入力面の制約。指定が無い面は「制約なし(判定しない)」。
    // 機械(millstone/mixer 等)で「側面はcog、底面はshaft」のような縛りを表現する。
    public Dictionary<Dir, PowerInputKind>? PowerInputFaces;

    // press のように「facing軸に沿った両端2面だけがshaft動力入力で、残り4面は不可」
    // という動的な制約を表す。true のとき PowerInputFaces より優先して判定する。
    // 軸は GetRotationAxis(=facingから算出) を使う。
    public bool PowerOnAxisEnds = false;

    // deployer 専用。動力入力面は「facing に垂直な水平軸」の両端2面(縦置き=up/downのときは
    // axis_along_first が示す軸: true=x/東西, false=z/南北)。背面(facingの反対)は不可。
    // 実機準拠: facing=west のとき入力口は南北(z軸両端)。facing=up かつ axis_along_first=true のとき東西(x軸両端)。
    // その2面のいずれか1面に、その軸と同軸の shaft/cog があればOK。他の面は動力入力不可。
    // true のとき他の動力入力フラグより優先し、Validator の (A'''') で判定する。
    public bool IsDeployer = false;

    // mechanical_saw 専用。sawは実機で動力入力面がfacingで二分される:
    //  ・縦置き(facing=up/down)=加工モード → ブレード軸に直交する「両側面のいずれか1面」から
    //    shaftで入力(片側でOK)。動力軸は properties["axis_along_first"] で決まる
    //    (true=x/東西, false=z/南北)。flipped は動力に無関係。
    //  ・横向き(facing=north/south/east/west)=伐採モード → 背面1面(PowerOnBackOnly相当)。
    //    ただし加工ジャンルでは縦置きが正で、横向きは伐採(からくり)用途のため許可しない。
    // この分岐は PowerOnBackOnly では表現できないため専用フラグにする。
    // true のとき他の動力入力フラグより優先し、Validator の (A''') で判定する。
    public bool IsSaw = false;

    // steam_engine 専用。steam_engine は単体では動力を生まない(Create 0.5 実機仕様)。
    //  加熱した fluid_tank(ボイラー)の側面に取り付けて初めて動力を出し、
    //  取り付け面の反対側(背面=shaft_input)から動力を取り出す。
    //  ここでは最低条件として「隣接6方向に create:fluid_tank が1つ以上あること」を検証する
    //  (ボイラーの加熱段階・タンク数の妥当性までは検証しない=将来)。
    //  true のとき Validator の (A'') で判定する。
    public bool IsSteamEngine = false;

    // 動力入力の正しいやり方をLLMへ伝える具体文(再生成プロンプト用)。
    // 「どこにどの部材をどの向きで置けば動くか」を明記する。
    public string PowerInputHint = "";
}

public static class ConnectionCatalog
{
    // 回転接続を持つブロックの定義。
    public static readonly Dictionary<string, RotationSpec> Rotation =
        new(StringComparer.Ordinal)
        {
            ["create:shaft"] = new()
            {
                BlockId = "create:shaft",
                AxisSource = AxisSource.AxisProperty
            },
            ["create:cogwheel"] = new()
            {
                BlockId = "create:cogwheel",
                AxisSource = AxisSource.AxisProperty,
                MeshesPerpendicular = true
            },
            ["create:large_cogwheel"] = new()
            {
                BlockId = "create:large_cogwheel",
                AxisSource = AxisSource.AxisProperty,
                MeshesPerpendicular = true,
                IsLargeCog = true
            },
            ["create:water_wheel"] = new()
            {
                BlockId = "create:water_wheel",
                AxisSource = AxisSource.FacingAxis,
                IsSource = true
            },
            ["create:large_water_wheel"] = new()
            {
                BlockId = "create:large_water_wheel",
                AxisSource = AxisSource.FacingAxis,
                IsSource = true
            },
            ["create:windmill_bearing"] = new()
            {
                BlockId = "create:windmill_bearing",
                AxisSource = AxisSource.FacingAxis,
                IsSource = true
            },
            ["create:hand_crank"] = new()
            {
                BlockId = "create:hand_crank",
                AxisSource = AxisSource.FacingAxis,
                IsSource = true
            },
            // steam_engine: 動力源だが単体では動かない。加熱した fluid_tank(ボイラー)の側面に
            //  取り付け、背面(shaft_input)から動力を取り出す。IsSource だが Validator (A'') で
            //  隣接 fluid_tank の有無を追加検証する。
            ["create:steam_engine"] = new()
            {
                BlockId = "create:steam_engine",
                AxisSource = AxisSource.FacingAxis,
                IsSource = true,
                IsSteamEngine = true,
                PowerInputHint =
                    "steam_engineは単体では動力を出さない。加熱したcreate:fluid_tank(ボイラー)の側面に" +
                    "取り付け、その反対側(背面=shaft_input)にcreate:shaftを挿して動力を取り出す。" +
                    "fluid_tankは火/溶岩/ブレイズバーナー等で加熱する。ボイラー無しのsteam_engine単体は動かない。"
            },
            // millstone: 上面=動力不可(アイテム投入), 側面=cogのみ, 底面=shaft可。
            ["create:millstone"] = new()
            {
                BlockId = "create:millstone",
                AxisSource = AxisSource.FixedY,
                PowerInputFaces = new()
                {
                    [Dir.Up] = PowerInputKind.None,
                    [Dir.Down] = PowerInputKind.ShaftOnly,
                    [Dir.North] = PowerInputKind.CogOnly,
                    [Dir.South] = PowerInputKind.CogOnly,
                    [Dir.East] = PowerInputKind.CogOnly,
                    [Dir.West] = PowerInputKind.CogOnly,
                },
                PowerInputHint =
                    "millstoneの動力は、側面(north/south/east/west)のいずれかにcreate:cogwheel(axis=y)を" +
                    "噛み合わせるか、底面(下)にcreate:shaft(axis=y)を縦に挿す。上面はアイテム投入口なので動力不可。"
            },
            // mechanical_press: facingで軸が決まる(facing=south→z軸)。
            //  動力入力はfacing軸の両端2面のみ(shaft/cog可)。残り4面は不可。
            //  実機: facingとその反対の2側面にshaftが繋がる。
            ["create:mechanical_press"] = new()
            {
                BlockId = "create:mechanical_press",
                AxisSource = AxisSource.FacingAxis,
                PowerOnAxisEnds = true,
                PowerInputHint =
                    "pressの動力は、facingの向きとその反対の2側面(facing軸の両端)にcreate:shaftを" +
                    "press同じ軸で挿す(facing=south/northなら相手はaxis=z、facing=east/westならaxis=x)。" +
                    "上面・下面やfacingに垂直な側面には繋がらない。"
            },
            // mechanical_mixer: 向きを持たない(プロパティなし)。軸はy固定。
            //  動力は側面4面からcogで入力(millstoneと同型)。上面=basin連動部, 下面=basin方向で不可。
            ["create:mechanical_mixer"] = new()
            {
                BlockId = "create:mechanical_mixer",
                AxisSource = AxisSource.FixedY,
                PowerInputFaces = new()
                {
                    [Dir.Up] = PowerInputKind.None,
                    [Dir.Down] = PowerInputKind.None,
                    [Dir.North] = PowerInputKind.CogOnly,
                    [Dir.South] = PowerInputKind.CogOnly,
                    [Dir.East] = PowerInputKind.CogOnly,
                    [Dir.West] = PowerInputKind.CogOnly,
                },
                PowerInputHint =
                    "mixerの動力は、側面(north/south/east/west)のいずれかにcreate:cogwheel(axis=y)を" +
                    "噛み合わせる。上面・下面には動力を繋げない(上は本体、下はbasin方向)。" +
                    "mixerは向きプロパティを持たないので置くだけでよい。"
            },
            // crushing_wheel: axis が回転軸。動力は axis 端(同軸方向の隣)に shaft/cog を同軸で挿す。
            //  2個1組で「軸に垂直な水平方向」へ1マス離して並べる(専用検証 (C-0) で間隔/受けを確認)。
            //  RotationSpec が無いと (C-0) の GetRotationAxis が null になり軸計算が崩れるため必須。
            ["create:crushing_wheel"] = new()
            {
                BlockId = "create:crushing_wheel",
                AxisSource = AxisSource.AxisProperty,
                PowerInputHint =
                    "crushing_wheelの動力は、axisが示す回転軸の端(同軸方向の隣)にcreate:shaftを" +
                    "同じaxisで挿すか、cogwheelを同軸で噛み合わせる。axisに垂直な側面にshaftを置いても繋がらない。" +
                    "2個を軸に垂直な水平方向へ1マス離して並べ、両方を逆回転で駆動する。"
            },
            // mechanical_saw: facing は刃(のこぎり)の向き。動力入力面は実機で二分される。
            //  【加工モード=縦置き(facing=up/down)】動力はブレード軸に直交する「両側面のいずれか1面」
            //   にshaftを同軸で挿す(片側でOK)。動力軸は axis_along_first で決まる(true=x, false=z)。
            //   flipped は動力に無関係。加工ジャンルではこの縦置きが正。
            //  【伐採モード=横向き(facing=north/south/east/west)】動力は背面1面。ただし伐採(からくり)
            //   用途であり加工ジャンルでは使わない。
            //  この分岐は PowerOnBackOnly では表せないため IsSaw 専用判定((A''')で処理)を使う。
            //  出力(加工品は刃の端から飛ぶ)の受けは強制しない(用途で向きが変わるためプロンプト誘導のみ)。
            ["create:mechanical_saw"] = new()
            {
                BlockId = "create:mechanical_saw",
                AxisSource = AxisSource.FacingAxis,
                IsSaw = true,
                PowerInputHint =
                    "mechanical_sawは加工用途では縦置き(facing=up)にする。動力はブレード軸に直交する" +
                    "両側面のどちらか一方にcreate:shaftを同軸で挿す(片側でOK)。動力軸はaxis_along_firstで決まり、" +
                    "axis_along_first=trueならx軸(東西)、falseならz軸(南北)。flipped は動力に関係ない。" +
                    "横向き(facing=north/south/east/west)は前方を伐採するモードで、加工には使わない。"
            },
            // deployer: 動力軸は axis_along_first で決まる。その軸の両端2面のいずれか1面に同軸shaft。
            //  axis_along_first=true → 垂直(axis=y, 上下の両端)。
            //  axis_along_first=false → facingに垂直な水平軸(facing=east/west→z/南北, north/south→x/東西)。
            //  片側1面に同軸shaftがあればOK。前面(作用面)・動力軸に沿わない面は不可。作用は facing 先の2マス目。
            //  作用先(depot/belt等)の受けは強制しない(用途で変わるためプロンプト誘導のみ)。
            ["create:deployer"] = new()
            {
                BlockId = "create:deployer",
                AxisSource = AxisSource.FacingAxis,
                IsDeployer = true,
                PowerInputHint =
                    "deployerの動力軸はaxis_along_firstで決まる。" +
                    "axis_along_first=trueなら垂直(axis=y)で、上面か下面のどちらか一方にcreate:shaft(axis=y)を挿す。" +
                    "axis_along_first=falseならfacingに垂直な水平軸で、facing=east/west(X向き)なら南北(axis=z)の" +
                    "north面かsouth面、facing=north/south(Z向き)なら東西(axis=x)のeast面かwest面のどちらか一方に" +
                    "create:shaftをその軸で挿す。shaftの軸は入力面の軸と一致させる(片側だけで回る)。" +
                    "前面(作用面)や動力軸に沿わない面には繋がらない。作用は facing の向きの2マス先(1マス先は貫通する)。"
            },
            // 必要に応じて拡充。
        };

    public static RotationSpec? GetRotation(string blockId)
        => Rotation.TryGetValue(blockId, out var s) ? s : null;

    // 加工物の取り出しに「隣接funnel→そのfunnelに隣接するstorage」が必要な機械。
    // crushing_wheel(単数IDが正)は排出位置が「2輪の隙間の真下」であって本体の隣接面ではない。
    // millstone型のfunnel検証(隣接funnel+真下storage)を当てると偽合格を生むため含めない。
    // crushing_wheel は専用検証(ペア存在・1マス間隔・軸端動力・隙間真下の保管庫)で扱う。
    public static readonly HashSet<string> RequiresFunnelOutput =
        new(StringComparer.Ordinal)
        {
            "create:millstone",
        };

    public static bool IsFunnel(string id)
        => id is "create:andesite_funnel" or "create:brass_funnel"
              or "create:andesite_belt_funnel" or "create:brass_belt_funnel";

    public static bool IsItemStorage(string id)
        => id is "create:depot" or "create:item_vault" or "minecraft:barrel"
           || id.Contains("chest");

    // crushing_wheel の受けに使える「貯められる保管庫」。
    //  depot は1個しか持てず連続排出で詰まるため除外する(depotは加工台・belt終点用)。
    public static bool IsBulkStorage(string id)
        => id is "create:item_vault" or "minecraft:barrel" || id.Contains("chest");

    // steam_engine のボイラーに使える流体タンク。
    //  実機で確認済みは create:fluid_tank のみ。加熱すると内部でボイラー化し、
    //  側面の steam_engine に蒸気圧を供給する。
    public static bool IsBoilerTank(string id)
        => id is "create:fluid_tank";

    // 方向 → facing のブロックステート文字列。
    public static string DirToFacing(Dir d) => d switch
    {
        Dir.Up => "up",
        Dir.Down => "down",
        Dir.North => "north",
        Dir.South => "south",
        Dir.East => "east",
        _ => "west",
    };

    // facing → 軸。
    public static string? FacingToAxis(string facing) => facing switch
    {
        "east" or "west" => "x",
        "up" or "down" => "y",
        "south" or "north" => "z",
        _ => null
    };

    // 軸 → その軸方向の2方向。
    public static (Dir a, Dir b) AxisToDirs(string axis) => axis switch
    {
        "x" => (Dir.East, Dir.West),
        "y" => (Dir.Up, Dir.Down),
        _ => (Dir.North, Dir.South), // z
    };

    // 方向の単位ベクトル。
    public static (int dx, int dy, int dz) DirToVec(Dir d) => d switch
    {
        Dir.Up => (0, 1, 0),
        Dir.Down => (0, -1, 0),
        Dir.North => (0, 0, -1),
        Dir.South => (0, 0, 1),
        Dir.East => (1, 0, 0),
        _ => (-1, 0, 0), // West
    };

    // ある方向から核を見たとき、その隣接ブロックが核に対して持つ「面」。
    // 例: 核の East 隣にあるブロックは、核の East 面に接している。
    public static Dir OppositeDir(Dir d) => d switch
    {
        Dir.Up => Dir.Down,
        Dir.Down => Dir.Up,
        Dir.North => Dir.South,
        Dir.South => Dir.North,
        Dir.East => Dir.West,
        _ => Dir.East
    };

    // ブロックの回転軸を求める。回転接続なし/判定不能なら null。
    public static string? GetRotationAxis(ModSorter.Clients.ModuleGenerator.PlacedBlock b)
    {
        var spec = GetRotation(b.Id);
        if (spec == null) return null;
        switch (spec.AxisSource)
        {
            case AxisSource.AxisProperty:
                return b.Properties != null && b.Properties.TryGetValue("axis", out var a) ? a : null;
            case AxisSource.FacingAxis:
                return b.Properties != null && b.Properties.TryGetValue("facing", out var f)
                    ? FacingToAxis(f) : null;
            case AxisSource.FixedY:
                return "y";
            default:
                return null;
        }
    }
}
