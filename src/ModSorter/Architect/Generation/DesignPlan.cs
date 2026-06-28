using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 2パス生成の1パス目（計画フェーズ）の出力。
// 指示から「設計方針」を言葉＋軽い構造で受け取り、2パス目の SPEC 生成プロンプトに添える。
// ここでは座標も最終 SPEC も作らない。あくまで方針のメモ。
public sealed class DesignPlan
{
    // 設計方針を人間語でまとめた文章（階数・様式・屋根・装飾・開口の考え方など）。
    // 2パス目プロンプトにそのまま添える主役。
    [JsonPropertyName("design_notes")] public string DesignNotes { get; set; } = "";

    // 以下は方針を補強する軽い構造値（任意）。空/未設定でも 2パス目は notes だけで動く。
    [JsonPropertyName("stories")] public int? Stories { get; set; }          // 階数の方針
    [JsonPropertyName("style")] public string? Style { get; set; }           // 様式（walled/colonnade/temple 等の意図）
    [JsonPropertyName("roof")] public string? Roof { get; set; }             // 屋根の方針（flat/gable/gable_stairs/dome）
    [JsonPropertyName("decoration")] public string? Decoration { get; set; } // 装飾の方針（柱・基礎・素材コントラスト等）
    [JsonPropertyName("openings")] public string? Openings { get; set; }     // 開口の方針（ドア/窓の配置方針）
}
