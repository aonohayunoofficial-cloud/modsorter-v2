using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 1ジャンルの定義（JSONファイル1枚に対応）
public sealed class Genre
{
    // プルダウンに出す表示名（日本語）
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";

    // 並び順（小さいほど上）。任意。
    [JsonPropertyName("order")] public int Order { get; set; } = 100;

    // プロンプトに差し込む雰囲気の説明（英文）
    [JsonPropertyName("style_prompt")] public string StylePrompt { get; set; } = "";

    // このジャンルで使うブロック（ID＋日本語名）
    [JsonPropertyName("blocks")] public List<GenreBlock> Blocks { get; set; } = new();

    // 読み込んだファイル名（デバッグ用、JSONには書かない）
    [JsonIgnore] public string SourceFile { get; set; } = "";
}

// ブロック1つ（ID と 人間向けの表示名）
public sealed class GenreBlock
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = ""; // 日本語表示名
}
