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
    string model, string instruction, IReadOnlyList<string> allowedBlocks,
    double temperature = 0.2, string? variantHint = null, string? stylePrompt = null,
    int? fixedWidth = null, int? fixedDepth = null, int? fixedHeight = null,
    string? facadeFace = null)
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
  ""accent_block"": ""minecraft:oak_log"",
  ""pilaster_step"": 3,
  ""has_base"": true,
  ""base_block"": ""minecraft:cobblestone"",
  ""building_style"": ""walled"",
  ""facade_face"": ""south"",
  ""roof_type"": ""gable"",
  ""ridge_axis"": ""x"",
  ""dome_height"": 5,
  ""floor_levels"": [],
  ""openings"": [


    {{ ""face"": ""south"", ""kind"": ""door"",   ""offset"": 2, ""level"": 1 }},
    {{ ""face"": ""east"",  ""kind"": ""window"", ""offset"": 2, ""level"": 1, ""block"": ""minecraft:glass"" }}
  ]
}}

FIELD MEANING:
- width = size along X, depth = size along Z, height = number of layers (vertical).
- *_block = which allowed block to use for that part.
- roof_type: ""flat"" (a flat roof), ""gable"" (a triangular pitched roof made of full
  blocks), ""gable_stairs"" (a triangular pitched roof made of STAIR blocks for a
  smoother sloped look), or ""dome"" (a rounded domed roof for temples/observatories).
- When you use ""gable_stairs"", roof_block MUST be a stair block (an id ending in
  ""_stairs"", e.g. minecraft:oak_stairs or minecraft:stone_brick_stairs) if one is in
  the allowed list. Otherwise fall back to ""gable"".
- ridge_axis: only for gable. ""x"" = ridge runs along X (roof slopes toward Z edges);
  ""z"" = ridge runs along Z (roof slopes toward X edges). Pick whichever fits a house.
- dome_height: only for dome. How tall the dome rises above the walls, in blocks.
  Omit to let it auto-size to a hemisphere. Use a larger value for a tall, grand dome.
- building_style: ""walled"" (an ordinary building with solid walls, the default),
  ""colonnade"" (an open structure with NO walls, just a sparse row of slim round
  columns around the perimeter, like a pavilion), or ""temple"" (a walled room at the
  back with a row of columns forming a porch across the FRONT, like a Greek temple
  facade). Column thickness and spacing are decided automatically; you only choose the
  style. ""temple"" looks best with a larger depth (6 or more).
- facade_face: only for building_style ""temple"". Which side the columned porch faces:
  ""north"", ""south"", ""east"", or ""west"". Choose the side that should be the front
  entrance. Omit to default to ""south"".
- floor_levels: heights (y) where an extra floor (ceiling/floor slab) is added,
  to split the interior into multiple stories. Empty [] = single story.
- accent_block: an allowed block used for vertical support columns (pilasters) on the
  walls. Choose a block that contrasts with wall_block (e.g. a log or a darker block).
  Omit it (or set it equal to wall_block) if the style should look plain.
- pilaster_step: spacing of those columns along the walls, as an integer.
  2-4 gives a visible rhythm of columns; use it when the style suits exposed framing
  (e.g. industrial, half-timbered, structured looks). Omit or 0 means no columns.
- has_base: true to add a base course (a foundation ring) at the bottom of the walls.
  Use it for buildings that look better sitting on a stone/brick foundation.
- base_block: the allowed block for that base course. Choose something solid and
  contrasting with the floor (e.g. cobblestone or stone bricks under wooden walls).
  Only matters when has_base is true.

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
- Use accent_block + pilaster_step only when the style benefits from visible columns or
  framing (industrial, fortified, or structured looks). For plain or natural styles,
  omit them so the walls stay simple.
- Use has_base + base_block when a stone/brick foundation suits the building (most
  houses look good on one). Skip it for very simple or floating structures.
- Use roof_type ""dome"" for grand, rounded buildings (temples, observatories, domed
  halls). For ordinary houses prefer ""gable"" or ""flat"".
- Prefer ""gable_stairs"" over ""gable"" when a stair block is available and a nicer
  sloped roof suits the house; set roof_block to that stair block.

- Use building_style ""colonnade"" for fully open, columned structures (pavilions,
  shrines) with columns all around. Use ""temple"" when the instruction wants a
  classical facade: a solid room behind a front row of columns (a porticoed temple).
  For normal houses keep ""walled"". When colonnade or temple, openings are ignored.
- Output ONLY the JSON spec. No explanation, no coordinates.

{(string.IsNullOrEmpty(stylePrompt) ? "" : "STYLE / GENRE:\n" + stylePrompt + "\n\n")}BUILD INSTRUCTION:
{instruction}
{(string.IsNullOrEmpty(variantHint) ? "" : "\nVARIATION FOR THIS DESIGN: " + variantHint)}

JSON:";

        var payload = new
        {
            model,
            prompt,
            stream = false,
            format = "json", // OllamaのJSONモードで素のJSONを促す
            options = new { temperature }
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

            // UIで寸法が確定されていれば、モデルの値を上書きする（寸法のブレを根絶）
            if (fixedWidth.HasValue) spec.Width = fixedWidth.Value;
            if (fixedDepth.HasValue) spec.Depth = fixedDepth.Value;
            if (fixedHeight.HasValue) spec.Height = fixedHeight.Value;
            // UIで正面の向きが指定されていれば上書き（temple のときのみ意味を持つ）。
            if (!string.IsNullOrWhiteSpace(facadeFace)) spec.FacadeFace = facadeFace;

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

    // 同じ指示で複数案を生成する。案ごとに temperature を変えて差を出す。
    // 成功・失敗を問わず count 件の結果を返す（呼び出し側で成否を見る）。
    public async Task<List<GenerationResult>> GenerateMultipleAsync(
    string model, string instruction, IReadOnlyList<string> allowedBlocks, int count = 3,
    string? stylePrompt = null,
    int? fixedWidth = null, int? fixedDepth = null, int? fixedHeight = null,
    string? facadeFace = null)
    {

        var results = new List<GenerationResult>();
        // 案ごとの temperature と方向性ヒント。temperatureだけでは差が出ないため
        // 方向性を明示的に変えて、確実に違う案を作る。
        var variants = new (double temp, string hint)[]
        {
            (0.3, "A standard, balanced version that fits the instruction faithfully."),
            (0.6, "A more compact version: slightly smaller footprint, cozy. Vary window/door placement."),
            (0.9, "A more spacious version: taller or larger, more windows. Consider a flat roof or a different ridge axis if it suits.")
        };
        for (int i = 0; i < count; i++)
        {
            var v = variants[i % variants.Length];
            var r = await GenerateAsync(model, instruction, allowedBlocks, v.temp, v.hint, stylePrompt,
                                        fixedWidth, fixedDepth, fixedHeight, facadeFace);
            results.Add(r);
        }
        return results;
    }

    // プリミティブ（曲面・造形物）を1案生成する。LLMは PrimitiveSpec を吐き、
    // PrimitiveExpander が確定的にボクセル化する。
    public async Task<GenerationResult> GeneratePrimitiveAsync(
        string model, string instruction, IReadOnlyList<string> allowedBlocks,
        double temperature = 0.3,
        int? fixedRadiusX = null, int? fixedRadiusY = null, int? fixedRadiusZ = null)
    {
        var result = new GenerationResult();
        string blockList = string.Join(", ", allowedBlocks);
        string prompt =
$@"You design a single Minecraft curved primitive (a rounded shape, NOT a building).
Output ONLY strict JSON, no prose. You do NOT output coordinates; you output a SPEC.
A separate program voxelizes your spec into blocks.

ALLOWED BLOCK IDS (use one exactly for the block field, never invent others):
{blockList}

OUTPUT SHAPE (this exact JSON shape):
{{
  ""shape"": ""ellipsoid"",
  ""radius_x"": 5,
  ""radius_y"": 4,
  ""radius_z"": 5,
  ""hollow"": false,
  ""block"": ""minecraft:stone""
}}

FIELD MEANING:
- shape: ""sphere"" (all radii equal) or ""ellipsoid"" (radii can differ, e.g. a dome
  is a flattened ellipsoid; an egg/blimp is elongated on one axis).
- radius_x / radius_y / radius_z: half-size in blocks along each axis (1-32).
  Make the shape match the instruction (e.g. tall -> larger radius_y).
- hollow: true to keep only the outer shell (a hollow dome/ball), false for solid.
- block: which allowed block to fill the shape with.

RULES:
- Choose radii that fit the requested object's proportions.
- Use only an allowed block ID for block.
- Output ONLY the JSON spec. No explanation, no coordinates.

BUILD INSTRUCTION:
{instruction}

JSON:";

        var payload = new
        {
            model,
            prompt,
            stream = false,
            format = "json",
            options = new { temperature }
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

            var spec = TryParsePrimitive(raw);
            if (spec == null)
            {
                result.Error = "PRIMITIVE(JSON)としてパースできませんでした。";
                return result;
            }

            // UIで寸法（直径）が確定されていれば、半径として上書きする（ブレ根絶）
            if (fixedRadiusX.HasValue) spec.RadiusX = fixedRadiusX.Value;
            if (fixedRadiusY.HasValue) spec.RadiusY = fixedRadiusY.Value;
            if (fixedRadiusZ.HasValue) spec.RadiusZ = fixedRadiusZ.Value;

            var blocks = PrimitiveExpander.Expand(spec, allowedBlocks);
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

    // 同じ指示で複数のプリミティブ案を生成（半径などをLLMの揺らぎで変える）。
    public async Task<List<GenerationResult>> GeneratePrimitiveMultipleAsync(
        string model, string instruction, IReadOnlyList<string> allowedBlocks, int count = 3,
        int? fixedRadiusX = null, int? fixedRadiusY = null, int? fixedRadiusZ = null)
    {
        var results = new List<GenerationResult>();
        double[] temps = { 0.3, 0.6, 0.9 };
        for (int i = 0; i < count; i++)
        {
            var r = await GeneratePrimitiveAsync(model, instruction, allowedBlocks,
                                                 temps[i % temps.Length],
                                                 fixedRadiusX, fixedRadiusY, fixedRadiusZ);
            results.Add(r);
        }
        return results;
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

    // 生出力から PrimitiveSpec を取り出してパース。
    private static PrimitiveSpec? TryParsePrimitive(string raw)
    {
        string candidate = raw.Trim();
        int start = candidate.IndexOf('{');
        int end = candidate.LastIndexOf('}');
        if (start >= 0 && end > start)
            candidate = candidate.Substring(start, end - start + 1);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PrimitiveSpec>(candidate, opts);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}