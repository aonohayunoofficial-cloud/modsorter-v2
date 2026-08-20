using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// StructureSpec の構造タイプと開口部。Opening 型もここに置く。
public sealed partial class StructureSpec
{
    // 全体の構造タイプ。"building"（既定・通常の建物。床/壁/屋根/開口部のロジックを通す）
    // または特殊形状。特殊形状は床/壁/屋根/開口部を一切作らず、専用ビルダーが座標を作る。
    // "building"（既定） | "ramp"（スロープ） | "bridge"（橋） | "ship"（船） | "venue"（屋外会場）。
    // "ship" のときは ShipExpander が船体・甲板・上部構造物・出入口を確定的に作る。
    // "venue" のときは VenueExpander が観客席・フィールド・シェル・テントを確定的に作る。
    // "civic:" で始まるときは PublicFacilityExpander が公共施設（体育館・病院・消防署・
    // 市庁舎）を確定的に作る。接頭辞の後ろの語で施設種別を分岐する。
    // "harbor:" で始まるときは HarborExpander が港湾の単体構造物（岸壁 "quay"・桟橋 "pier"・
    // 防波堤 "breakwater"）を確定的に作る。断面は下の harbor_* から組むので width だけを
    // 延長として使い、depth/height は参照しない。
    // 通常の開口部/入口保証は通さない（出入口はそれぞれのビルダーが自動配置する）。
    [JsonPropertyName("structure_type")] public string? StructureType { get; set; }

    // 入口の自動生成を止めるか。true で「door が1つも無ければ正面中央に1つ開ける」保証を
    // 通さない。記念碑・オベリスク・台座のように穴を開けてはいけない塊のための指定。
    // openings に明示したドア・アーチ・大開口は true でもそのまま適用される。
    [JsonPropertyName("no_entrance")] public bool NoEntrance { get; set; }

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

    // 開口の横幅（マス）。kind="gate"（大型シャッター/搬入口）のとき有効。0以下なら3。
    [JsonPropertyName("width")] public int Width { get; set; }

    // 開口の縦高さ（マス）。kind="gate" のとき有効。0以下なら3。
    [JsonPropertyName("height")] public int Height { get; set; }

    // 窓に使うブロック（kind=window のとき）。未指定なら glass。
    [JsonPropertyName("block")] public string? Block { get; set; }
}
