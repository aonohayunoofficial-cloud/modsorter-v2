using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ModSorter.Clients;

// ローカルOllama(qwen2.5:14b)で Create の機能モジュール配置を生成するクライアント。
// 入力: 使用可能ブロックの定義(ID→プロパティ→値) と ユーザー要望。
// 出力: ブロック配置のリスト(ID + 座標 + プロパティ)。
public static class ModuleGenerator
{
    private const string Endpoint = "http://localhost:11434/api/generate";
    private const string Model = "qwen2.5:14b";
    private const int TimeoutSeconds = 180;

    public static string LastError = "";

    private static readonly HttpClient Http =
        new() { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };

    // 生成された1ブロックの配置。
    public sealed class PlacedBlock
    {
        public string Id { get; set; } = "minecraft:air";
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }

    // allowed: 使用可能ブロック ID → (プロパティ名 → 値リスト)。
    // userRequest: 例 "shaftを3本、X軸方向に一直線に並べて"。
    public static async Task<List<PlacedBlock>?> GenerateAsync(
        string userRequest,
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> allowed)
    {
        LastError = "";

        if (allowed == null || allowed.Count == 0)
        {
            LastError = "使用可能ブロックの定義が空です。";
            return null;
        }

        // 使用可能ブロックを LLM が読みやすい行に整形。
        // 例: create:shaft  (axis: x|y|z)
        var sb = new StringBuilder();
        foreach (var kv in allowed)
        {
            sb.Append("- ").Append(kv.Key);
            if (kv.Value.Count > 0)
            {
                sb.Append("  (");
                bool first = true;
                foreach (var p in kv.Value)
                {
                    if (!first) sb.Append("; ");
                    sb.Append(p.Key).Append(": ").Append(string.Join("|", p.Value));
                    first = false;
                }
                sb.Append(')');
            }
            sb.Append('\n');
        }
        string blockList = sb.ToString();

        string prompt =
$@"You place Minecraft blocks for a Create-mod machine.
Output ONLY one JSON object. No explanation, no markdown.

The JSON object MUST have a single key ""blocks"" whose value is an ARRAY.
Each array element MUST be:
{{ ""id"": ""<block id>"", ""x"": <int>, ""y"": <int>, ""z"": <int>, ""properties"": {{ ""<name>"": ""<value>"" }} }}

Example of the required shape (values are illustrative):
{{
  ""blocks"": [
    {{ ""id"": ""create:millstone"", ""x"": 0, ""y"": 0, ""z"": 0, ""properties"": {{}} }},
    {{ ""id"": ""create:hand_crank"", ""x"": 0, ""y"": 1, ""z"": 0, ""properties"": {{ ""facing"": ""down"" }} }}
  ]
}}

Rules:
- ""id"" MUST be chosen exactly from the allowed block list below.
- ""properties"" may ONLY use the property names and values listed for that block.
- If a block has no properties listed, use ""properties"": {{}}.
- Coordinates are integers, keep them small (0 to 8).
- Put EVERY block as a separate element inside the ""blocks"" array.
- Output the single JSON object only.

Allowed blocks:
{blockList}
User request:
{userRequest}

JSON object:";

        var payload = new
        {
            model = Model,
            prompt,
            stream = false,
            format = "json",
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

            string raw = (respProp.GetString() ?? "").Trim();
            return ParsePlacement(raw, allowed);
        }
        catch (TaskCanceledException)
        {
            LastError = $"タイムアウト({TimeoutSeconds}秒)しました。";
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

    // LLM の生 JSON をパースし、許可リストで検証する。
    // format=json により response は配列、または {""blocks"":[...]} 等の可能性があるため両対応。
    private static List<PlacedBlock>? ParsePlacement(
        string raw,
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> allowed)
    {
        JsonElement arr;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                arr = root.Clone();
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // 最初に見つかった配列プロパティを使う(例 {""blocks"":[...]})。
                JsonElement? firstArray = null;
                foreach (var p in root.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Array)
                    {
                        firstArray = p.Value.Clone();
                        break;
                    }
                }
                if (firstArray == null)
                {
                    LastError = $"配列が見つかりません。応答: {raw}";
                    return null;
                }
                arr = firstArray.Value;
            }
            else
            {
                LastError = $"想定外のJSON形式です。応答: {raw}";
                return null;
            }
        }
        catch (Exception ex)
        {
            LastError = $"JSON解析失敗: {ex.Message} 応答: {raw}";
            return null;
        }

        var result = new List<PlacedBlock>();
        var rejected = new List<string>();

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;

            string id = el.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? "") : "";
            if (string.IsNullOrEmpty(id) || !allowed.TryGetValue(id, out var validProps))
            {
                rejected.Add($"不正なID: {id}");
                continue;
            }

            int x = el.TryGetProperty("x", out var xe) && xe.TryGetInt32(out var xi) ? xi : 0;
            int y = el.TryGetProperty("y", out var ye) && ye.TryGetInt32(out var yi) ? yi : 0;
            int z = el.TryGetProperty("z", out var ze) && ze.TryGetInt32(out var zi) ? zi : 0;

            Dictionary<string, string>? props = null;
            if (el.TryGetProperty("properties", out var pe) && pe.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in pe.EnumerateObject())
                {
                    string pName = p.Name;
                    string pVal = p.Value.ValueKind == JsonValueKind.String
                        ? (p.Value.GetString() ?? "")
                        : p.Value.ToString();

                    // 許可リストに無いプロパティ/値は捨てる(無効な状態を防ぐ)。
                    if (validProps.TryGetValue(pName, out var allowedVals)
                        && allowedVals.Contains(pVal))
                    {
                        props ??= new Dictionary<string, string>();
                        props[pName] = pVal;
                    }
                    else
                    {
                        rejected.Add($"{id} の不正プロパティ {pName}={pVal}");
                    }
                }
            }

            result.Add(new PlacedBlock { Id = id, X = x, Y = y, Z = z, Properties = props });
        }

        if (result.Count == 0)
        {
            LastError = $"有効なブロックが得られませんでした。除外: {string.Join("; ", rejected)}";
            return null;
        }

        if (rejected.Count > 0)
            LastError = $"(一部除外) {string.Join("; ", rejected)}";

        return result;
    }
}
