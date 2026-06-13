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

    public string LastError { get; private set; } = "";

    // vanillaJarPath: versions/<ver>/<ver>.jar(無ければ null/空でOK)
    // modJarPaths: 読み込み済みMODのjarパス群(ModEntry.FilePath)
    public BlockTextureProvider(string? vanillaJarPath, IEnumerable<string> modJarPaths)
    {
        // バニラは namespace "minecraft"。
        if (!string.IsNullOrEmpty(vanillaJarPath) && File.Exists(vanillaJarPath))
            _nsToJar["minecraft"] = vanillaJarPath!;

        // MOD jar は、中の assets/ 直下のフォルダ名を namespace として登録。
        foreach (var jar in modJarPaths.Distinct())
        {
            if (string.IsNullOrEmpty(jar) || !File.Exists(jar)) continue;
            try
            {
                using var za = ZipFile.OpenRead(jar);
                foreach (var ns in NamespacesInZip(za))
                {
                    // 同名namespaceが複数あったら先勝ち(雑だが実用上は十分)。
                    if (!_nsToJar.ContainsKey(ns)) _nsToJar[ns] = jar;
                }
            }
            catch { /* 壊れたjarは無視 */ }
        }
    }

    // zip内の assets/<ns>/ を列挙して namespace 名を集める。
    private static IEnumerable<string> NamespacesInZip(ZipArchive za)
    {
        var set = new HashSet<string>();
        foreach (var e in za.Entries)
        {
            // 例: assets/create/textures/...
            var parts = e.FullName.Split('/');
            if (parts.Length >= 3 && parts[0] == "assets" &&
                parts[2].StartsWith("textures"))
            {
                set.Add(parts[1]);
            }
        }
        return set;
    }

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

    public void Dispose()
    {
        foreach (var za in _zips.Values)
        {
            try { za.Dispose(); } catch { }
        }
        _zips.Clear();
    }
}
