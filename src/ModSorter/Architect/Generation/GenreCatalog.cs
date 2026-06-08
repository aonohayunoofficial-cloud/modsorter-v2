using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModSorter.Architect.Generation;

// Genres フォルダから *.json を読み込んでジャンル一覧を提供する。
public static class GenreCatalog
{
    // 実行ファイルの隣の Architect/Genres を見る
    private static string GenresDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Architect", "Genres");

    public static string LastError = "";

    public static List<Genre> Load()
    {
        LastError = "";
        var list = new List<Genre>();

        try
        {
            if (!Directory.Exists(GenresDir))
            {
                LastError = $"ジャンルフォルダが見つかりません: {GenresDir}";
                return list;
            }

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var file in Directory.GetFiles(GenresDir, "*.json"))
            {
                try
                {
                    string text = File.ReadAllText(file);
                    var genre = JsonSerializer.Deserialize<Genre>(text, opts);
                    if (genre != null && !string.IsNullOrWhiteSpace(genre.DisplayName))
                    {
                        genre.SourceFile = Path.GetFileName(file);
                        list.Add(genre);
                    }
                }
                catch (JsonException)
                {
                    // 壊れたJSONは1枚スキップして続行
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }

        return list.OrderBy(g => g.Order).ThenBy(g => g.DisplayName).ToList();
    }
}
