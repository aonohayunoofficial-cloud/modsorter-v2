using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModSorter.Architect;

// ブロック選択UI用のカタログ。block_catalog.json を読み込む。
public sealed class BlockCatalogItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class BlockCategory
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("blocks")] public List<BlockCatalogItem> Blocks { get; set; } = new();
}

public sealed class BlockCatalogRoot
{
    [JsonPropertyName("categories")] public List<BlockCategory> Categories { get; set; } = new();
}

public static class BlockCatalog
{
    public static string LastError { get; private set; } = "";

    public static List<BlockCategory> Load()
    {
        LastError = "";
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(dir, "Architect", "Genres", "block_catalog.json");
            if (!File.Exists(path))
            {
                LastError = $"block_catalog.json が見つかりません: {path}";
                return new List<BlockCategory>();
            }
            string json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var root = JsonSerializer.Deserialize<BlockCatalogRoot>(json, opts);
            return root?.Categories ?? new List<BlockCategory>();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return new List<BlockCategory>();
        }
    }
}
