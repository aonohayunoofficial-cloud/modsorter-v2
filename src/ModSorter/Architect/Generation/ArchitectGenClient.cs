using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModSorter.Architect.Generation;

// 建築用ローカルLLMクライアント（最小実験）。
// 既存 Clients/OllamaClient とは独立。モデル名は呼び出し時に指定。
public sealed class ArchitectGenClient
{
    private const string Endpoint = "http://localhost:11434/api/generate";
    private const int TimeoutSeconds = 300; // 生成は分類より重いので長め

    private readonly HttpClient _http;

    public ArchitectGenClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
    }

    public async Task<GenerationResult> GenerateAsync(
        string model, string instruction, IReadOnlyList<string> allowedBlocks)
    {
        var result = new GenerationResult();

        string blockList = string.Join(", ", allowedBlocks);
        string prompt =
$@"You design a small Minecraft building. Output ONLY strict JSON, no prose.

You do NOT output coordinates. You output a high-level SPEC.
A separate program turns your spec into blocks (floor, roof, walls, openings).
Just describe the building's dimensions, materials, and where windows/doors go.

ALLOWED BLOCK IDS (use these exactly for the *_block fields, never invent others):
{blockList}

OUTPUT SHAPE (this exact JSON shape):
{{
  ""width"": 5,
  ""depth"": 5,
  ""height"": 4,
  ""floor_block"": ""minecraft:oak_planks"",
  ""wall_block"": ""minecraft:oak_planks"",
  ""roof_block"": ""minecraft:oak_planks"",
  ""roof_type"": ""gable"",
  ""ridge_axis"": ""x"",
  ""floor_levels"": [],
  ""openings"": [


    {{ ""face"": ""south"", ""kind"": ""door"",   ""offset"": 2, ""level"": 1 }},
    {{ ""face"": ""east"",  ""kind"": ""window"", ""offset"": 2, ""level"": 1, ""block"": ""minecraft:glass"" }}
  ]
}}

FIELD MEANING:
- width = size along X, depth = size along Z, height = number of layers (vertical).
- *_block = which allowed block to use for that part.
- roof_type: ""flat"" (a flat roof) or ""gable"" (a triangular pitched roof).
- ridge_axis: only for gable. ""x"" = ridge runs along X (roof slopes toward Z edges);
  ""z"" = ridge runs along Z (roof slopes toward X edges). Pick whichever fits a house.
- floor_levels: heights (y) where an extra floor (ceiling/floor slab) is added,
  to split the interior into multiple stories. Empty [] = single story.

HOW TO HANDLE STORIES (IMPORTANT):
- If the instruction asks for N stories (e.g. ""2-story"", ""2階建て"", ""3 floors""),
  you MUST make a multi-story building. Do NOT leave floor_levels empty in that case.
- Give each story about 4 blocks of height. So:
    1 story  -> height about 4, floor_levels = []
    2 stories -> height about 7, floor_levels = [4]   (one floor slab at y=4)
    3 stories -> height about 10, floor_levels = [4, 7]
- Each floor_levels value must be between 1 and height-2.
- If the instruction explicitly gives dimensions like ""WxDxH"", use that H as height,
  and still add the appropriate floor_levels for the requested number of stories.
- openings: each is on one wall face (""north"",""south"",""east"",""west"").
  - kind: ""door"" (an empty opening) or ""window"" (a glass cell).
  - offset: position along that face (0 = one corner, up to width-1 or depth-1).
  - level: which middle layer, counting from the floor (1 = just above the floor).

RULES:
- Interpret the instruction's size (e.g. ""5x5x4"" means width=5, depth=5, height=4).
- Use only allowed block IDs for *_block and window block.
- Keep openings on walls, not on corners. A small house has 1 door and a few windows.
- Output ONLY the JSON spec. No explanation, no coordinates.

BUILD INSTRUCTION:
{instruction}

JSON:";

        var payload = new
        {
            model,
            prompt,
            stream = false,
            format = "json", // OllamaのJSONモードで素のJSONを促す
            options = new { temperature = 0.2 }
        };

        try
        {
            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(Endpoint, content);

            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                result.Error = $"HTTP {(int)resp.StatusCode}: {err}";
                return result;
            }

            string respText = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respText);
            if (!doc.RootElement.TryGetProperty("response", out var respProp))
            {
                result.Error = "応答に response フィールドがありません。";
                result.RawResponse = respText;
                return result;
            }

            string raw = respProp.GetString() ?? "";
            result.RawResponse = raw;

            var spec = TryParseSpec(raw);
            if (spec == null)
            {
                result.Error = "SPEC(JSON)としてパースできませんでした。";
                return result;
            }
            if (spec.Width < 2 || spec.Depth < 2 || spec.Height < 2)
            {
                result.Error = $"寸法が不正です（W={spec.Width}, D={spec.Depth}, H={spec.Height}）。";
                return result;
            }

            // 確定的に座標展開（壁の外周リングは必ずここで生成）
            var blocks = StructureExpander.Expand(spec, allowedBlocks);
            if (blocks.Count == 0)
            {
                result.Error = "展開結果が空になりました。";
                return result;
            }

            result.Blocks = blocks;
            return result;
        }
        catch (TaskCanceledException)
        {
            result.Error = $"タイムアウト({TimeoutSeconds}秒)。モデルが重い可能性があります。";
            return result;
        }
        catch (HttpRequestException ex)
        {
            result.Error = $"接続失敗: {ex.Message}（Ollamaが起動していない可能性）";
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return result;
        }
    }

    // Ollamaにインストール済みのモデルタグ一覧を取得。失敗時は空リスト。
    public async Task<List<string>> ListModelsAsync()
    {
        const string tagsUrl = "http://localhost:11434/api/tags";
        var models = new List<string>();
        try
        {
            using var resp = await _http.GetAsync(tagsUrl);
            if (!resp.IsSuccessStatusCode) return models;

            string text = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("models", out var arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var name))
                    {
                        var s = name.GetString();
                        if (!string.IsNullOrEmpty(s)) models.Add(s);
                    }
                }
            }
        }
        catch
        {
            // 取得失敗時は空のまま（UI側でフォールバック）
        }
        return models;
    }

    // 生出力からJSONを取り出してパース。format=json でも前後にゴミが付く場合に備え、
    // 最初の '{' から最後の '}' までを抜き出して試す。
    private static List<GeneratedBlock>? TryParse(string raw)
    {
        string candidate = raw.Trim();
        int start = candidate.IndexOf('{');
        int end = candidate.LastIndexOf('}');
        if (start >= 0 && end > start)
            candidate = candidate.Substring(start, end - start + 1);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var structure = JsonSerializer.Deserialize<GeneratedStructure>(candidate, opts);
            // blocks フィールドが無い／空（{} や {"blocks":[]}）は失敗扱い。
            // これを成功0ブロックと誤判定しないための保険。
            if (structure?.Blocks == null || structure.Blocks.Count == 0) return null;
            return structure.Blocks;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // 生出力から StructureSpec を取り出してパース。前後にゴミが付く場合に備え
    // 最初の '{' から最後の '}' までを抜き出して試す。
    private static StructureSpec? TryParseSpec(string raw)
    {
        string candidate = raw.Trim();
        int start = candidate.IndexOf('{');
        int end = candidate.LastIndexOf('}');
        if (start >= 0 && end > start)
            candidate = candidate.Substring(start, end - start + 1);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<StructureSpec>(candidate, opts);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}