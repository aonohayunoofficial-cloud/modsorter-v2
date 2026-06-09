using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// プリミティブ（曲面・造形物）の中間表現。家のStructureSpecとは別系統。
// 座標展開は PrimitiveExpander が確定的に行う。
public sealed class PrimitiveSpec
{
    // 形状の種類: "sphere" | "ellipsoid"（将来 "cylinder" 等を追加可能）
    [JsonPropertyName("shape")] public string? Shape { get; set; }

    // 三軸の半径（ブロック数）。sphere の場合は3つを同じ値にすればよい。
    [JsonPropertyName("radius_x")] public int RadiusX { get; set; } = 4;
    [JsonPropertyName("radius_y")] public int RadiusY { get; set; } = 4;
    [JsonPropertyName("radius_z")] public int RadiusZ { get; set; } = 4;

    // 中空にするか。true なら殻だけ（内部をくり抜く）。
    [JsonPropertyName("hollow")] public bool Hollow { get; set; }

    // 使用ブロック（許可リストのいずれか）。未指定なら先頭ブロック。
    [JsonPropertyName("block")] public string? Block { get; set; }
}
