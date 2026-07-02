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

    // プレーンテキストを英語に翻訳する（画像生成プロンプト用）。
    // tag_handling は付けない（HTMLではなく素のテキストとして扱う）。
    public static async Task<string?> TranslateToEnglishAsync(string text)
    {
        if (_http == null) { LastError = "未初期化"; return null; }
        if (string.IsNullOrWhiteSpace(text)) return text;
        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("text", text),
                new("target_lang", "EN-US")
            };
            using var content = new FormUrlEncodedContent(form);
            using var resp = await _http.PostAsync("v2/translate", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)resp.StatusCode}: " +
                    body.Substring(0, Math.Min(120, body.Length));
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

    // ===== lang一括翻訳用（追加） =====

    // DeepLのusage(当月使用文字数と上限)を取得する。失敗時はnull。
    public static async Task<(long Count, long Limit)?> GetUsageAsync()
    {
        if (_http == null) { LastError = "未初期化"; return null; }
        try
        {
            using var resp = await _http.GetAsync("v2/usage");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)resp.StatusCode}: " +
                    body.Substring(0, Math.Min(120, body.Length));
                return null;
            }
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            long count = root.GetProperty("character_count").GetInt64();
            long limit = root.GetProperty("character_limit").GetInt64();
            return (count, limit);
        }
        catch (Exception ex)
        {
            LastError = "ex: " + ex.Message;
            return null;
        }
    }

    // 複数のプレーンテキストを一括で日本語に翻訳する（lang値用）。
    // DeepLは1リクエストにつき text を最大50件まで受け付ける。
    // 呼び出し側で50件以下に分割して渡すこと。入力順と同じ順序で結果を返す。
    // 失敗時はnull（呼び出し側で原文フォールバックする）。
    // tag_handlingは付けない（langの値は素のテキストとして扱う）。
    public static async Task<List<string>?> TranslateBatchAsync(IReadOnlyList<string> texts)
    {
        if (_http == null) { LastError = "未初期化"; return null; }
        if (texts == null || texts.Count == 0) return new List<string>();
        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("target_lang", "JA"),
                new("preserve_formatting", "1")
            };
            // textパラメータを件数分繰り返して付与する（DeepLの複数text指定）
            foreach (var t in texts)
                form.Add(new("text", t ?? ""));

            using var content = new FormUrlEncodedContent(form);
            using var resp = await _http.PostAsync("v2/translate", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)resp.StatusCode}: " +
                    body.Substring(0, Math.Min(120, body.Length));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var translations = doc.RootElement.GetProperty("translations");
            var result = new List<string>(translations.GetArrayLength());
            foreach (var tr in translations.EnumerateArray())
                result.Add(tr.GetProperty("text").GetString() ?? "");
            return result;
        }
        catch (Exception ex)
        {
            LastError = "ex: " + ex.Message;
            return null;
        }
    }

    // XMLタグ保持モードで複数テキストを一括日本語翻訳する（案1-B）。
    // 呼び出し側で、翻訳させたくない断片(プレースホルダ/色コード)を
    // <x id="n"/> 形式のタグに置換し、それ以外の < > & はエスケープ済みで渡すこと。
    // ignore_tags=x により x タグは翻訳されず位置が保持される。
    // 入力順と同じ順序で返す。失敗時は null。
    public static async Task<List<string>?> TranslateBatchXmlAsync(IReadOnlyList<string> texts)
    {
        if (_http == null) { LastError = "未初期化"; return null; }
        if (texts == null || texts.Count == 0) return new List<string>();
        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("target_lang", "JA"),
                new("preserve_formatting", "1"),
                new("tag_handling", "xml"),
                new("ignore_tags", "x"),
                new("outline_detection", "0")
            };
            foreach (var t in texts)
                form.Add(new("text", t ?? ""));

            using var content = new FormUrlEncodedContent(form);
            using var resp = await _http.PostAsync("v2/translate", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)resp.StatusCode}: " +
                    body.Substring(0, Math.Min(120, body.Length));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var translations = doc.RootElement.GetProperty("translations");
            var result = new List<string>(translations.GetArrayLength());
            foreach (var tr in translations.EnumerateArray())
                result.Add(tr.GetProperty("text").GetString() ?? "");
            return result;
        }
        catch (Exception ex)
        {
            LastError = "ex: " + ex.Message;
            return null;
        }
    }
}

