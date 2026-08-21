using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 手動生成の船体（structure_type="hull:<船種>"）の上部構造のプロパティ。
// StructureSpec の partial。素の船体の主要目・船型は StructureSpec.Hull.cs にある。
//
// 素材スロットの割り当て（宣言は StructureSpec.cs / StructureSpec.Ship.cs にある既存のもの）。
//   superstructure_block … マスト・帆桁
//   roof_block           … 帆
//   tower_block          … 盾（シールドラック）
//   seat_block           … 舵・舵柄・船首材の飾り
public sealed partial class StructureSpec
{
    // マストの本数。0でマストなし。等間隔に立てる。
    [JsonPropertyName("hull_mast_count")] public int? HullMastCount { get; set; }

    // マストの高さ（甲板から上へのマス数）。未指定なら全長の半分。
    [JsonPropertyName("hull_mast_height")] public int? HullMastHeight { get; set; }

    // 帆の状態。"none" | "furled"（帆桁に畳んだ状態） | "set"（張った状態）。
    [JsonPropertyName("hull_sail")] public string? HullSail { get; set; }

    // 帆の幅。帆桁の長さを兼ねる。未指定ならマストの高さと同じ。
    [JsonPropertyName("hull_sail_width")] public int? HullSailWidth { get; set; }

    // 帆の丈。未指定ならマストの高さ-1。
    [JsonPropertyName("hull_sail_height")] public int? HullSailHeight { get; set; }

    // 盾掛けの盾の枚数（片舷）。0で盾掛けなし。船体中央から前後へ振り分ける。
    [JsonPropertyName("hull_shield_per_side")] public int? HullShieldPerSide { get; set; }

    // 舵（クォーターラダー）と舵柄。船尾の片舷に付く。
    [JsonPropertyName("hull_steering_oar")] public bool? HullSteeringOar { get; set; }

    // 船首材・船尾材の飾り。"none" | "spiral"（渦巻き・高さ3） | "dragon"（竜頭・高さ5）。
    [JsonPropertyName("hull_stem_head")] public string? HullStemHead { get; set; }
}
