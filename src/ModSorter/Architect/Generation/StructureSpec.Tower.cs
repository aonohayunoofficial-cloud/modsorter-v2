using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// StructureSpec の屋根上／平面内の突出物。塔屋（ペントハウス）と塔（鐘塔・尖塔）。
public sealed partial class StructureSpec
{
    // 塔屋（屋上の機械室・階段室）の平面寸法。3未満なら塔屋を作らない。
    [JsonPropertyName("penthouse_width")] public int? PenthouseWidth { get; set; }
    [JsonPropertyName("penthouse_depth")] public int? PenthouseDepth { get; set; }

    // 塔屋の高さ（屋根面から上へ何マス）。0/未指定で塔屋なし。
    // 対応する屋根形状は flat のみ（勾配屋根では無視される）。
    [JsonPropertyName("penthouse_height")] public int? PenthouseHeight { get; set; }

    // 塔屋の壁材。未指定なら wall_block を流用。天面は roof_block。
    [JsonPropertyName("penthouse_block")] public string? PenthouseBlock { get; set; }

    // 塔屋の寄せ方向。"center"（既定）| "north" | "south" | "east" | "west" |
    // "northeast" | "northwest" | "southeast" | "southwest"（4隅寄せ）。
    // north が z の小さい側、south が z の大きい側、west が x の小さい側、east が x の大きい側。
    // 展開側は文字列に north/south/east/west が含まれるかで x・z の寄せを独立に決めるため、
    // "north_east" のような区切り付きの表記でも同じ結果になる。
    [JsonPropertyName("penthouse_align")] public string? PenthouseAlign { get; set; }

    // ===== 塔（鐘塔・尖塔・ミナレット）=====
    // 建物の平面内に立てる正方形の塔の一辺（マス）。3未満/未指定なら塔なし。
    [JsonPropertyName("tower_width")] public int? TowerWidth { get; set; }

    // 塔の高さ（壁の上端 y=height-1 から塔の壁の上端まで何マス）。0/未指定なら塔なし。
    // penthouse と違い屋根形状を問わず作る。棟より低いと屋根に埋まる。
    [JsonPropertyName("tower_height")] public int? TowerHeight { get; set; }

    // 塔の位置。"front"（既定・正面の中央） | "front_corners"（正面の両角） |
    // "four_corners"（四隅） | "center"（平面の中央） | "rear"（背面の中央）。
    // 「正面」は facade_face で決まる。
    [JsonPropertyName("tower_align")] public string? TowerAlign { get; set; }

    // 塔の頂部の形。"spire"（既定・尖塔） | "dome"（丸屋根） | "flat"（陸屋根）。
    [JsonPropertyName("tower_roof")] public string? TowerRoof { get; set; }

    // 塔の壁材。未指定なら wall_block を流用。
    [JsonPropertyName("tower_block")] public string? TowerBlock { get; set; }

    // 塔の頂部の素材。未指定なら roof_block を流用。
    [JsonPropertyName("tower_roof_block")] public string? TowerRoofBlock { get; set; }

    // 鐘楼の開口を作るか。true で塔の上端付近の四面中央を抜く（tower_height が4以上のとき）。
    [JsonPropertyName("tower_belfry")] public bool TowerBelfry { get; set; }
}
