using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// LLMが返す1ブロック（座標 + ID）
public sealed class GeneratedBlock
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("z")] public int Z { get; set; }
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}

// LLM応答のルート（{ "blocks": [ ... ] }）
public sealed class GeneratedStructure
{
    [JsonPropertyName("blocks")] public List<GeneratedBlock> Blocks { get; set; } = new();
}

// 生成1回分の結果（成功/失敗を呼び出し側で扱いやすくまとめる）
public sealed class GenerationResult
{
    public List<GeneratedBlock>? Blocks { get; set; }  // null = 失敗
    public string? RawResponse { get; set; }            // 生出力（デバッグ用）
    public string Error { get; set; } = "";

    // 色マッチのデバッグ集計(ブロックID別の個数と平均RGB)。null可。
    public string? MatchLog { get; set; }

}
