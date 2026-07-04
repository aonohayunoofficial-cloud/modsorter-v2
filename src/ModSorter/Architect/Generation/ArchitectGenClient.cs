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
    string? facadeFace = null, DesignPlan? plan = null)
    {

        var result = new GenerationResult();

        string blockList = string.Join(", ", allowedBlocks);
        bool isShipInstruction = IsShipInstruction(instruction);
        if (isShipInstruction) plan = null;
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
  ""structure_type"": ""building"",
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
  smoother sloped look), ""dome"" (a rounded domed roof for temples/observatories), or
  ""pyramid"" (a four-sided pyramidal roof tapering to a point, for pyramids, towers,
  or oriental-style roofs).
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
- structure_type: the overall kind of structure. ""building"" (the default: an ordinary
  building made of a floor, walls, a roof, and openings — use this for houses, towers,
  temples, etc.), ""ramp"" (a solid sloped walkway / incline that rises from the ground
  to full height, with no walls or roof — use this for ramps, inclines, and slopes),
  ""bridge"" (a flat horizontal deck carried high in the air on a few support piers, with
  low railings along both edges — use this for bridges and viaducts), or ""ship"" (a boat
  or ship with a tapered hull, a deck, and a superstructure — use this for any watercraft).
  For ""ramp"", ridge_axis chooses the direction it climbs: ""x"" rises along X (default),
  ""z"" rises along Z. For ""bridge"", ridge_axis chooses the direction it spans: ""x"" spans
  along X (default), ""z"" spans along Z. wall_block is used for the ramp body / bridge
  deck, and base_block for the ramp's bottom course / the bridge's piers and railings.
  roof_type, openings, floors and columns are IGNORED for non-""building"" types.
- For ""ship"", width = the beam (how wide across), depth = the length (bow-to-stern),
  height = how tall including the superstructure. The bow (pointed front) is at the
  z=0 side by default; set bow_face to ""south"" to point it the other way. Choose the
  kind of ship with ship_class (leave it out to auto-pick one from the size):
    ""rowboat"" (tiny open boat), ""motorboat"" (small powerboat with a cockpit),
    ""trawler"" (fishing/crab boat: tall wheelhouse near the bow, open work deck aft),
    ""caravel"" (small sailing ship), ""galleon"" (large sailing ship with fore/stern
    castles and masts), ""liner"" (large passenger ship with a tall multi-deck
    superstructure and funnels), ""cargo"" (freighter/tanker: bridge tower aft, long flat
    deck), ""destroyer"" (slim warship), ""battleship"" (heavy warship with gun turrets),
    ""carrier"" (flat full-length flight deck with a small island on the starboard side),
    ""submarine"" (rounded cigar hull with a conning tower).
  hull_block = the hull material, deck_block = the deck, superstructure_block = the
  cabins/bridge/island. All openings/doors are placed automatically per ship type;
  roof_type, building_style, openings, and floors are IGNORED for ""ship"".
- DECIDE structure_type FIRST, before anything else.
  - If the instruction describes a ramp, slope, incline, or sloped walkway (English
    ""ramp""/""slope""/""incline"" or Japanese ""スロープ""/""坂""/""坂道""/""傾斜""), you MUST set
    structure_type to ""ramp"".
  - If the instruction describes a bridge or viaduct (English ""bridge""/""viaduct"" or
    Japanese ""橋""/""ブリッジ""/""高架""), you MUST set structure_type to ""bridge"".
  - If the instruction describes a boat or ship (English ""boat""/""ship""/""vessel""/""fishing
    boat""/""galleon""/""submarine""/""carrier""/""liner""/""tanker""/""warship"" or Japanese
    ""船""/""ボート""/""漁船""/""帆船""/""ガレオン""/""潜水艦""/""空母""/""客船""/""タンカー""/""軍艦""),
    you MUST set structure_type to ""ship"" and pick a matching ship_class (or omit
    ship_class to auto-pick from the size).
  - Otherwise use ""building"" when the instruction describes a house, tower, temple,
    wall, or other walled/roofed structure.
  When in doubt, match the keyword: ramp/slope/スロープ/坂 -> ""ramp"";
  bridge/橋/ブリッジ -> ""bridge""; boat/ship/船/漁船/帆船 -> ""ship"";
  everything else -> ""building"".
  For a ""ramp"", width = its length, depth = its width, height = how high it climbs.
  For a ""bridge"", width = its span (length across), depth = its width, height = how high
  the deck sits above the ground. Pick wall_block as the requested deck/body material
  (e.g. stone bricks) and leave roof_type, building_style, and openings at their
  defaults (they are ignored for ramp and bridge).
  For a ""ship"", width = the beam, depth = the length, height = the total height; set
  hull_block/deck_block/superstructure_block for materials and leave roof_type,
  building_style, and openings at their defaults (they are ignored for ship).
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
- footprint_shape: the FLOOR PLAN shape, seen from above. Omit (or ""rect"") for an
  ordinary rectangular building — this is the default and correct choice for almost all
  buildings. Only set a non-rect shape when the instruction clearly asks for one:
    ""l""    = L-shaped plan (a rectangle with one corner bitten out),
    ""u""    = U-shaped / courtyard plan (a notch cut into the front-center),
    ""t""    = T-shaped plan (a cross-bar plus a central stem),
    ""plus"" = plus/cross-shaped plan (a central vertical band crossing a horizontal band).
  Japanese cues: ""L字"" -> ""l"", ""コの字"" -> ""u"", ""T字"" -> ""t"", ""十字"" -> ""plus"".
  When you set a non-rect footprint_shape, the roof is forced to flat and decorative
  styles (colonnade/temple, pilasters) are ignored, so keep roof_type ""flat"" and
  building_style ""walled"" for those shapes.
- footprint_params: optional sizing for footprint_shape, as {{ ""cut_w"": N, ""cut_d"": M }}
  where cut_w is the size of the notch/band along X and cut_d along Z. Omit to let it
  auto-size to about half the width/depth. Only meaningful with a non-rect shape.
- footprint_add / footprint_sub: OPTIONAL lists of rectangles to fine-tune the plan.
  Each rectangle is {{ ""x"": X, ""z"": Z, ""w"": W, ""d"": D }} in the 0..width-1 / 0..depth-1
  grid. All footprint_add rectangles are unioned onto the plan, then all footprint_sub
  rectangles are removed. Use these only for a specific wing or cut-out that the
  instruction explicitly describes; otherwise leave them as empty lists [].

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
  - kind: ""door"" (an empty opening), ""window"" (a glass cell), or ""arch""
    (a tall rounded archway opening from the floor up, with a curved top; good for
    temples, cathedrals, and grand entrances).
  - offset: position along that face (0 = one corner, up to width-1 or depth-1).
    For an arch, keep offset at least 1 away from corners so it has room to curve.
  - level: which middle layer, counting from the floor (1 = just above the floor).
    Ignored for ""arch"" (arches always start at the floor).

RULES:
- Interpret the instruction's size (e.g. ""5x5x4"" means width=5, depth=5, height=4).
- Use only allowed block IDs for *_block and window block.
- Keep openings on walls, not on corners. A small house has 1 door and a few windows.
- ALWAYS include at least one ""door"" opening so the building has an entrance. Even a
  tower or a windows-only design MUST have exactly one door, usually on the front face.
- Use accent_block + pilaster_step only when the style benefits from visible columns or
  framing (industrial, fortified, or structured looks). For plain or natural styles,
  omit them so the walls stay simple.
- Use has_base + base_block when a stone/brick foundation suits the building (most
  houses look good on one). Skip it for very simple or floating structures.
- Use roof_type ""dome"" for grand, rounded buildings (temples, observatories, domed
  halls). For ordinary houses prefer ""gable"" or ""flat"".
- Prefer ""gable_stairs"" over ""gable"" when a stair block is available and a nicer
  sloped roof suits the house; set roof_block to that stair block.
- Keep footprint_shape as ""rect"" (or omit it) unless the instruction explicitly asks
  for an L-shaped, U-shaped, T-shaped, or cross/plus-shaped plan. For any such non-rect
  plan, set roof_type ""flat"" and building_style ""walled"" (other roofs and columned
  styles are ignored for non-rectangular plans).

- Use building_style ""colonnade"" for fully open, columned structures (pavilions,
  shrines) with columns all around. Use ""temple"" when the instruction wants a
  classical facade: a solid room behind a front row of columns (a porticoed temple).
  For normal houses keep ""walled"". When colonnade or temple, openings are ignored.
- Output ONLY the JSON spec. No explanation, no coordinates.

{(string.IsNullOrEmpty(stylePrompt) ? "" : "STYLE / GENRE:\n" + stylePrompt + "\n\n")}{(plan == null || string.IsNullOrWhiteSpace(plan.DesignNotes) ? "" : "DESIGN PLAN (follow this plan; turn it into the JSON spec above):\n" + BuildPlanText(plan) + "\n\n")}BUILD INSTRUCTION:
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

            // ship_class が入っているのに structure_type が抜けている出力ムラを補正する。
            if (!string.IsNullOrWhiteSpace(spec.ShipClass) || isShipInstruction)
                spec.StructureType = "ship";

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

        // 2パス生成の1パス目: 指示から設計方針を1回だけ作り、3案で共有する。
        // 失敗時は plan=null となり、各案は従来どおり方針なし（1パス相当）で生成される。
        DesignPlan? plan = await PlanAsync(model, instruction, stylePrompt);

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
                                        fixedWidth, fixedDepth, fixedHeight, facadeFace, plan);
            results.Add(r);
        }
        return results;
    }

    // 2パス生成の1パス目。指示から設計方針（DesignPlan）を作る。
    // 失敗（接続不可・パース不可など）時は null を返し、呼び出し側は方針なしで2パス目に進む。
    // ここでは座標も SPEC も作らない。方針だけを短く出させる。
    public async Task<DesignPlan?> PlanAsync(
        string model, string instruction, string? stylePrompt = null,
        double temperature = 0.4)
    {
        string prompt =
$@"You are an architect planning a small Minecraft building BEFORE any spec is written.
Output ONLY strict JSON, no prose, no coordinates.

Read the instruction and decide the design DIRECTION: how many stories, the overall
style, the roof approach, decoration (columns/base/material contrast), and how openings
(doors/windows) should be arranged. Keep it high-level; do NOT pick exact coordinates
or block ids. A later step turns this plan into a detailed spec and then into blocks.

OUTPUT SHAPE (this exact JSON shape):
{{
  ""design_notes"": ""one short paragraph describing the building's character and how the parts fit together"",
  ""stories"": 1,
  ""style"": ""walled"",
  ""roof"": ""gable"",
  ""decoration"": ""plain walls, stone base course"",
  ""openings"": ""a door on the front, a couple of windows on the sides""
}}

RULES:
- design_notes is the main field: a concise plan in plain language.
- stories: integer number of floors implied by the instruction (default 1).
- style: the intended look (e.g. walled / colonnade / temple / cottage / industrial).
- roof: flat, gable, gable_stairs, or dome — whichever suits.
- Output ONLY the JSON. No explanation.

{(string.IsNullOrEmpty(stylePrompt) ? "" : "STYLE / GENRE:\n" + stylePrompt + "\n\n")}BUILD INSTRUCTION:
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
            if (!resp.IsSuccessStatusCode) return null;

            string respText = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respText);
            if (!doc.RootElement.TryGetProperty("response", out var respProp)) return null;

            string raw = respProp.GetString() ?? "";
            return TryParsePlan(raw);
        }
        catch
        {
            // 計画フェーズの失敗は致命ではない。null を返し、2パス目は方針なしで進む。
            return null;
        }
    }

    // 生出力から DesignPlan を取り出してパース。前後のゴミに備え '{'〜'}' を抜く。
    private static DesignPlan? TryParsePlan(string raw)
    {
        string candidate = raw.Trim();
        int start = candidate.IndexOf('{');
        int end = candidate.LastIndexOf('}');
        if (start >= 0 && end > start)
            candidate = candidate.Substring(start, end - start + 1);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var plan = JsonSerializer.Deserialize<DesignPlan>(candidate, opts);
            if (plan == null || string.IsNullOrWhiteSpace(plan.DesignNotes)) return null;
            return plan;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // DesignPlan を 2パス目プロンプトに添える短いテキストに整形する。
    private static string BuildPlanText(DesignPlan plan)
    {
        var sb = new StringBuilder();
        sb.Append(plan.DesignNotes.Trim());
        if (plan.Stories.HasValue) sb.Append($"\n- stories: {plan.Stories.Value}");
        if (!string.IsNullOrWhiteSpace(plan.Style)) sb.Append($"\n- style: {plan.Style!.Trim()}");
        if (!string.IsNullOrWhiteSpace(plan.Roof)) sb.Append($"\n- roof: {plan.Roof!.Trim()}");
        if (!string.IsNullOrWhiteSpace(plan.Decoration)) sb.Append($"\n- decoration: {plan.Decoration!.Trim()}");
        if (!string.IsNullOrWhiteSpace(plan.Openings)) sb.Append($"\n- openings: {plan.Openings!.Trim()}");
        return sb.ToString();
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

    // 生出力から StructureSpec を取り出してパース。前後にゴミが付く場合に備え
    // 最初の '{' から最後の '}' までを抜き出して試す。
    private static bool IsShipInstruction(string instruction)
    {
        if (string.IsNullOrEmpty(instruction)) return false;
        string s = instruction.ToLowerInvariant();
        string[] kws =
        {
            "boat", "ship", "vessel", "trawler", "galleon", "caravel", "liner",
            "tanker", "cargo", "warship", "destroyer", "battleship", "carrier",
            "submarine", "rowboat", "motorboat", "yacht",
            "船", "ボート", "漁船", "帆船", "ガレオン", "カラベル", "潜水艦",
            "空母", "客船", "タンカー", "貨物船", "軍艦", "駆逐艦", "戦艦", "ヨット"
        };
        foreach (var k in kws)
            if (s.Contains(k)) return true;
        return false;
    }

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