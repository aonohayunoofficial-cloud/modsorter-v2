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
            // 必要に応じて拡充。
        };

    public static RotationSpec? GetRotation(string blockId)
        => Rotation.TryGetValue(blockId, out var s) ? s : null;

    // 加工物の取り出しに「隣接funnel→そのfunnelに隣接するstorage」が必要な機械。
    // crushing_wheels は排出位置が「2輪の隙間の真下」であって機械本体の隣接面ではない。
    // millstone型のfunnel検証(隣接funnel+真下storage)を当てると偽合格を生むため含めない。
    // crushing_wheels は専用検証(ペア存在・1マス間隔)で扱う。
    public static readonly HashSet<string> RequiresFunnelOutput =
        new(StringComparer.Ordinal)
        {
            "create:millstone",
        };

    public static bool IsFunnel(string id)
        => id is "create:andesite_funnel" or "create:brass_funnel"
              or "create:andesite_belt_funnel" or "create:brass_belt_funnel";

    public static bool IsItemStorage(string id)
        => id is "create:depot" or "create:item_vault" || id.Contains("chest");

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
