using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// モデルが吐く中間表現。座標ではなく「設計意図」だけを持つ。
// 座標への展開は StructureExpander が確定的に行う。
public sealed class StructureSpec
{
    [JsonPropertyName("width")] public int Width { get; set; }  // x方向 W
    [JsonPropertyName("depth")] public int Depth { get; set; }  // z方向 D
    [JsonPropertyName("height")] public int Height { get; set; }  // y方向 H

    // 各面の素材（許可ブロックIDのいずれか）。未指定時は wall_block を流用。
    [JsonPropertyName("floor_block")] public string? FloorBlock { get; set; }
    [JsonPropertyName("roof_block")] public string? RoofBlock { get; set; }
    [JsonPropertyName("wall_block")] public string? WallBlock { get; set; }

    // 屋根の形: "flat"（平屋根・既定） または "gable"（切妻・三角）
    [JsonPropertyName("roof_type")] public string? RoofType { get; set; }

    // gable のときの棟の向き: "x"（棟がx軸に平行・z方向に傾斜） または "z"
    [JsonPropertyName("ridge_axis")] public string? RidgeAxis { get; set; }

    // 中間床を入れる高さ(y)のリスト。例: [3] なら y=3 に2階の床。複数指定で3階建て以上。
    // 1階の床(y=0)と屋根は別管理なので、ここには中間の階の床だけを入れる。
    [JsonPropertyName("floor_levels")] public List<int> FloorLevels { get; set; } = new();

    // 柱型リズム（pilaster）用のアクセント材。未指定なら wall_block と同じ＝柱が目立たない。
    // 例: 壁が oak_planks のとき accent_block を oak_log にすると柱だけ丸太になる。
    [JsonPropertyName("accent_block")] public string? AccentBlock { get; set; }

    // 柱を立てる間隔。2以上で有効、未指定/1以下なら柱なし（角だけは accent になる）。
    // 例: 3 なら外周に沿って3マスごとに柱を立てる。
    [JsonPropertyName("pilaster_step")] public int? PilasterStep { get; set; }

    // 開口部（窓・ドア）。面と面内の相対位置で指定する。
    [JsonPropertyName("openings")] public List<Opening> Openings { get; set; } = new();

}

// 開口部1つ。座標ではなく「どの面の、どのあたりか」で表す。
public sealed class Opening
{
    // "north" | "south" | "east" | "west"
    [JsonPropertyName("face")] public string Face { get; set; } = "";

    // "window" | "door"
    [JsonPropertyName("kind")] public string Kind { get; set; } = "window";

    // 面に沿った位置（端=0 から数えた何番目か）。中央寄りなら W/2 や D/2 あたり。
    [JsonPropertyName("offset")] public int Offset { get; set; }

    // 下から何段目か（床のすぐ上=1）。door は通常1、window は1〜2あたり。
    [JsonPropertyName("level")] public int Level { get; set; } = 1;

    // 窓に使うブロック（kind=window のとき）。未指定なら glass。
    [JsonPropertyName("block")] public string? Block { get; set; }
}
