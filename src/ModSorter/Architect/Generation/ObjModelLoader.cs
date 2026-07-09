using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace ModSorter.Architect.Generation;

// Create の water_wheel / large_water_wheel / crushing_wheel / flywheel は
// blockstates の box モデルではなく Forge OBJ ローダーで描画される。
// これらを OBJ から読み、three.js の BufferGeometry に載せられる三角形リスト
// (頂点座標・UV・面ごとの texKey) へ変換する。
//
// OBJ 頂点は 0..1 のブロック単位(車輪系は -0.87..1.87 まではみ出す)。
// PreviewHtml の addBlock は「x-0.5」でブロック中心原点に置く前提なので、
// ここでは 0..1 のまま返し、-0.5 平行移動は JS 側に任せる。
//
// マテリアル→テクスチャ解決:
//   MTL の "map_Kd #var" の #var を、参照JSON(textures系)の "textures" 定義で
//   実テクスチャパス(例 create:block/axis)へ解決する。flywheel はキーが数字
//   ("0"/"1"/"2")、他は名前("axis"/"log"等)。両対応する。
public sealed class ObjMesh
{
    // 三角形1枚 = 頂点3つ(各: 位置xyz + uv)。texKey は解決済みテクスチャ参照。
    public sealed class Tri
    {
        public double[] P0 = new double[3];
        public double[] P1 = new double[3];
        public double[] P2 = new double[3];
        public double[] Uv0 = new double[2];
        public double[] Uv1 = new double[2];
        public double[] Uv2 = new double[2];
        public string TexKey = "";
    }

    public List<Tri> Tris { get; } = new();
    // このメッシュが使う texKey 一覧(テクスチャ収集用)。
    public HashSet<string> TexKeys { get; } = new(StringComparer.Ordinal);
}

public static class ObjModelLoader
{
    // blockId(baseId) → 参照JSON群。1ブロックが複数OBJを持つ場合(large_water_wheel)は
    // 複数の参照JSONを並べる。参照JSONは "loader":"forge:obj" を持つモデルJSON。
    // 値: (参照JSONの assets 相対パス) の配列。
    private static readonly Dictionary<string, string[]> ObjRefJsonPaths =
        new(StringComparer.Ordinal)
        {
            // water_wheel は block.json が静的枠で、車輪本体は wheel.json が OBJ を指す。
            ["create:water_wheel"] = new[]
            {
                "assets/create/models/block/water_wheel/wheel.json",
            },
            // large_water_wheel は block.json(本体) + block_extension.json(拡張) の2 OBJ。
            ["create:large_water_wheel"] = new[]
            {
                "assets/create/models/block/large_water_wheel/block.json",
                "assets/create/models/block/large_water_wheel/block_extension.json",
            },
            // crushing_wheel は block.json 自体が OBJ 参照。
            ["create:crushing_wheel"] = new[]
            {
                "assets/create/models/block/crushing_wheel/block.json",
            },
            // flywheel は block.json が OBJ(軸なし版 flywheel_shaftless.obj)を指す。
            ["create:flywheel"] = new[]
            {
                "assets/create/models/block/flywheel/block.json",
            },
        };

    // このブロックが OBJ 描画対象か。
    public static bool IsObjBlock(string baseId) => ObjRefJsonPaths.ContainsKey(baseId);

    // baseId の OBJ メッシュを読み、結合して返す。対象外/失敗時は null。
    // zipResolver: namespace("create"等) → その assets を含む ZipArchive を返す関数。
    public static ObjMesh? Load(string baseId, Func<string, ZipArchive?> zipResolver)
    {
        if (!ObjRefJsonPaths.TryGetValue(baseId, out var refPaths)) return null;

        var za = zipResolver("create");
        if (za == null) return null;

        var mesh = new ObjMesh();
        try
        {
            foreach (var refPath in refPaths)
            {
                var refJson = ReadZipText(za, refPath);
                if (refJson == null) continue;

                using var doc = JsonDocument.Parse(refJson);
                var root = doc.RootElement;

                // OBJ の assets 相対パス。"model":"create:models/block/.../x.obj"
                if (!root.TryGetProperty("model", out var modelEl)) continue;
                string modelRef = modelEl.GetString() ?? "";
                string objPath = ModelRefToAssetPath(modelRef);
                if (objPath.Length == 0) continue;

                // parent の textures 定義を集める(#var → 実テクスチャパス)。
                var texVars = ResolveTextureVars(za, root);

                bool flipV = root.TryGetProperty("flip_v", out var fv) && fv.ValueKind == JsonValueKind.True;

                var objText = ReadZipText(za, objPath);
                if (objText == null) continue;

                // OBJ と同じフォルダの MTL を読む(mtllib 行)。
                string objDir = objPath.Contains('/')
                    ? objPath.Substring(0, objPath.LastIndexOf('/') + 1)
                    : "";

                ParseObjInto(mesh, objText, za, objDir, texVars, flipV);
            }
        }
        catch
        {
            return mesh.Tris.Count > 0 ? mesh : null;
        }

        return mesh.Tris.Count > 0 ? mesh : null;
    }

    // "create:models/block/water_wheel/water_wheel.obj"
    //   → "assets/create/models/block/water_wheel/water_wheel.obj"
    private static string ModelRefToAssetPath(string modelRef)
    {
        if (string.IsNullOrEmpty(modelRef)) return "";
        int c = modelRef.IndexOf(':');
        string ns = c >= 0 ? modelRef.Substring(0, c) : "minecraft";
        string rel = c >= 0 ? modelRef.Substring(c + 1) : modelRef;
        return $"assets/{ns}/{rel}";
    }

    // 参照JSON(またはその parent 連鎖)の "textures" を集めて #var→実パスの辞書にする。
    // parent が "create:block/xxx/textures" 形式ならそれも assets パスへ辿って読む。
    private static Dictionary<string, string> ResolveTextureVars(ZipArchive za, JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        // parent を先に辿り、子で上書きする(子優先)。深さ上限8で無限ループ回避。
        var chain = new List<JsonElement> { root };
        var cur = root;
        for (int depth = 0; depth < 8; depth++)
        {
            if (!cur.TryGetProperty("parent", out var pEl)) break;
            string parentRef = pEl.GetString() ?? "";
            if (parentRef.Length == 0) break;
            // parent は "block/block" のようなバニラ既定に行き着くと textures を持たない。
            string pPath = ModelRefToAssetPath(parentRef.Contains(':') ? parentRef : "minecraft:" + parentRef)
                .Replace("assets/minecraft/block/", "assets/minecraft/models/block/");
            // create:block/xxx/textures → assets/create/models/block/xxx/textures.json
            if (!pPath.Contains("/models/"))
                pPath = pPath.Replace("assets/", "assets/").Replace("/block/", "/models/block/");
            if (!pPath.EndsWith(".json")) pPath += ".json";

            var pText = ReadZipText(za, pPath);
            if (pText == null) break;
            try
            {
                var pdoc = JsonDocument.Parse(pText);
                chain.Add(pdoc.RootElement.Clone());
                cur = pdoc.RootElement.Clone();
            }
            catch { break; }
        }

        // 親→子の順にマージ(子が後で上書き)。
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i].TryGetProperty("textures", out var texEl) &&
                texEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in texEl.EnumerateObject())
                {
                    string v = p.Value.GetString() ?? "";
                    map[p.Name] = v;
                }
            }
        }

        // "#planks" のような参照はチェーンで実パスへ解決する。
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in map)
            resolved[kv.Key] = ResolveVar(kv.Value, map, 0);
        return resolved;
    }

    private static string ResolveVar(string v, Dictionary<string, string> map, int depth)
    {
        if (depth > 8 || string.IsNullOrEmpty(v)) return v;
        if (v.StartsWith("#"))
        {
            string key = v.Substring(1);
            return map.TryGetValue(key, out var next) ? ResolveVar(next, map, depth + 1) : v;
        }
        return v;
    }

    // MTL を読み、"newmtl 名" → "map_Kd の #var" の対応を作る。
    private static Dictionary<string, string> ParseMtl(ZipArchive za, string mtlPath)
    {
        var mat2var = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = ReadZipText(za, mtlPath);
        if (text == null) return mat2var;

        string curMat = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("newmtl "))
                curMat = line.Substring("newmtl ".Length).Trim();
            else if (line.StartsWith("map_Kd ") && curMat.Length > 0)
            {
                string arg = line.Substring("map_Kd ".Length).Trim();
                // "#axis" のような変数参照。先頭 # を外してキー名に。
                mat2var[curMat] = arg.StartsWith("#") ? arg.Substring(1) : arg;
            }
        }
        return mat2var;
    }

    // OBJ 本文をパースして mesh に三角形を足す。
    private static void ParseObjInto(
        ObjMesh mesh, string objText, ZipArchive za, string objDir,
        Dictionary<string, string> texVars, bool flipV)
    {
        var verts = new List<double[]>();
        var uvs = new List<double[]>();
        Dictionary<string, string> mat2var = new(StringComparer.Ordinal);
        string curTexKey = "";

        var ci = CultureInfo.InvariantCulture;

        foreach (var raw in objText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            if (line.StartsWith("mtllib "))
            {
                string mtlName = line.Substring("mtllib ".Length).Trim();
                mat2var = ParseMtl(za, objDir + mtlName);
            }
            else if (line.StartsWith("v "))
            {
                var t = line.Substring(2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length >= 3)
                    verts.Add(new[]
                    {
                        double.Parse(t[0], ci), double.Parse(t[1], ci), double.Parse(t[2], ci)
                    });
            }
            else if (line.StartsWith("vt "))
            {
                var t = line.Substring(3).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length >= 2)
                {
                    double u = double.Parse(t[0], ci);
                    double v = double.Parse(t[1], ci);
                    if (flipV) v = 1.0 - v;
                    uvs.Add(new[] { u, v });
                }
            }
            else if (line.StartsWith("usemtl "))
            {
                string mat = line.Substring("usemtl ".Length).Trim();
                string var = mat2var.TryGetValue(mat, out var vv) ? vv : "";
                // #var を textures 定義で実パスへ。数字キー("0")も名前キーも同じ辞書で引く。
                curTexKey = texVars.TryGetValue(var, out var tk) ? tk : "";
            }
            else if (line.StartsWith("f "))
            {
                var toks = line.Substring(2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // 各トークン "v/vt/vn"。頂点インデックス(1始まり)と UV インデックスを取る。
                var poly = new List<(int vi, int ti)>();
                foreach (var tok in toks)
                {
                    var parts = tok.Split('/');
                    int vi = ParseIndex(parts.Length > 0 ? parts[0] : "", verts.Count);
                    int ti = ParseIndex(parts.Length > 1 ? parts[1] : "", uvs.Count);
                    poly.Add((vi, ti));
                }
                // 三角形/四角形/多角形をファン分割して三角形へ。
                for (int i = 1; i + 1 < poly.Count; i++)
                    AddTri(mesh, verts, uvs, poly[0], poly[i], poly[i + 1], curTexKey);
            }
        }
    }

    // OBJ のインデックス(1始まり・負数は末尾相対)を 0 始まり配列添字へ。無効は -1。
    private static int ParseIndex(string s, int count)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        if (!int.TryParse(s, out int idx)) return -1;
        if (idx > 0) return idx - 1;
        if (idx < 0) return count + idx;
        return -1;
    }

    private static void AddTri(
        ObjMesh mesh, List<double[]> verts, List<double[]> uvs,
        (int vi, int ti) a, (int vi, int ti) b, (int vi, int ti) c, string texKey)
    {
        if (a.vi < 0 || b.vi < 0 || c.vi < 0) return;
        if (a.vi >= verts.Count || b.vi >= verts.Count || c.vi >= verts.Count) return;

        var tri = new ObjMesh.Tri
        {
            P0 = verts[a.vi],
            P1 = verts[b.vi],
            P2 = verts[c.vi],
            Uv0 = SafeUv(uvs, a.ti),
            Uv1 = SafeUv(uvs, b.ti),
            Uv2 = SafeUv(uvs, c.ti),
            TexKey = texKey
        };
        mesh.Tris.Add(tri);
        if (texKey.Length > 0) mesh.TexKeys.Add(texKey);
    }

    private static double[] SafeUv(List<double[]> uvs, int ti)
        => (ti >= 0 && ti < uvs.Count) ? uvs[ti] : new double[] { 0, 0 };

    private static string? ReadZipText(ZipArchive za, string path)
    {
        var entry = za.GetEntry(path);
        if (entry == null) return null;
        using var s = entry.Open();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
