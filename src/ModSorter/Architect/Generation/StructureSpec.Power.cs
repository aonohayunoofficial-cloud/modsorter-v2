using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 発電所（structure_type="industry:<構造物>"）のプロパティ。StructureSpec の partial。
// 港湾・空港と同じく1基まるごとではなく、構成する単体構造物ごとに持つ。
public sealed partial class StructureSpec
{
    // ===== 箱物 共通（ボイラ建屋・タービン建屋・変電ヤード）=====
    // 長手方向（x）の長さ。
    [JsonPropertyName("power_length")] public int? PowerLength { get; set; }

    // 短手方向（z）の幅。
    [JsonPropertyName("power_width")] public int? PowerWidth { get; set; }

    // 高さ。変電ヤードでは門型（ガントリー）の高さ。
    [JsonPropertyName("power_height")] public int? PowerHeight { get; set; }

    // 柱間隔。鉄骨柱と採光帯の割り付けに使う。
    [JsonPropertyName("power_bay")] public int? PowerBay { get; set; }

    // 床の段数。1で運転床のみ、2以上で中間床が入る。
    [JsonPropertyName("power_levels")] public int? PowerLevels { get; set; }

    // 数量。煙突では内筒の本数、冷却塔では斜め柱の組数、変電ヤードでは回線数。
    [JsonPropertyName("power_count")] public int? PowerCount { get; set; }

    // ===== 円筒物 共通（煙突・冷却塔・格納容器）=====
    // 底部（格納容器では内径）の直径。
    [JsonPropertyName("power_diameter")] public int? PowerDiameter { get; set; }

    // 頂部の直径。
    [JsonPropertyName("power_top_diameter")] public int? PowerTopDiameter { get; set; }

    // 喉部（冷却塔の最小径）の直径。
    [JsonPropertyName("power_throat")] public int? PowerThroat { get; set; }

    // 空気取入口の高さ。冷却塔のシェル下端までの高さ。
    [JsonPropertyName("power_inlet")] public int? PowerInlet { get; set; }

    // 壁の厚み。格納容器の遮蔽壁。
    [JsonPropertyName("power_wall")] public int? PowerWall { get; set; }

    // 格納容器の形式。"cylinder"（PWR 円筒＋ドーム）| "box"（BWR 角形建屋）。
    [JsonPropertyName("power_shape")] public string? PowerShape { get; set; }

    // 変圧器の台数。
    [JsonPropertyName("power_transformers")] public int? PowerTransformers { get; set; }

    // ===== 付帯設備 =====
    // 天井クレーン。機器の吊り出しに使うので屋根直下に走行梁が通る。
    [JsonPropertyName("power_crane")] public bool PowerCrane { get; set; }

    // 壁上部の採光帯。
    [JsonPropertyName("power_louver")] public bool PowerLouver { get; set; }

    // 付属棟（管理・電気室）。
    [JsonPropertyName("power_annex")] public bool PowerAnnex { get; set; }

    // 外周フェンス。
    [JsonPropertyName("power_fence")] public bool PowerFence { get; set; }

    // 航空障害灯。地上高60m以上で必要になる。
    [JsonPropertyName("power_light")] public bool PowerLight { get; set; }

    // 点検はしごと踊り場。
    [JsonPropertyName("power_ladder")] public bool PowerLadder { get; set; }

    // 冷却塔の水盤（下部の貯水）。
    [JsonPropertyName("power_basin")] public bool PowerBasin { get; set; }

    // 機器搬入口（建屋の大扉、格納容器のエアロック）。
    [JsonPropertyName("power_gate")] public bool PowerGate { get; set; }
}
