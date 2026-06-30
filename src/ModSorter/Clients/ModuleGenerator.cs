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
    private const string TagsEndpoint = "http://localhost:11434/api/tags";
    private const string DefaultModel = "gpt-oss:20b";
    private const int TimeoutSeconds = 180;

    // Ollama にインストール済みのモデル名一覧を取得する。
    // 失敗時は空リストを返す(UI 側で既定モデルにフォールバックする)。
    public static async Task<List<string>> ListModelsAsync()
    {
        var names = new List<string>();
        try
        {
            using var resp = await Http.GetAsync(TagsEndpoint);
            if (!resp.IsSuccessStatusCode) return names;

            string body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("models", out var arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var n))
                    {
                        string? name = n.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                    }
                }
            }
        }
        catch { /* 取得失敗は空のまま返す */ }
        return names;
    }

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

    // 機械骨格に不要な「装飾・建材・純素材」を判定する除外キーワード。
    // create の機能ブロックは原則すべて許可し、ここに該当するものだけ落とす(ブラックリスト方式)。
    private static readonly string[] DecorationKeywords =
    {
        "_planks", "_log", "_wood", "_stairs", "_slab", "_fence", "_wall",
        "_door", "_trapdoor", "_window", "_glass", "_pane", "_bars",
        "_tile", "_tiles", "_brick", "_bricks", "_paving", "_pillar",
        "_polished", "_cut_", "_layer", "_palette", "_seat",
        "_sail", "_train_door", "_girder_encased", "copycat",
    };

    // 単体で設置できない/モジュールに不向きなため明示的に除外する個別ブロック。
    private static readonly HashSet<string> PowerExcluded = new(StringComparer.Ordinal)
    {
        "create:water_wheel_structure",
        "create:large_water_wheel_structure",
        "create:mechanical_piston_head",
        "create:sticky_mechanical_piston_head",
        "create:piston_extension_pole",   // 単体不可の延長パーツ
        "create:gantry_shaft",            // gantry本体と組でないと意味を持たない補助
        "create:contraption_controls",
        "create:minecart_anchor",
    };

    // フルパレットから、create の機能ブロックを「原則すべて」許可リストに入れる。
    // 装飾・建材・純素材(DecorationKeywords該当)と、単体設置不可の個別ブロック
    // (PowerExcluded)だけを除外する。これで funnel/chain_drive/vault/pipe 等の
    // 機能ブロックがキーワード漏れで消える問題をなくす。
    public static Dictionary<string, Dictionary<string, List<string>>> BuildPowerPalette(
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> fullPalette)
    {
        var result = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        foreach (var kv in fullPalette)
        {
            string id = kv.Key;
            if (!id.StartsWith("create:", StringComparison.Ordinal)) continue;
            if (PowerExcluded.Contains(id)) continue;

            // 装飾・建材・純素材は除外。
            bool isDecoration = false;
            foreach (var k in DecorationKeywords)
            {
                if (id.Contains(k, StringComparison.Ordinal)) { isDecoration = true; break; }
            }
            if (isDecoration) continue;

            result[id] = kv.Value;
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
        int sizeZ = 9,
        string? model = null,
        IReadOnlyList<string>? genres = null,
        string? refinementNotes = null)
    {
        LastError = "";

        // モデル未指定なら既定モデルを使う。
        string useModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;

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

        // 全ルールファイルを Architect/Rules/ から一括読込(番号順に結合)。無ければ空で続行。
        string powerRules = ModSorter.Architect.Generation.RuleLoader.LoadAllRules();

        string powerRulesSection = string.IsNullOrWhiteSpace(powerRules)
            ? ""
            : $"Follow these Create-mod placement rules when placing blocks:\n{powerRules}\n\n";

        // ジャンル制約。動力・加工・搬送・保管などは「別モジュール」が基本。
        // チェックされたジャンルだけを作らせ、それ以外の役割のブロックは混ぜさせない。
        string genreSection = "";
        if (genres != null && genres.Count > 0)
        {
            string list = string.Join("、", genres);
            if (genres.Count == 1)
            {
                genreSection =
                    $"このモジュールのジャンルは「{list}」だけです。\n" +
                    "このジャンルの機能を持つブロックだけで構成してください。\n" +
                    "他のジャンル(動力源/動力伝達/動力制御/加工/搬送/保管/流体/可動・構造/計測・表示/レッドストーン連動)の" +
                    "ブロックを混ぜてはいけません。完成品ではなく、後で他モジュールと接続できる単機能の骨格を作ります。\n\n";
            }
            else
            {
                genreSection =
                    $"このモジュールのジャンルは「{list}」です。\n" +
                    "指定されたジャンルの機能だけで構成し、それ以外のジャンルのブロックは混ぜないでください。\n" +
                    "後で他モジュールと接続できる骨格として作ります。\n\n";
            }
        }

        // 前回生成の結合不正を次のプロンプトに反映する(再生成時のみ)。
        string refinementSection = "";
        if (!string.IsNullOrWhiteSpace(refinementNotes))
        {
            refinementSection =
                "前回の配置には次の不具合があった。今回は必ず直すこと:\n" +
                refinementNotes + "\n\n";
        }

        int maxX = sizeX - 1;
        int maxY = sizeY - 1;
        int maxZ = sizeZ - 1;

        string prompt =
$@"{refinementSection}{genreSection}{powerRulesSection}You place Minecraft blocks for a Create-mod machine.
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
- create:crushing_wheel (singular id) MUST be placed as a PAIR. Both wheels use the SAME
  axis. Place the two wheels one block apart along the axis PERPENDICULAR to their
  rotation axis (leave exactly one empty cell between them, horizontally). NEVER place
  them directly adjacent, and NEVER place a single one. Drive BOTH wheels with
  create:shaft (same axis) at the AXIS END, and put BOTH shafts on the SAME side
  (both on the negative-axis end OR both on the positive-axis end) so one drive line can
  reach them; do NOT attach shafts to the perpendicular sides. Items go into the gap from
  above. Output path below the gap is THREE cells stacked: gap -> create:andesite_funnel
  (facing=down) -> a STORAGE block (chest/barrel/item_vault). The funnel is REQUIRED to
  feed the storage. Do NOT use a depot as the storage (it holds only one item).
- Output the single JSON object only.

Allowed blocks:
{blockList}
User request:
{userRequest}

JSON object:";

        var payload = new
        {
            model = useModel,
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

                    // bool 値の表記ゆれを正規化(True/False/TRUE → true/false)。
                    // Minecraft の blockstate は小文字なので、大文字だと許可リストと一致せず弾かれる。
                    if (string.Equals(pVal, "true", StringComparison.OrdinalIgnoreCase))
                        pVal = "true";
                    else if (string.Equals(pVal, "false", StringComparison.OrdinalIgnoreCase))
                        pVal = "false";

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
