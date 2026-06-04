using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ModSorter;

public class CurseForgeResult
{
    public int ModId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Url { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string DescriptionHtml { get; set; } = "";
    public List<string> Categories { get; set; } = new();
}


public static class CurseForgeClient
{
    private static HttpClient? _http;
    private static string _apiKey = "";

    public static string LastError = "";

    public static void Init(string apiKey)
    {
        _apiKey = apiKey;
        _http = new HttpClient { BaseAddress = new Uri("https://api.curseforge.com/") };
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ModSorter/0.1");
    }

    public static bool IsReady => _http != null && !string.IsNullOrEmpty(_apiKey);

    // CurseForge fingerprint = 空白除去後の MurmurHash2 (seed=1)
    public static long ComputeFingerprint(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var filtered = new byte[bytes.Length];
        int len = 0;
        foreach (var b in bytes)
        {
            // タブ(9)・改行(10)・復帰(13)・スペース(32)を除去
            if (b == 9 || b == 10 || b == 13 || b == 32) continue;
            filtered[len++] = b;
        }
        return MurmurHash2(filtered, len, 1);
    }

    private static long MurmurHash2(byte[] data, int length, uint seed)
    {
        const uint m = 0x5bd1e995;
        const int r = 24;
        uint h = seed ^ (uint)length;
        int i = 0;
        while (length - i >= 4)
        {
            uint k = (uint)(data[i] | (data[i + 1] << 8) |
                            (data[i + 2] << 16) | (data[i + 3] << 24));
            k *= m;
            k ^= k >> r;
            k *= m;
            h *= m;
            h ^= k;
            i += 4;
        }
        int rem = length - i;
        if (rem >= 3) h ^= (uint)(data[i + 2] << 16);
        if (rem >= 2) h ^= (uint)(data[i + 1] << 8);
        if (rem >= 1) { h ^= data[i]; h *= m; }
        h ^= h >> 13;
        h *= m;
        h ^= h >> 15;
        return h;
    }

    public static async Task<CurseForgeResult?> GetByFingerprintAsync(string filePath)
    {
        if (_http == null) { LastError = "client null"; return null; }
        try
        {
            long fp = ComputeFingerprint(filePath);
            var body = JsonSerializer.Serialize(new { fingerprints = new[] { fp } });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("v1/fingerprints", content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)resp.StatusCode}: {respText.Substring(0, Math.Min(120, respText.Length))}";
                return null;
            }

            using var doc = JsonDocument.Parse(respText);
            var matches = doc.RootElement.GetProperty("data").GetProperty("exactMatches");
            if (matches.GetArrayLength() == 0)
            {
                LastError = $"no match (fp={fp})";
                return null;
            }

            var file = matches[0].GetProperty("file");
            int modId = file.GetProperty("modId").GetInt32();

            using var modResp = await _http.GetAsync($"v1/mods/{modId}");
            if (!modResp.IsSuccessStatusCode)
            {
                LastError = $"mod detail HTTP {(int)modResp.StatusCode}";
                return new CurseForgeResult { ModId = modId };
            }

            using var modDoc = JsonDocument.Parse(await modResp.Content.ReadAsStringAsync());
            var d = modDoc.RootElement.GetProperty("data");

            string iconUrl = "";
            if (d.TryGetProperty("logo", out var logo) &&
                logo.ValueKind == JsonValueKind.Object &&
                logo.TryGetProperty("url", out var lu))
                iconUrl = lu.GetString() ?? "";

            // categories配列を取り出す(各要素はオブジェクトで name を持つ)
            var cats = new List<string>();
            if (d.TryGetProperty("categories", out var catArr) &&
                catArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in catArr.EnumerateArray())
                {
                    if (c.TryGetProperty("name", out var cn))
                    {
                        var name = cn.GetString();
                        if (!string.IsNullOrEmpty(name)) cats.Add(name);
                    }
                }
            }

            // フル説明(HTML)を取得
            string descHtml = "";
            try
            {
                using var descResp = await _http.GetAsync($"v1/mods/{modId}/description");
                if (descResp.IsSuccessStatusCode)
                {
                    using var descDoc = JsonDocument.Parse(await descResp.Content.ReadAsStringAsync());
                    descHtml = descDoc.RootElement.GetProperty("data").GetString() ?? "";
                }
            }
            catch { }

            return new CurseForgeResult
            {
                ModId = modId,
                Name = d.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Slug = d.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "",
                Summary = d.TryGetProperty("summary", out var sm) ? sm.GetString() ?? "" : "",
                IconUrl = iconUrl,
                DescriptionHtml = descHtml,
                Categories = cats,
                Url = d.TryGetProperty("links", out var links) &&
                      links.TryGetProperty("websiteUrl", out var w)
                          ? w.GetString() ?? "" : ""
            };

        }
        catch (Exception ex)
        {
            LastError = "ex: " + ex.Message;
            return null;
        }
    }
}
