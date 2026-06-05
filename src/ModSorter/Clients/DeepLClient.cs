using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ModSorter.Clients;

public static class DeepLClient
{
    private static HttpClient? _http;
    private static string _key = "";

    public static string LastError = "";

    public static void Init(string apiKey)
    {
        _key = apiKey;
        // 無料版キーは末尾が :fx
        var baseUrl = apiKey.TrimEnd().EndsWith(":fx")
            ? "https://api-free.deepl.com/"
            : "https://api.deepl.com/";
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {apiKey}");
    }

    public static bool IsReady => _http != null && !string.IsNullOrEmpty(_key);

    // HTMLを日本語に翻訳(タグ保持)
    public static async Task<string?> TranslateHtmlAsync(string html)
    {
        if (_http == null) { LastError = "未初期化"; return null; }
        if (string.IsNullOrWhiteSpace(html)) return html;
        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("text", html),
                new("target_lang", "JA"),
                new("tag_handling", "html")
            };
            using var content = new FormUrlEncodedContent(form);
            using var resp = await _http.PostAsync("v2/translate", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)resp.StatusCode}: {body.Substring(0, Math.Min(120, body.Length))}";
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var translations = doc.RootElement.GetProperty("translations");
            if (translations.GetArrayLength() == 0) return null;
            return translations[0].GetProperty("text").GetString();
        }
        catch (Exception ex)
        {
            LastError = "ex: " + ex.Message;
            return null;
        }
    }
}
