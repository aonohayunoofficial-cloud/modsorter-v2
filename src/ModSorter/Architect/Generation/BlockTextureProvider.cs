using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

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

    public void Dispose()
    {
        foreach (var za in _zips.Values)
        {
            try { za.Dispose(); } catch { }
        }
        _zips.Clear();
    }
}
