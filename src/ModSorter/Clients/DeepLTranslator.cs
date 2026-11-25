namespace ModSorter.Clients;

// DeepL を ITranslator として扱う薄い包み。
// DeepLClient は static で、MOD 説明文の翻訳(TranslateHtmlAsync)や英訳など
// 他機能からも直接使われているため作り替えない。Init と呼び出しを中継するだけにする。
// DeepL は ignore_tags=x で <x id="n"/> をそのまま扱えるので、載せ替えは不要。
public sealed class DeepLTranslator : ITranslator
{
    private readonly bool _hasKey;
    private bool _fatal;

    public DeepLTranslator(string apiKey)
    {
        var key = (apiKey ?? "").Trim();
        _hasKey = key.Length > 0;
        // キーが空のときは Init しない。空キーで Init すると、説明文翻訳など
        // 他機能が使っている DeepLClient の状態まで空キーで上書きしてしまう。
        if (_hasKey) DeepLClient.Init(key);
    }

    public string Name => TranslatorFactory.DeepL;
    public string DisplayName => "DeepL";
    public bool IsReady => _hasKey && DeepLClient.IsReady;
    public string LastError => _hasKey ? DeepLClient.LastError : "キー未設定";
    public bool LastErrorIsFatal => _fatal;

    // v2/usage は文字を消費せずに (character_count, character_limit) を返す。
    public bool SupportsUsage => true;

    public string Endpoint => _hasKey ? DeepLClient.BaseUrl : "(キー未設定)";
    public string KeyTail => _hasKey ? DeepLClient.KeyTail : "";

    public async Task<List<string>?> TranslateBatchXmlAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (!_hasKey) { _fatal = true; return null; }
        ct.ThrowIfCancellationRequested();
        var r = await DeepLClient.TranslateBatchXmlAsync(texts);
        _fatal = r == null && IsFatalError(DeepLClient.LastError);
        return r;
    }

    public async Task<(long Count, long Limit)?> GetUsageAsync()
        => _hasKey ? await DeepLClient.GetUsageAsync() : null;

    // DeepLClient.LastError は "HTTP {code}: ..." の形。
    // 456: 枠切れ。Developer プランの通算枠は使い切るとリセットされない。
    // 403: キー不正。401 も認証失敗として同じ扱いにする。
    private static bool IsFatalError(string err)
        => err.StartsWith("HTTP 456:") || err.StartsWith("HTTP 403:")
        || err.StartsWith("HTTP 401:");
}
