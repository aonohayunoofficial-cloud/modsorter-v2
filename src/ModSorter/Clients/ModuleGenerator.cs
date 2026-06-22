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
    private const string Model = "gpt-oss:20b";
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

    // 動力・機械系のブロックを判定するキーワード。
    private static readonly string[] PowerKeywords =
    {
        "cogwheel", "shaft", "gearbox", "gearshift", "clutch",
        "water_wheel", "windmill", "crank", "flywheel", "steam_engine",
        "bearing", "belt", "press", "mixer", "encased_fan",
        "millstone", "depot", "funnel", "chute", "mechanical",
    };

    // 単体で設置できない/モジュールに不向きなため除外するブロック。
    private static readonly HashSet<string> PowerExcluded = new(StringComparer.Ordinal)
    {
        "create:water_wheel_structure",
        "create:mechanical_piston_head",
    };

    // フルパレットから、create の動力・機械系ブロックだけを抜き出して許可リストを作る。
    public static Dictionary<string, Dictionary<string, List<string>>> BuildPowerPalette(
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> fullPalette)
    {
        var result = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        foreach (var kv in fullPalette)
        {
            string id = kv.Key;
            if (!id.StartsWith("create:", StringComparison.Ordinal)) continue;
            if (PowerExcluded.Contains(id)) continue;

            bool hit = false;
            foreach (var k in PowerKeywords)
            {
                if (id.Contains(k, StringComparison.Ordinal)) { hit = true; break; }
            }
            if (hit) result[id] = kv.Value;
        }
        return result;
    }

    // allowed: 使用可能ブロック ID → (プロパティ名 → 値リスト)。
    // userRequest: 例 "shaftを3本、X軸方向に一直線に並べて"。
    // sizeX/sizeY/sizeZ: 生成を許す空間サイズ。座標は 0..size-1 に収める。
    public static async Task<List<PlacedBlock>?> GenerateAsync(
        string userRequest,
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> allowed,
        int sizeX = 9,
        int sizeY = 9,
        int sizeZ = 9)
    {
        LastError = "";

        if (allowed == null || allowed.Count == 0)
        {
            LastError = "使用可能ブロックの定義が空です。";
            return null;
        }

        if (string.IsNullOrWhiteSpace(userRequest))
        {
            LastError = "お題(userRequest)が空です。";
            return null;
        }

        // サイズは最低 1。異常値は 1 に丸める。
        if (sizeX < 1) sizeX = 1;
        if (sizeY < 1) sizeY = 1;
        if (sizeZ < 1) sizeZ = 1;

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

        // 動力ルール集を読み込む(無ければ空文字で続行)。
        string powerRules = "";
        try
        {
            string rulesPath = System.IO.Path.Combine(
                System.AppContext.BaseDirectory,
                "Architect", "Rules", "create_power_rules.txt");
            if (System.IO.File.Exists(rulesPath))
                powerRules = System.IO.File.ReadAllText(rulesPath, Encoding.UTF8);
        }
        catch { /* 読めなければルール無しで続行 */ }

        string powerRulesSection = string.IsNullOrWhiteSpace(powerRules)
            ? ""
            : $"Follow these Create-mod power rules when placing blocks:\n{powerRules}\n\n";

        int maxX = sizeX - 1;
        int maxY = sizeY - 1;
        int maxZ = sizeZ - 1;

        string prompt =
$@"{powerRulesSection}You place Minecraft blocks for a Create-mod machine.
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
- Coordinates are integers. The build volume is {sizeX} x {sizeY} x {sizeZ}.
  x MUST be 0..{maxX}, y MUST be 0..{maxY}, z MUST be 0..{maxZ}. Do NOT exceed these.
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
            // reasoning モデル(gpt-oss 等)は format=json を付けると thinking 段階で
            // 出力が破綻するため指定しない。代わりにプロンプトで JSON を強制し、
            // 応答からコードブロックや前置きを除去してパースする。
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
            raw = ExtractJsonBlock(raw);
            if (string.IsNullOrWhiteSpace(raw))
            {
                LastError = "応答が空でした(reasoning のみで本文が無い可能性)。";
                return null;
            }
            return ParsePlacement(raw, allowed, sizeX, sizeY, sizeZ);
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
    // 応答テキストから JSON 本体だけを取り出す。
    // ```json ... ``` のコードブロックや、前後の説明文を除去する。
    private static string ExtractJsonBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // ```json ... ``` または ``` ... ``` を剥がす。
        int fence = text.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            int start = text.IndexOf('\n', fence);
            if (start >= 0)
            {
                int end = text.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start)
                    return text.Substring(start + 1, end - start - 1).Trim();
            }
        }

        // コードブロックが無い場合、最初の { または [ から、対応する最後の } または ] までを取る。
        int objStart = text.IndexOf('{');
        int arrStart = text.IndexOf('[');

        int s;
        char close;
        if (objStart >= 0 && (arrStart < 0 || objStart < arrStart))
        {
            s = objStart;
            close = '}';
        }
        else if (arrStart >= 0)
        {
            s = arrStart;
            close = ']';
        }
        else
        {
            return text.Trim();
        }

        int e = text.LastIndexOf(close);
        if (e > s) return text.Substring(s, e - s + 1).Trim();

        return text.Trim();
    }

    private static List<PlacedBlock>? ParsePlacement(
        string raw,
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> allowed,
        int sizeX,
        int sizeY,
        int sizeZ)
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

            // 指定空間サイズの外に出たブロックは弾く(プロパティ処理の前に判定)。
            if (x < 0 || x >= sizeX || y < 0 || y >= sizeY || z < 0 || z >= sizeZ)
            {
                rejected.Add($"{id} 範囲外 ({x},{y},{z})");
                continue;
            }

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
