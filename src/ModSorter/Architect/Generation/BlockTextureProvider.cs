using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace ModSorter.Architect.Generation;

// ブロックID から テクスチャPNGのバイト列を取り出す。
// バニラ jar と各MOD jar の中の assets/<ns>/textures/block/<name>.png を探す。
// jarはzipなので ZipArchive で開く。見つかったものはキャッシュする。
public sealed class BlockTextureProvider : IDisposable
{
    // namespace ごとに開いた zip を保持(毎回開かない)。
    private readonly Dictionary<string, ZipArchive> _zips = new();
    // 取得済みPNGのキャッシュ(blockId → bytes or null)。
    private readonly Dictionary<string, byte[]?> _cache = new();
    // テクスチャパス(例 "create:block/shaft_side")→ PNG bytes のキャッシュ。
    private readonly Dictionary<string, byte[]?> _pathCache = new(StringComparer.Ordinal);

    // namespace(例 "minecraft","create") → そのassetsを含むjarパス
    private readonly Dictionary<string, string> _nsToJar = new();
    // 各 namespace で採用中の jar が持つ blockstates 件数(本体優先の判定用)。
    private readonly Dictionary<string, int> _nsBlockstateCount = new();

    public string LastError { get; private set; } = "";

    // vanillaJarPath: versions/<ver>/<ver>.jar(無ければ null/空でOK)
    // modJarPaths: 読み込み済みMODのjarパス群(ModEntry.FilePath)
    public BlockTextureProvider(string? vanillaJarPath, IEnumerable<string> modJarPaths)
    {
        // バニラは namespace "minecraft"。バニラは常に本体扱いで固定。
        if (!string.IsNullOrEmpty(vanillaJarPath) && File.Exists(vanillaJarPath))
        {
            _nsToJar["minecraft"] = vanillaJarPath!;
            _nsBlockstateCount["minecraft"] = int.MaxValue; // バニラは絶対に上書きされない
        }

        // MOD jar を走査。
        // 同名 namespace が複数の jar に現れる場合(create本体 + 派生MODが
        // assets/create を同梱、等)は「blockstates をより多く持つ jar」を採用する。
        // これで派生MODが namespace を先取りして本体が隠れる問題を防ぐ。
        foreach (var jar in modJarPaths.Distinct())
        {
            if (string.IsNullOrEmpty(jar) || !File.Exists(jar)) continue;
            try
            {
                using var za = ZipFile.OpenRead(jar);

                // この jar が持つ namespace ごとの blockstates 件数を数える。
                var counts = CountBlockstatesByNamespace(za);

                // textures しか持たない namespace も登録対象に含めるため、
                // namespace の集合は「textures or blockstates のどちらか」を持つもの。
                foreach (var ns in NamespacesInZip(za))
                {
                    int bsCount = counts.TryGetValue(ns, out var c) ? c : 0;

                    // 未登録なら採用。登録済みなら blockstates 件数が多い方を採用。
                    if (!_nsToJar.ContainsKey(ns))
                    {
                        _nsToJar[ns] = jar;
                        _nsBlockstateCount[ns] = bsCount;
                    }
                    else if (bsCount > _nsBlockstateCount[ns])
                    {
                        _nsToJar[ns] = jar;
                        _nsBlockstateCount[ns] = bsCount;
                    }
                }
            }
            catch { /* 壊れたjarは無視 */ }
        }
    }

    // zip内の assets/<ns>/ を列挙して namespace 名を集める。
    // textures または blockstates のどちらかを持つ namespace を対象にする。
    private static IEnumerable<string> NamespacesInZip(ZipArchive za)
    {
        var set = new HashSet<string>();
        foreach (var e in za.Entries)
        {
            // 例: assets/create/textures/...  または  assets/create/blockstates/...
            var parts = e.FullName.Split('/');
            if (parts.Length >= 3 && parts[0] == "assets" &&
                (parts[2].StartsWith("textures") || parts[2].StartsWith("blockstates")))
            {
                set.Add(parts[1]);
            }
        }
        return set;
    }

    // zip内の namespace ごとに assets/<ns>/blockstates/*.json の件数を数える。
    // 本体 jar かどうかの判定(件数が多い=本体)に使う。
    private static Dictionary<string, int> CountBlockstatesByNamespace(ZipArchive za)
    {
        var counts = new Dictionary<string, int>();
        foreach (var e in za.Entries)
        {
            var parts = e.FullName.Split('/');
            // assets / <ns> / blockstates / <name>.json
            if (parts.Length == 4 && parts[0] == "assets" && parts[2] == "blockstates" &&
                parts[3].EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                parts[3].Length > ".json".Length)
            {
                string ns = parts[1];
                counts[ns] = counts.TryGetValue(ns, out var c) ? c + 1 : 1;
            }
        }
        return counts;
    }

    // 診断用: 指定 namespace が今どの jar に紐づいているかを返す。
    public string? JarForNamespace(string ns)
        => _nsToJar.TryGetValue(ns, out var jar) ? jar : null;

    // blockId(例 "minecraft:oak_planks") のテクスチャPNGバイト列を返す。無ければ null。
    public byte[]? GetTexture(string blockId)
    {
        if (_cache.TryGetValue(blockId, out var cached)) return cached;

        byte[]? result = null;
        try
        {
            var (ns, name) = SplitId(blockId);
            if (_nsToJar.TryGetValue(ns, out var jar))
            {
                var za = GetZip(ns, jar);
                // 候補名: そのまま、_top、側面など。まずは素直に複数試す。
                string[] candidates =
                {
                    name,            // oak_planks
                    name + "_top",   // 原木など
                    name + "_side",
                    name + "_0"      // アニメ系の先頭
                };
                foreach (var cand in candidates)
                {
                    var path = $"assets/{ns}/textures/block/{cand}.png";
                    var entry = za.GetEntry(path);
                    if (entry == null) continue;
                    using var s = entry.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    result = ms.ToArray();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }

        _cache[blockId] = result;
        return result;
    }

    // テクスチャ参照パス(例 "create:block/shaft_side" や "block/oak_planks")から
    // PNG バイト列を返す。無ければ null。面別テクスチャ解決で使う。
    public byte[]? GetTextureByPath(string texRef)
    {
        if (string.IsNullOrEmpty(texRef)) return null;
        if (_pathCache.TryGetValue(texRef, out var cached)) return cached;

        byte[]? result = null;
        try
        {
            // ns の解決。ns 無しは minecraft 扱い(バニラモデルの慣習)。
            string ns, path;
            int c = texRef.IndexOf(':');
            if (c >= 0) { ns = texRef.Substring(0, c); path = texRef.Substring(c + 1); }
            else { ns = "minecraft"; path = texRef; }

            if (_nsToJar.TryGetValue(ns, out var jar))
            {
                var za = GetZip(ns, jar);
                var entry = za.GetEntry($"assets/{ns}/textures/{path}.png");
                if (entry != null)
                {
                    using var s = entry.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    result = ms.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }

        _pathCache[texRef] = result;
        return result;
    }

    private ZipArchive GetZip(string ns, string jar)
    {
        if (_zips.TryGetValue(ns, out var za)) return za;
        za = ZipFile.OpenRead(jar);
        _zips[ns] = za;
        return za;
    }

    private static (string ns, string name) SplitId(string blockId)
    {
        int i = blockId.IndexOf(':');
        if (i < 0) return ("minecraft", blockId);
        return (blockId.Substring(0, i), blockId.Substring(i + 1));
    }

    // 登録済みの全 namespace について、その jar に含まれるブロックIDを列挙する。
    // 列挙元は assets/<ns>/blockstates/*.json（実際に登録されたブロックに最も近い）。
    // 戻り値: namespace(modid) → そのMODのブロックID一覧(例 "create:andesite_casing")。
    // ブロック名(ファイル名)昇順で返す。
    public Dictionary<string, List<string>> EnumerateBlocks()
    {
        var result = new Dictionary<string, List<string>>();

        foreach (var kv in _nsToJar)
        {
            string ns = kv.Key;
            string jar = kv.Value;
            var ids = new List<string>();

            try
            {
                var za = GetZip(ns, jar);
                string prefix = $"assets/{ns}/blockstates/";
                foreach (var e in za.Entries)
                {
                    // assets/<ns>/blockstates/<name>.json のみ対象。
                    if (!e.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (!e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                    string rest = e.FullName.Substring(prefix.Length);
                    if (rest.Contains('/')) continue; // サブフォルダは除外
                    string name = rest.Substring(0, rest.Length - ".json".Length);
                    if (name.Length == 0) continue;

                    ids.Add($"{ns}:{name}");
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            if (ids.Count > 0)
            {
                ids.Sort(StringComparer.Ordinal);
                result[ns] = ids;
            }
        }

        return result;
    }

    // 登録済み全 namespace のブロックについて、blockstates JSON を解析し
    // ブロックID → プロパティ名 → 取りうる値リスト を返す。
    // 例: "create:shaft" -> { "axis" -> ["x","y","z"] }
    // variants を持たない multipart 形式や解析失敗は「プロパティなし」(空辞書)として扱う。
    public Dictionary<string, Dictionary<string, List<string>>> ExtractBlockPalette()
    {
        var result = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

        foreach (var kv in _nsToJar)
        {
            string ns = kv.Key;
            string jar = kv.Value;

            try
            {
                var za = GetZip(ns, jar);
                string prefix = $"assets/{ns}/blockstates/";
                foreach (var e in za.Entries)
                {
                    if (!e.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (!e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                    string rest = e.FullName.Substring(prefix.Length);
                    if (rest.Contains('/')) continue;
                    string name = rest.Substring(0, rest.Length - ".json".Length);
                    if (name.Length == 0) continue;

                    string blockId = $"{ns}:{name}";

                    // プロパティ名 -> 値集合(重複排除しつつ出現順保持)
                    var props = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                    try
                    {
                        string json;
                        using (var sr = new System.IO.StreamReader(e.Open()))
                            json = sr.ReadToEnd();

                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        // variants 形式: { "variants": { "axis=x": {...}, "facing=north,half=top": {...} } }
                        if (root.ValueKind == System.Text.Json.JsonValueKind.Object
                            && root.TryGetProperty("variants", out var variants)
                            && variants.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            foreach (var v in variants.EnumerateObject())
                            {
                                string key = v.Name;
                                if (key.Length == 0) continue;

                                foreach (var pair in key.Split(','))
                                {
                                    int eq = pair.IndexOf('=');
                                    if (eq <= 0) continue;
                                    string pName = pair.Substring(0, eq).Trim();
                                    string pVal = pair.Substring(eq + 1).Trim();
                                    if (pName.Length == 0) continue;

                                    if (!props.TryGetValue(pName, out var list))
                                    {
                                        list = new List<string>();
                                        props[pName] = list;
                                    }
                                    if (!list.Contains(pVal)) list.Add(pVal);
                                }
                            }
                        }
                        // multipart 形式は variants を持たない → props 空のまま。
                    }
                    catch
                    {
                        // 個別 JSON の解析失敗は黙殺し、プロパティなし扱いで続行。
                    }

                    result[blockId] = props;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        return result;
    }

    // ======================================================================
    // ブロックの「見た目形状(elements)」と「面ごとのテクスチャ」をモデルJSONから解決する。
    // Minecraft のモデルは assets/<ns>/models/block/<name>.json に定義され、
    // parent 継承・elements(小箱の集合)・textures(テクスチャ変数辞書)を持つ。
    // 各 element の faces で「どの面にどのテクスチャ変数を使うか」を指定する。
    // ======================================================================

    // 1個の小箱の1面ぶんのテクスチャ指定。
    // Tex は解決済みテクスチャ参照(例 "create:block/shaft_side")。
    // Uv は [x1,y1,x2,y2] の 0..16 座標。null なら from/to から自動算出させる。
    // Rotation は 0/90/180/270。
    public sealed class ShapeFace
    {
        public string Tex = "";
        public double[]? Uv = null;
        public int Rotation = 0;
    }

    // 1個の小箱。from/to は 0〜16 のピクセル座標。faces は面名→面テクスチャ指定。
    // 要素単位の回転(Minecraftモデルの element.rotation 相当、および簡易形状の円形配置用)を持つ。
    public sealed class ShapeElement
    {
        public double[] From = new double[3]; // x,y,z (0..16)
        public double[] To = new double[3];   // x,y,z (0..16)
        // 面名(north/south/east/west/up/down) → 解決済み面テクスチャ指定。
        // 面指定が無い面は含まれない。
        public Dictionary<string, ShapeFace> Faces = new(StringComparer.Ordinal);

        // 要素の回転(1段)。Minecraftモデルの element.rotation 相当。
        // 原点(RotOrigin)まわりに RotAxis 軸で RotAngle 度回す。cogwheel の45度歯車などで使う。
        public double RotAngle = 0;
        public string RotAxis = "x";
        public double[] RotOrigin = new double[] { 8, 8, 8 };
    }

    // ブロック1個ぶんの形状。elements と、姿勢回転(度)。
    // RotX/RotY は blockstates variant 由来(0/90/180/270)。
    // RotZ は belt の sideways など、コード側で決める姿勢用(任意角)。
    public sealed class BlockShape
    {
        public List<ShapeElement> Elements = new();
        public int RotX = 0;
        public int RotY = 0;
        public double RotZ = 0;
    }

    // 解決済みモデルのキャッシュ。model名 → elements。
    private readonly Dictionary<string, List<ShapeElement>?> _modelElemCache =
        new(StringComparer.Ordinal);

    // 一部の Create ブロックは、本体モデル(blockstates 経由)とは別に、
    // BlockEntityRenderer が動的描画する追加モデルを持つ(millstone の挽き臼など)。
    // これらは blockstates に現れないため、本体 elements にこの追加モデルの
    // elements を連結して「中身入り」に見せる。baseId → 追加モデル参照の対応表。
    private static readonly Dictionary<string, string[]> ExtraModels =
        new(StringComparer.Ordinal)
        {
            // 石臼: 中央の挽き臼(cogwheel)+軸は inner.json(BER描画)にある。
            ["create:millstone"] = new[] { "create:block/millstone/inner" },
            // ハンドクランク: 回すハンドル(Arm/Grip)は handle.json(BER描画)にあり、
            // block.json 本体は軸だけ。handle.json は静的 elements なので連結して見せる。
            ["create:hand_crank"] = new[] { "create:block/hand_crank/handle" },

        };

    // ブロックID + プロパティ から形状を解決する。
    public BlockShape? GetBlockShape(string blockId, IReadOnlyDictionary<string, string>? props)
    {
        try
        {
            var (ns, name) = SplitId(blockId);
            if (!_nsToJar.TryGetValue(ns, out var jar)) return null;
            var za = GetZip(ns, jar);

            var (modelRef, rotX, rotY) = ResolveVariantModel(za, ns, name, props);
            if (string.IsNullOrEmpty(modelRef)) return null;

            var elems = ResolveModelElements(za, ns, modelRef!, 0);
            if (elems == null || elems.Count == 0) return null;

            // BER 描画の追加モデル(millstone の inner 等)があれば elements を連結する。
            string baseId = $"{ns}:{name}";
            if (ExtraModels.TryGetValue(baseId, out var extras))
            {
                // ResolveModelElements は _modelElemCache の実体を返すため、そのまま
                // AddRange するとキャッシュを汚染し、2回目以降に追加モデルが二重連結される。
                // 新しいリストへ複製してから連結する。
                elems = new List<ShapeElement>(elems);
                foreach (var extraRef in extras)
                {
                    var extraElems = ResolveModelElements(za, ns, extraRef, 0);
                    if (extraElems != null && extraElems.Count > 0)
                        elems.AddRange(extraElems);
                }
            }

            return new BlockShape { Elements = elems, RotX = rotX, RotY = rotY };
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    // belt は blockstates を持たず、コード(BeltRenderer)がモデルと姿勢を決める特殊ブロック。
    // slope/part/facing から BeltRenderer.getBeltPartial と同じ写像でモデルを選び、
    // 既存の ResolveModelElements で elements を読む。姿勢は Rot X/Y/Z に載せる。
    // 水平・傾斜は top+bottom の2モデルを重ねる。斜め(diagonal)は1枚。
    // vertical/sideways は middle 系を回転で立てる。
    // props が取れない/belt 以外は null(呼び出し側で通常経路へ)。
    public BlockShape? GetBeltShape(string blockId, IReadOnlyDictionary<string, string>? props)
    {
        try
        {
            var (ns, name) = SplitId(blockId);
            if (name != "belt") return null;
            if (!_nsToJar.TryGetValue(ns, out var jar)) return null;
            var za = GetZip(ns, jar);

            // プロパティ読み取り(未指定は既定値)。
            string slope = GetProp(props, "slope", "horizontal");
            string part = GetProp(props, "part", "start");
            string facing = GetProp(props, "facing", "north");

            bool upward = slope == "upward";
            bool downward = slope == "downward";
            bool diagonal = upward || downward;
            bool vertical = slope == "vertical";
            bool sideways = slope == "sideways";
            bool start = part == "start";
            bool end = part == "end";

            // BeltRenderer: downward もしくは vertical かつ facing が正方向のとき start/end を入れ替える。
            var axisDir = FacingAxisDirection(facing); // +1 / -1
            if (downward || (vertical && axisDir > 0))
            {
                bool tmp = start; start = end; end = tmp;
            }

            // モデル名の写像(BeltRenderer.getBeltPartial 準拠)。
            // 水平・傾斜以外(vertical/sideways)は middle 系を使い、回転で姿勢を作る。
            var models = new List<string>();
            if (diagonal)
            {
                models.Add("belt/" + (start ? "diagonal_start" : end ? "diagonal_end" : "diagonal_middle"));
            }
            else
            {
                // top(bottom=false) と bottom(bottom=true) の2枚。
                models.Add("belt/" + (start ? "start" : end ? "end" : "middle"));
                models.Add("belt/" + (start ? "start_bottom" : end ? "end_bottom" : "middle_bottom"));
            }

            // elements を全モデル分マージ。
            var elems = new List<ShapeElement>();
            foreach (var m in models)
            {
                var part2 = ResolveModelElements(za, ns, "create:block/" + m, 0);
                if (part2 != null) elems.AddRange(part2);
            }
            if (elems.Count == 0) return null;

            // 姿勢回転(BeltRenderer の msr と同じ)。
            double yDeg = HorizontalAngle(facing) + (upward ? 180 : 0) + (sideways ? 270 : 0);
            double zDeg = sideways ? 90 : 0;
            double xDeg = (!diagonal && slope != "horizontal") ? 90 : 0; // vertical のみ90

            // diagonal の傾き45度は各 element の rotation(モデルJSON側)に既に入っているため、
            // ここでは追加しない。RotX/RotY は0/90/180/270、RotZは任意角。
            return new BlockShape
            {
                Elements = elems,
                RotX = (int)xDeg,
                RotY = ((int)yDeg % 360 + 360) % 360,
                RotZ = zDeg
            };
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    // props から値を取る(無ければ既定)。
    private static string GetProp(
        IReadOnlyDictionary<string, string>? props, string key, string def)
        => (props != null && props.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) ? v : def;

    // facing の水平角(北=180,南=0,西=90,東=270)。BeltRenderer の AngleHelper.horizontalAngle 準拠。
    private static double HorizontalAngle(string facing) => facing switch
    {
        "south" => 0,
        "west" => 90,
        "north" => 180,
        "east" => 270,
        _ => 180
    };

    // facing の軸方向(北/東=負, 南/西=正)を +1/-1 で返す。AxisDirection 準拠。
    private static int FacingAxisDirection(string facing) => facing switch
    {
        "south" => 1,
        "west" => 1,
        "north" => -1,
        "east" => -1,
        _ => -1
    };


    // blockstates/<name>.json を読み、props に合う variant の model 名と回転を返す。
    private (string? model, int rotX, int rotY) ResolveVariantModel(
        ZipArchive za, string ns, string name, IReadOnlyDictionary<string, string>? props)
    {
        var entry = za.GetEntry($"assets/{ns}/blockstates/{name}.json");
        if (entry == null) return (null, 0, 0);

        string json;
        using (var sr = new StreamReader(entry.Open())) json = sr.ReadToEnd();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return (null, 0, 0);
        if (!root.TryGetProperty("variants", out var variants) ||
            variants.ValueKind != JsonValueKind.Object)
            return (null, 0, 0); // multipart 等は非対応

        JsonElement bestVal = default;
        int bestScore = -1;
        bool found = false;

        foreach (var v in variants.EnumerateObject())
        {
            string key = v.Name;
            int score = 0;
            bool ok = true;

            if (key.Length > 0)
            {
                foreach (var pair in key.Split(','))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0) { ok = false; break; }
                    string pName = pair.Substring(0, eq).Trim();
                    string pVal = pair.Substring(eq + 1).Trim();
                    string? cur = null;
                    if (props != null) props.TryGetValue(pName, out cur);
                    if (!string.Equals(cur, pVal, StringComparison.Ordinal)) { ok = false; break; }
                    score++;
                }
            }
            if (!ok) continue;
            if (score > bestScore) { bestScore = score; bestVal = v.Value; found = true; }
        }

        if (!found) return (null, 0, 0);

        JsonElement obj = bestVal;
        if (bestVal.ValueKind == JsonValueKind.Array)
        {
            if (bestVal.GetArrayLength() == 0) return (null, 0, 0);
            obj = bestVal[0];
        }
        if (obj.ValueKind != JsonValueKind.Object) return (null, 0, 0);

        string? model = obj.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString() : null;
        int rx = obj.TryGetProperty("x", out var xe) && xe.ValueKind == JsonValueKind.Number
            ? xe.GetInt32() : 0;
        int ry = obj.TryGetProperty("y", out var ye) && ye.ValueKind == JsonValueKind.Number
            ? ye.GetInt32() : 0;

        return (model, rx, ry);
    }

    // model 参照名から elements(faces解決済み)を得る。
    // parent を辿り、textures 辞書を子優先でマージしながら、elements を持つ階層で
    // 各 face のテクスチャ変数を実パスへ解決する。
    private List<ShapeElement>? ResolveModelElements(
        ZipArchive za, string defaultNs, string modelRef, int depth)
    {
        string cacheKey = NormalizeModelKey(defaultNs, modelRef);
        if (_modelElemCache.TryGetValue(cacheKey, out var cached)) return cached;

        // textures 辞書を継承チェーン全体でマージしてから elements を解決する必要がある。
        var mergedTextures = new Dictionary<string, string>(StringComparer.Ordinal);
        var elems = ResolveElementsWithTextures(za, defaultNs, modelRef, 0, mergedTextures);

        _modelElemCache[cacheKey] = elems;
        return elems;
    }

    // 継承チェーンを辿りながら textures をマージし、elements を持つ階層で faces を解決する。
    // textures は「子が優先」。呼び出し時に子側の textures を先に inout 辞書へ入れておき、
    // 親の textures は未設定キーだけ補完する。
    private List<ShapeElement>? ResolveElementsWithTextures(
        ZipArchive za, string defaultNs, string modelRef, int depth,
        Dictionary<string, string> textures)
    {
        if (depth > 16) return null;

        string mns, mpath;
        int c = modelRef.IndexOf(':');
        if (c >= 0) { mns = modelRef.Substring(0, c); mpath = modelRef.Substring(c + 1); }
        else { mns = defaultNs; mpath = modelRef; }

        JsonDocument? doc = ReadModelJson(za, mns, mpath) ?? ReadModelJsonFromNs(mns, mpath);
        if (doc == null) return null;

        using (doc)
        {
            var root = doc.RootElement;

            // このモデルの textures を辞書へ取り込む(子優先なので未設定キーのみ補完)。
            if (root.TryGetProperty("textures", out var texEl) &&
                texEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in texEl.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.String) continue;
                    if (!textures.ContainsKey(p.Name))
                        textures[p.Name] = p.Value.GetString()!;
                }
            }

            // 自分が elements を持つならここで faces を解決して返す。
            if (root.TryGetProperty("elements", out var elemsEl) &&
                elemsEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<ShapeElement>();
                foreach (var e in elemsEl.EnumerateArray())
                {
                    if (!e.TryGetProperty("from", out var fe) ||
                        !e.TryGetProperty("to", out var te)) continue;
                    var from = ReadVec3(fe);
                    var to = ReadVec3(te);
                    if (from == null || to == null) continue;

                    var se = new ShapeElement { From = from, To = to };

                    // element.rotation: { origin:[x,y,z], axis:"x"|"y"|"z", angle:±22.5..±45 }
                    if (e.TryGetProperty("rotation", out var erot) &&
                        erot.ValueKind == JsonValueKind.Object)
                    {
                        if (erot.TryGetProperty("angle", out var ang) &&
                            ang.ValueKind == JsonValueKind.Number)
                            se.RotAngle = ang.GetDouble();
                        if (erot.TryGetProperty("axis", out var ax) &&
                            ax.ValueKind == JsonValueKind.String)
                            se.RotAxis = ax.GetString() ?? "x";
                        if (erot.TryGetProperty("origin", out var org) &&
                            org.ValueKind == JsonValueKind.Array)
                        {
                            var o = ReadVec3(org);
                            if (o != null) se.RotOrigin = o;
                        }
                    }

                    if (e.TryGetProperty("faces", out var facesEl) &&
                        facesEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var face in facesEl.EnumerateObject())
                        {
                            if (face.Value.ValueKind != JsonValueKind.Object) continue;
                            if (!face.Value.TryGetProperty("texture", out var texRefEl) ||
                                texRefEl.ValueKind != JsonValueKind.String) continue;
                            string resolved = ResolveTextureVar(texRefEl.GetString()!, textures);
                            if (string.IsNullOrEmpty(resolved)) continue;

                            var sf = new ShapeFace { Tex = resolved };

                            // uv: [x1,y1,x2,y2] 0..16。省略時は null(JS側で from/to から算出)。
                            if (face.Value.TryGetProperty("uv", out var uvEl) &&
                                uvEl.ValueKind == JsonValueKind.Array &&
                                uvEl.GetArrayLength() >= 4)
                            {
                                var uv = new double[4];
                                int i = 0;
                                foreach (var n in uvEl.EnumerateArray())
                                {
                                    if (i >= 4) break;
                                    if (n.ValueKind != JsonValueKind.Number) { i = -1; break; }
                                    uv[i++] = n.GetDouble();
                                }
                                if (i == 4) sf.Uv = uv;
                            }

                            // rotation: 面テクスチャの回転(0/90/180/270)。
                            if (face.Value.TryGetProperty("rotation", out var rotEl) &&
                                rotEl.ValueKind == JsonValueKind.Number)
                            {
                                sf.Rotation = rotEl.GetInt32();
                            }

                            se.Faces[face.Name] = sf;
                        }
                    }
                    list.Add(se);
                }
                if (list.Count > 0) return list;
            }

            // elements が無ければ親を辿る(textures は既にこの階層ぶん取り込み済み)。
            if (root.TryGetProperty("parent", out var pe) &&
                pe.ValueKind == JsonValueKind.String)
            {
                return ResolveElementsWithTextures(za, mns, pe.GetString()!, depth + 1, textures);
            }
        }
        return null;
    }

    // テクスチャ変数("#side" 等)を textures 辞書で実パスへ解決する。
    // 変数が別の変数を指す多段参照("#0" → "#texture" → 実パス)にも対応(上限あり)。
    private static string ResolveTextureVar(string texRef, Dictionary<string, string> textures)
    {
        string cur = texRef;
        for (int i = 0; i < 8; i++)
        {
            if (cur.Length == 0) return "";
            if (cur[0] != '#') return cur; // 変数でない = 実パス
            string key = cur.Substring(1);
            if (!textures.TryGetValue(key, out var next)) return ""; // 未解決
            cur = next;
        }
        return ""; // 循環など
    }

    private static string NormalizeModelKey(string defaultNs, string modelRef)
    {
        int c = modelRef.IndexOf(':');
        if (c >= 0) return modelRef;
        return $"{defaultNs}:{modelRef}";
    }

    private JsonDocument? ReadModelJson(ZipArchive za, string ns, string mpath)
    {
        string rel = mpath.Contains('/') ? mpath : $"block/{mpath}";
        var entry = za.GetEntry($"assets/{ns}/models/{rel}.json");
        if (entry == null) return null;
        try
        {
            string json;
            using (var sr = new StreamReader(entry.Open())) json = sr.ReadToEnd();
            return JsonDocument.Parse(json);
        }
        catch { return null; }
    }

    private JsonDocument? ReadModelJsonFromNs(string ns, string mpath)
    {
        if (!_nsToJar.TryGetValue(ns, out var jar)) return null;
        try
        {
            var za = GetZip(ns, jar);
            return ReadModelJson(za, ns, mpath);
        }
        catch { return null; }
    }

    private static double[]? ReadVec3(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() < 3) return null;
        var v = new double[3];
        int i = 0;
        foreach (var n in arr.EnumerateArray())
        {
            if (i >= 3) break;
            if (n.ValueKind != JsonValueKind.Number) return null;
            v[i++] = n.GetDouble();
        }
        return i == 3 ? v : null;
    }

    // water_wheel / large_water_wheel / crushing_wheel / flywheel は blockstates の
    // box モデルでは本体が取れず Forge OBJ ローダーで描画される。これらの OBJ を
    // ObjModelLoader で読み、三角形リスト(ObjMesh)として返す。対象外/失敗時は null。
    // b.Id は "create:water_wheel" 形式(状態[...]付きの場合は除去してから判定)。
    // OBJ が参照する assets は namespace ごとに開いた zip(GetZip)から取り出す。
    public ObjMesh? GetObjMesh(string blockId)
    {
        try
        {
            string baseId = blockId.Split('[')[0];
            if (!ObjModelLoader.IsObjBlock(baseId)) return null;

            return ObjModelLoader.Load(baseId, ns =>
                _nsToJar.TryGetValue(ns, out var jar) ? GetZip(ns, jar) : null);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var za in _zips.Values)
        {
            try { za.Dispose(); } catch { }
        }
        _zips.Clear();
    }
}
