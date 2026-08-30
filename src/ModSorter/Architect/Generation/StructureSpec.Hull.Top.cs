using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 手動生成の船体（structure_type="hull:<船種>"）の上部構造のプロパティ。
// StructureSpec の partial。素の船体の主要目・船型は StructureSpec.Hull.cs にある。
//
// 素材スロットの割り当て（宣言は StructureSpec.cs / StructureSpec.Ship.cs にある既存のもの）。
//   superstructure_block … マスト・帆桁
//   roof_block           … 帆
//   tower_block          … 盾（シールドラック）
//   seat_block           … 舵・舵柄・貫通横梁・船首材の飾り
//   hull_castle_block    … 船楼（未指定なら superstructure_block）
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

    // 盾の2枚目の素材。1枚おきにこちらを使う。未指定なら tower_block と同じ＝交互にならない。
    // ゴクスタ船の盾は黄と黒の交互に塗られていた。
    [JsonPropertyName("hull_shield_block_alt")] public string? HullShieldBlockAlt { get; set; }

    // ===== 貫通横梁（through-beam） =====
    // 梁の間隔（マス）。0でなし。1は2へ丸める。コグ船は外板を貫いて梁の木口が
    // 舷側の外へ突き出すので、舷の外へ左右1マスずつ出る。
    [JsonPropertyName("hull_beam_step")] public int? HullBeamStep { get; set; }

    // ===== 舵 =====
    // 中心線舵（船尾材に付く舵）。1200年頃以降のコグ船以降の標準。
    // クォーターラダー（hull_steering_oar）とは付く位置が違うので別に持つ。
    // 船尾材より後ろへ1マス出るので奥行きが1増える。
    [JsonPropertyName("hull_stern_rudder")] public bool? HullSternRudder { get; set; }

    // ===== 船楼（キャッスル） =====
    // 船尾楼の高さ（甲板・舷墻の上からのマス数）。0でなし。
    [JsonPropertyName("hull_castle_aft")] public int? HullCastleAft { get; set; }

    // 船首楼の高さ。0でなし。初期のコグ船は船尾楼だけを持つ。
    [JsonPropertyName("hull_castle_fore")] public int? HullCastleFore { get; set; }

    // 船楼の前後長（全長に対する%）。船尾楼・船首楼で共通。
    [JsonPropertyName("hull_castle_length")] public int? HullCastleLength { get; set; }

    // 船楼の素材。未指定なら superstructure_block。
    [JsonPropertyName("hull_castle_block")] public string? HullCastleBlock { get; set; }

    // ===== 砲門 =====
    // 砲門の段数。0でなし。フリゲートは1段、戦列艦は3段。上へ2マスおきに重ねる。
    [JsonPropertyName("hull_gun_rows")] public int? HullGunRows { get; set; }

    // 砲門の前後の間隔（マス・中心間）。0でなし。1は2へ丸める。
    // ヴィクトリーは下甲板片舷15門で12.4ft＝3.8m間隔。
    [JsonPropertyName("hull_gun_step")] public int? HullGunStep { get; set; }

    // 最下段の砲門の高さ（喫水線から上へのマス数）。
    // ヴィクトリーの下甲板は水面上4ft9in＝1.4m、フリゲートの砲甲板は2.4m級。
    [JsonPropertyName("hull_gun_base")] public int? HullGunBase { get; set; }

    // ===== 櫂 =====
    // 櫂の数（片舷）。0でなし。ガレーは漕ぎ座 片舷24。
    // 舷縁の外へ3マス出るので幅が左右6マス増える。
    [JsonPropertyName("hull_oar_per_side")] public int? HullOarPerSide { get; set; }
}
