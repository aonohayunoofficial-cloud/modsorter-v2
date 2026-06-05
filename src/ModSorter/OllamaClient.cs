using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ModSorter;

// ローカルOllama(http://localhost:11434)でMODを分類するクライアント
public static class OllamaClient
{
    private const string Endpoint = "http://localhost:11434/api/generate";
    private const string TagsUrl = "http://localhost:11434/api/tags";
    private const string Model = "qwen2.5:14b";
    private const int TimeoutSeconds = 120;
    private const int MaxBodyChars = 1500; // 説明文の切り詰め

    public static string LastError = "";

    private static readonly HttpClient Http;

    static OllamaClient()
    {
        Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
        };
    }

    // Ollamaが起動して応答するか簡易チェック
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var resp = await Http.GetAsync(TagsUrl);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // 1つのMODを、与えられた候補カテゴリの中から分類する。
    // 戻り値: 選ばれたカテゴリのリスト(1〜2個)。失敗時は null
    public static async Task<List<string>?> ClassifyAsync(
        string modName, string body, IReadOnlyList<string> candidates)
    {
        LastError = "";

        if (candidates == null || candidates.Count == 0)
        {
            LastError = "候補カテゴリが空です。";
            return null;
        }

        string desc = body ?? "";
        if (desc.Length > MaxBodyChars)
            desc = desc.Substring(0, MaxBodyChars);

        string candidateList = string.Join(", ", candidates);

        string prompt =
$@"You are classifying a Minecraft mod into categories.
Choose 1 or 2 categories that best fit the mod, STRICTLY from this list:
{candidateList}

Rules:
- Only output category names from the list above, separated by commas.
- Do not invent new categories.
- Do not output any explanation, only the category names.

Mod name: {modName}
Mod description:
{desc}

Categories:";

        var payload = new
        {
            model = Model,
            prompt = prompt,
            stream = false,
            options = new { temperature = 0.0 }
        };

        try
        {
            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(Endpoint, content);

            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                LastError = $"HTTP {(int)resp.StatusCode}: {err}";
                return null;
            }

            string respText = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respText);
            if (!doc.RootElement.TryGetProperty("response", out var respProp))
            {
                LastError = "応答に response フィールドがありません。";
                return null;
            }

            string raw = respProp.GetString() ?? "";
            var result = ParseCategories(raw, candidates);
            if (result.Count == 0)
            {
                LastError = $"候補に一致する分類が得られませんでした。応答: {raw.Trim()}";
                return null;
            }
            return result;
        }
        catch (TaskCanceledException)
        {
            LastError = $"タイムアウト({TimeoutSeconds}秒)しました。モデルが重い可能性があります。";
            return null;
        }
        catch (HttpRequestException ex)
        {
            LastError = $"接続失敗: {ex.Message}(Ollamaが起動していない可能性)";
            return null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    // LLMの生出力から、候補に含まれるカテゴリだけを最大2個抽出
    private static List<string> ParseCategories(string raw, IReadOnlyList<string> candidates)
    {
        var found = new List<string>();
        foreach (var cand in candidates)
        {
            if (raw.Contains(cand, StringComparison.OrdinalIgnoreCase))
            {
                if (!found.Contains(cand)) found.Add(cand);
            }
            if (found.Count >= 2) break;
        }
        return found;
    }
}
