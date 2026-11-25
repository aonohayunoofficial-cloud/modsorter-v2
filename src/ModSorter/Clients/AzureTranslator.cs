using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModSorter.Clients;

// Azure AI Translator (Text Translation v3.0) を ITranslator として扱う。
// 仕様の出典: learn.microsoft.com の translator/service-limits、
// text-translation/reference/v3/translate、status-response-codes、prevent-translation。
//   1リクエスト: 要素 1,000 件まで / 合計 50,000 文字まで
//   F0: 2,000,000 文字/時をスライディングウィンドウで評価
//       （目安 約 33,300 文字/分。短時間に偏ると 429 になる）
//   無料枠切れ: HTTP 403 / code 403001
//   キーだけで消費量を取る API は無い（ポータルのメトリックで見る）
//
// <x id="n"/> の扱い: Azure には DeepL の ignore_tags に当たる指定が無い。
// textType=html で送り、退避タグを <span class="notranslate">n</span> へ載せ替えて
// 訳させず、戻りで <x id="n"/> へ戻す。これで RestoreXml を共通のまま使える。
// html では改行が空白に畳まれるため、\n も <br> へ載せ替えて往復させる。
public sealed class AzureTranslator : ITranslator
{
    private const string BaseUrl = "https://api.cognitive.microsofttranslator.com/";
    private const string TranslatePath =
        "translate?api-version=3.0&from=en&to=ja&textType=html";

    // F0 の分あたり目安(約33,300)より少し下に抑える。
    private const double CharsPerSecond = 30000.0 / 60.0;

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(60)
    };

    // 送信ペースはインスタンスをまたいで共有する（作り直しても枠は同じため）。
    private static DateTime _nextSendUtc = DateTime.MinValue;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly Regex XTagRegex = new(
        @"<x id=""(\d+)""/>", RegexOptions.Compiled);
    private static readonly Regex SpanBackRegex = new(
        @"<span\b[^>]*\bnotranslate\b[^>]*>\s*(\d+)\s*</span>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BrRegex = new(
        @"<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _key;
    private readonly string _region;

    public AzureTranslator(string apiKey, string region)
    {
        _key = (apiKey ?? "").Trim();
        _region = (region ?? "").Trim();
        if (_key.Length == 0) LastError = "キー未設定";
    }

    public string Name => TranslatorFactory.Azure;
    public string DisplayName => "Azure AI Translator";
    public bool IsReady => _key.Length > 0;
    public string LastError { get; private set; } = "";
    public bool LastErrorIsFatal { get; private set; }
    public bool SupportsUsage => false;

    public string Endpoint => BaseUrl +
        (_region.Length > 0 ? $"（リージョン {_region}）" : "（リージョン未指定）");
    public string KeyTail => _key.Length <= 4 ? _key : _key[^4..];

    public async Task<List<string>?> TranslateBatchXmlAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        LastErrorIsFatal = false;
        if (!IsReady) { LastError = "キー未設定"; LastErrorIsFatal = true; return null; }
        if (texts == null || texts.Count == 0) return new List<string>();

        var html = texts.Select(ToHtml).ToList();
        int chars = html.Sum(s => s.Length);
        var body = JsonSerializer.Serialize(html.Select(s => new { text = s }));

        // 429 はスライディングウィンドウの超過。秒単位の再送では窓が空かないので、
        // ここで1回だけ長めに待ってから送り直す。
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await WaitTurnAsync(chars, ct);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, TranslatePath)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("Ocp-Apim-Subscription-Key", _key);
                if (_region.Length > 0)
                    req.Headers.Add("Ocp-Apim-Subscription-Region", _region);

                using var resp = await Http.SendAsync(req, ct);
                var text = await resp.Content.ReadAsStringAsync(ct);
                if (resp.IsSuccessStatusCode) return Parse(text, texts.Count);

                int status = (int)resp.StatusCode;
                LastError = FormatError(status, text);
                LastErrorIsFatal = status == 401 || status == 403;
                if (status != 429 || attempt > 0) return null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { LastError = "ex: " + ex.Message; return null; }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
        return null;
    }

    public Task<(long Count, long Limit)?> GetUsageAsync()
    {
        LastError = "Azure はキーだけでは消費量を取得できません（ポータルのメトリックで確認）";
        return Task.FromResult<(long Count, long Limit)?>(null);
    }

    // 前回送信の文字数に応じた間隔を空けてから送る。
    private static async Task WaitTurnAsync(int chars, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            if (_nextSendUtc > now) await Task.Delay(_nextSendUtc - now, ct);
            _nextSendUtc = DateTime.UtcNow + TimeSpan.FromSeconds(chars / CharsPerSecond);
        }
        finally { Gate.Release(); }
    }

    // 応答は入力と同じ順の配列。各要素の translations[0].text が訳文。
    private List<string>? Parse(string json, int expected)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<string>(expected);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var tr = item.GetProperty("translations");
            list.Add(tr.GetArrayLength() > 0
                ? FromHtml(tr[0].GetProperty("text").GetString() ?? "")
                : "");
        }
        if (list.Count < expected)
        {
            LastError = $"応答件数不足 {list.Count}/{expected}";
            return null;
        }
        return list;
    }

    // ProtectXml の出力 → Azure 用 html。
    private static string ToHtml(string s)
    {
        s = XTagRegex.Replace(s ?? "",
            m => $"<span class=\"notranslate\">{m.Groups[1].Value}</span>");
        return s.Replace("\r\n", "\n").Replace("\n", "<br>");
    }

    // Azure の訳文 → <x id="n"/> の形へ戻す。戻せなかったタグは RestoreXml が警告に数える。
    // &nbsp; は XmlUnescape が知らない実体なので、ここで空白へ戻す。
    private static string FromHtml(string s)
    {
        s = SpanBackRegex.Replace(s, m => $"<x id=\"{m.Groups[1].Value}\"/>");
        s = BrRegex.Replace(s, "\n");
        return s.Replace("&nbsp;", " ");
    }

    // {"error":{"code":403001,"message":"..."}} から code と message を拾う。
    private static string FormatError(int status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var e = doc.RootElement.GetProperty("error");
            return $"HTTP {status} ({e.GetProperty("code").GetInt32()}): " +
                   e.GetProperty("message").GetString();
        }
        catch
        {
            return $"HTTP {status}: " + body.Substring(0, Math.Min(120, body.Length));
        }
    }
}
