using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace ModSorter;

public class ModrinthResult
{
    public string ProjectId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Slug { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string Url => string.IsNullOrEmpty(Slug) ? "" : $"https://modrinth.com/mod/{Slug}";
}


public static class ModrinthClient
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { BaseAddress = new Uri("https://api.modrinth.com/v2/") };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ModSorter/0.1 (+https://github.com/aonohayunoofficial-cloud/modsorter-v2)");
        return c;
    }

    public static string Sha1(string filePath)
    {
        using var sha = SHA1.Create();
        using var fs = File.OpenRead(filePath);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // jarのSHA1からプロジェクトを照合
    public static async Task<ModrinthResult?> GetByHashAsync(string jarPath)
    {
        try
        {
            var hash = Sha1(jarPath);
            // バージョン情報を取得
            using var verResp = await Http.GetAsync($"version_file/{hash}?algorithm=sha1");
            if (!verResp.IsSuccessStatusCode) return null;

            using var verDoc = JsonDocument.Parse(await verResp.Content.ReadAsStringAsync());
            if (!verDoc.RootElement.TryGetProperty("project_id", out var pid))
                return null;
            var projectId = pid.GetString() ?? "";
            if (string.IsNullOrEmpty(projectId)) return null;

            // プロジェクト詳細を取得
            using var projResp = await Http.GetAsync($"project/{projectId}");
            if (!projResp.IsSuccessStatusCode) return null;

            using var projDoc = JsonDocument.Parse(await projResp.Content.ReadAsStringAsync());
            var root = projDoc.RootElement;

            return new ModrinthResult
            {
                ProjectId = projectId,
                Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                Body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                Slug = root.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "",
                IconUrl = root.TryGetProperty("icon_url", out var ic) ? ic.GetString() ?? "" : ""
            };

        }
        catch
        {
            return null;
        }
    }
}
