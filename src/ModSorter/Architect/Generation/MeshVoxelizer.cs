using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SharpGLTF.Schema2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
// SharpGLTF.Schema2 にも Image 型があり衝突するため、ImageSharp 側を別名にする。
using ISImage = SixLabors.ImageSharp.Image;

namespace ModSorter.Architect.Generation;

// GLB(表面メッシュ) を Minecraft ブロック集合へ変換する。
// 前段: GLB → ボクセルグリッド(セルごとに色を保持)
// 後段: セルの色を ColorMatcher でブロックIDに変換
public static class MeshVoxelizer
{
    public enum FillMode { Hollow, Solid }

    // 三角形: 位置3点 + 代表色(RGB)。色が取れなければ null。
    private struct Triangle
    {
        public Vector3 A, B, C;
        public (byte r, byte g, byte b)? Color;
    }

    // 中間表現: 存在セルと、その代表色(平均用に合計と件数を持つ)。
    public sealed class VoxelGrid
    {
        public int Resolution;
        public HashSet<(int x, int y, int z)> Cells = new();
        // セル → 色合計(平均を取るため)
        public Dictionary<(int, int, int), (long r, long g, long b, int n)> ColorSum = new();
    }

    // エントリポイント。matcher が null/候補無しのときは fallbackBlockId で単色。
    public static GenerationResult Voxelize(
        string glbPath, int resolution, FillMode fill,
        string fallbackBlockId, ColorMatcher? matcher = null)
    {
        var result = new GenerationResult();
        result.Error = "[M0] 開始";
        try
        {
            result.Error = "[M1] LoadTriangles 呼び出し直前";
            var tris = LoadTriangles(glbPath);
            result.Error = $"[M2] LoadTriangles 完了 tris={tris.Count}";

            if (tris.Count == 0)
            {
                result.Error =
                    "三角形が 0 件でした。GLB にメッシュが無いか、POSITION が読めていません。";
                return result;
            }

            result.Error = "[M3] BuildGridFromTris 呼び出し直前";
            var grid = BuildGridFromTris(tris, resolution, fill);
            if (grid.Cells.Count == 0)
            {
                result.Error = $"セルが 0 件 (tris={tris.Count})";
                return result;
            }

            bool useColor = matcher != null && matcher.HasCandidates;

            // ブロックIDごとに「出現数・RGB合計」を集計してログ用に貯める。
            // 窓色と壁色が別RGB/別ブロックに分離できているかを後で確認するため。
            var stat = new Dictionary<string, (int n, long r, long g, long b)>();
            // 診断: ガンマ補正を通す前の「生RGB」の彩度分布を見る。
            // 生がそもそも無彩色なのか、後段で潰しているのかを切り分けるため。
            int rawGray = 0, rawColored = 0;       // 彩度(max-min)が小さい/大きい生ピクセル数
            long rawSatSum = 0;                     // 生の彩度合計(平均彩度を出す)
            int rawSamples = 0;
            long boostSatSum = 0;                   // 彩度ブースト後の彩度合計

            var blocks = new List<GeneratedBlock>();
            foreach (var c in grid.Cells
                .OrderBy(c => c.y).ThenBy(c => c.z).ThenBy(c => c.x))
            {
                string id = fallbackBlockId;
                if (useColor && grid.ColorSum.TryGetValue(c, out var s) && s.n > 0)
                {
                    int rawR = (int)(s.r / s.n);
                    int rawG = (int)(s.g / s.n);
                    int rawB = (int)(s.b / s.n);

                    // --- 診断: 生RGBの彩度(max-min)を集計 ---
                    int sat = Math.Max(rawR, Math.Max(rawG, rawB))
                            - Math.Min(rawR, Math.Min(rawG, rawB));
                    rawSatSum += sat;
                    rawSamples++;
                    if (sat <= 12) rawGray++; else rawColored++;

                    // TRELLIS.2は彩度が低い素材+陰影焼き込みで色が数値的に無彩色へ潰れる。
                    // まず彩度を拡大して色相を立ててから、ガンマで明度を持ち上げる。
                    var (br, bg, bb2) = Saturate(rawR, rawG, rawB);
                    int sr = Brighten(br);
                    int sg = Brighten(bg);
                    int sb = Brighten(bb2);

                    // 診断: ブースト+ガンマ後の彩度。これでも低いままなら処理不足。
                    boostSatSum += Math.Max(sr, Math.Max(sg, sb))
                                 - Math.Min(sr, Math.Min(sg, sb));

                    id = matcher!.Nearest(sr, sg, sb, fallbackBlockId);

                    stat.TryGetValue(id, out var acc);
                    stat[id] = (acc.n + 1, acc.r + sr, acc.g + sg, acc.b + sb);
                }
                blocks.Add(new GeneratedBlock { X = c.x, Y = c.y, Z = c.z, Id = id });
            }

            // 集計ログを MatchLog に格納(呼び出し側で AppendLog に流す)。
            // 出現数の多い順。各行: ブロックID  個数  平均RGB
            var sb2 = new System.Text.StringBuilder();
            sb2.AppendLine($"[色マッチ集計] useColor={useColor}  色付きセル {stat.Values.Sum(v => v.n)} 件  総セル {grid.Cells.Count} 件  → {stat.Count} 種類");
            // 生RGB(ガンマ前)の彩度診断。これが「灰色だらけ」なら入力(GLBテクスチャ)が
            // そもそも無彩色 = ボクセル側の問題。彩度があるのに結果が石なら距離/パレット側。
            if (rawSamples > 0)
            {
                sb2.AppendLine(
                    $"[生RGB診断] 生平均彩度={rawSatSum / rawSamples}  " +
                    $"補正後平均彩度={boostSatSum / rawSamples}  " +
                    $"ほぼ無彩色(<=12)={rawGray}  有彩色={rawColored}  / 計{rawSamples}");
                sb2.AppendLine(
                    $"[候補診断] 色マッチ候補ブロック数={matcher!.CandidateCount}");
            }
            foreach (var kv in stat.OrderByDescending(kv => kv.Value.n))
            {
                var v = kv.Value;
                sb2.AppendLine(
                    $"  {kv.Key}  x{v.n}  avgRGB=({v.r / v.n},{v.g / v.n},{v.b / v.n})");
            }
            result.MatchLog = sb2.ToString().TrimEnd();

            // ログが流れて消えても確認できるよう、デスクトップに必ず書き出す。
            try
            {
                string dump = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "colormatch_dump.txt");
                System.IO.File.WriteAllText(dump, sb2.ToString());
            }
            catch { }

            result.Error = null;
            result.Blocks = blocks;
            return result;
        }
        catch (Exception ex)
        {
            result.Error =
                $"{ex.GetType().Name}: {ex.Message}" +
                (ex.InnerException != null
                    ? $" / inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                    : "");
            return result;
        }
    }

    // ── 前段: 三角形リスト → ボクセルグリッド(色付き) ──
    private static VoxelGrid BuildGridFromTris(
        List<Triangle> tris, int resolution, FillMode fill)
    {
        var grid = new VoxelGrid { Resolution = resolution };
        if (tris.Count == 0) return grid;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var t in tris)
            foreach (var p in new[] { t.A, t.B, t.C })
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

        Vector3 size = max - min;
        float maxEdge = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (maxEdge <= 0) return grid;
        float voxelSize = maxEdge / resolution;

        (int x, int y, int z) ToCell(Vector3 p)
        {
            int cx = Math.Clamp((int)((p.X - min.X) / voxelSize), 0, resolution - 1);
            int cy = Math.Clamp((int)((p.Y - min.Y) / voxelSize), 0, resolution - 1);
            int cz = Math.Clamp((int)((p.Z - min.Z) / voxelSize), 0, resolution - 1);
            return (cx, cy, cz);
        }

        void AddCell((int, int, int) cell, (byte r, byte g, byte b)? col)
        {
            grid.Cells.Add(cell);
            if (col == null) return;
            grid.ColorSum.TryGetValue(cell, out var s);
            grid.ColorSum[cell] =
                (s.r + col.Value.r, s.g + col.Value.g, s.b + col.Value.b, s.n + 1);
        }

        foreach (var t in tris)
        {
            float edgeLen = Math.Max(
                Vector3.Distance(t.A, t.B),
                Math.Max(Vector3.Distance(t.B, t.C), Vector3.Distance(t.C, t.A)));
            int steps = Math.Max(2, (int)(edgeLen / voxelSize) + 1);

            for (int i = 0; i <= steps; i++)
                for (int j = 0; j <= steps - i; j++)
                {
                    float u = (float)i / steps;
                    float v = (float)j / steps;
                    float w = 1 - u - v;
                    if (w < 0) continue;
                    Vector3 p = t.A * w + t.B * u + t.C * v;
                    AddCell(ToCell(p), t.Color);
                }
        }

        if (fill == FillMode.Solid)
            FillInterior(grid, resolution);

        return grid;
    }

    private static void FillInterior(VoxelGrid grid, int res)
    {
        var outside = new HashSet<(int, int, int)>();
        var queue = new Queue<(int x, int y, int z)>();

        for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
                for (int z = 0; z < res; z++)
                {
                    bool onBoundary = x == 0 || y == 0 || z == 0 ||
                                      x == res - 1 || y == res - 1 || z == res - 1;
                    if (!onBoundary) continue;
                    var c = (x, y, z);
                    if (grid.Cells.Contains(c)) continue;
                    if (outside.Add(c)) queue.Enqueue(c);
                }

        var dirs = new (int dx, int dy, int dz)[]
        { (1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1) };

        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            foreach (var (dx, dy, dz) in dirs)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (nx < 0 || ny < 0 || nz < 0 ||
                    nx >= res || ny >= res || nz >= res) continue;
                var nc = (nx, ny, nz);
                if (grid.Cells.Contains(nc)) continue;
                if (outside.Add(nc)) queue.Enqueue(nc);
            }
        }

        for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
                for (int z = 0; z < res; z++)
                {
                    var c = (x, y, z);
                    if (!grid.Cells.Contains(c) && !outside.Contains(c))
                        grid.Cells.Add(c); // 内部は色情報なし(平均が無ければfallback色)
                }
    }

    // ── GLB読み込み: 三角形(位置 + 代表色) ──
    private static List<Triangle> LoadTriangles(string glbPath)
    {
        var tris = new List<Triangle>();
        var readSettings = new SharpGLTF.Schema2.ReadSettings
        {
            Validation = SharpGLTF.Validation.ValidationMode.Skip
        };
        var model = ModelRoot.Load(glbPath, readSettings);

        foreach (var mesh in model.LogicalMeshes)
            foreach (var prim in mesh.Primitives)
            {
                var posAccessor = prim.GetVertexAccessor("POSITION");
                if (posAccessor == null) continue;
                var positions = posAccessor.AsVector3Array();

                // UV と baseColor テクスチャを取得(無ければ色なし)。
                IList<Vector2>? uvs = null;
                var uvAccessor = prim.GetVertexAccessor("TEXCOORD_0");
                if (uvAccessor != null) uvs = uvAccessor.AsVector2Array();

                SixLabors.ImageSharp.Image<Rgba32>? tex = LoadBaseColorImage(prim);

                foreach (var (ia, ib, ic) in prim.GetTriangleIndices())
                {
                    var tri = new Triangle
                    {
                        A = positions[ia],
                        B = positions[ib],
                        C = positions[ic],
                        Color = null
                    };

                    // 三角形の3頂点UVそれぞれをサンプリングして平均する(1点より安定)。
                    if (tex != null && uvs != null)
                    {
                        var c0 = SampleTexture(tex, uvs[ia].X, uvs[ia].Y);
                        var c1 = SampleTexture(tex, uvs[ib].X, uvs[ib].Y);
                        var c2 = SampleTexture(tex, uvs[ic].X, uvs[ic].Y);
                        tri.Color = (
                            (byte)((c0.r + c1.r + c2.r) / 3),
                            (byte)((c0.g + c1.g + c2.g) / 3),
                            (byte)((c0.b + c1.b + c2.b) / 3));
                    }
                    tris.Add(tri);
                }

                tex?.Dispose();
            }

        return tris;
    }

    // baseColorTexture の画像を ImageSharp で読む。無ければ null。
    private static SixLabors.ImageSharp.Image<Rgba32>? LoadBaseColorImage(MeshPrimitive prim)
    {
        try
        {
            var mat = prim.Material;
            if (mat == null) return null;
            var ch = mat.FindChannel("BaseColor");
            if (ch == null) return null;
            var tex = ch.Value.Texture;
            var img = tex?.PrimaryImage;
            var content = img?.Content;
            if (content == null) return null;
            var bytes = content.Value.Content.ToArray();
            if (bytes.Length == 0) return null;
            return ISImage.Load<Rgba32>(bytes);
        }
        catch
        {
            return null;
        }
    }

    // V を反転するか。trimesh由来GLBは左上原点で反転不要のことが多い。
    // 色がおかしいときはここを true/false 切り替えて試す。
    private const bool FlipV = false;

    // UV(0-1)からピクセル色を取る。範囲外はラップ。
    private static (byte r, byte g, byte b) SampleTexture(
        SixLabors.ImageSharp.Image<Rgba32> tex, float u, float v)
    {
        u = u - (float)Math.Floor(u);
        v = v - (float)Math.Floor(v);
        if (v < 0) v += 1f;
        if (u < 0) u += 1f;
        float vv = FlipV ? (1f - v) : v;
        int px = Math.Clamp((int)(u * (tex.Width - 1)), 0, tex.Width - 1);
        int py = Math.Clamp((int)(vv * (tex.Height - 1)), 0, tex.Height - 1);
        var p = tex[px, py];
        return (p.R, p.G, p.B);
    }

    // 焼き込まれた陰影で暗くなった色を、ガンマ補正で持ち上げる。
    // Gamma < 1.0 で中間〜暗部が明るくなる。0.6 で暗い箱がかなり明るくなる。
    // 明るすぎ(白飛び)なら 0.75〜0.85、まだ暗いなら 0.5 に下げる。
    private const double Gamma = 0.6;
    private static int Brighten(int c)
    {
        double n = c / 255.0;
        double g = Math.Pow(n, Gamma);
        return Math.Clamp((int)(g * 255.0 + 0.5), 0, 255);
    }

    // 彩度ブーストの強さ。TRELLIS素材は彩度がほぼ潰れている(生彩度4等)ため、
    // 線形倍率だと色が立つ前に破綻する。彩度が小さいほど強く持ち上げる非線形にする。
    // SatGamma<1.0 で弱い彩度を強調(小さいほど強い)。SatScale は全体倍率。
    // 緑が出ないなら SatGamma を 0.4 へ、極彩色に荒れるなら 0.7 へ。
    private const double SatGamma = 0.7;
    private const double SatScale = 2.5;

    // 輝度との色差を非線形(べき乗)で拡大して足し戻す彩度ブースト。
    // 各チャンネルの「輝度からのズレ」を符号を保ったまま増幅する。
    private static (int r, int g, int b) Saturate(int r, int g, int b)
    {
        // Rec.709 輝度。グレー成分。
        double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        int Boost(int c)
        {
            double diff = c - lum;                 // 輝度からのズレ(色味)
            double sign = Math.Sign(diff);
            double mag = Math.Abs(diff) / 255.0;   // 0-1 正規化
            // 弱い色味を強く持ち上げる: mag^SatGamma を倍率に使う。
            double boosted = Math.Pow(mag, SatGamma) * SatScale * 255.0;
            double v = lum + sign * boosted;
            return Math.Clamp((int)(v + 0.5), 0, 255);
        }
        return (Boost(r), Boost(g), Boost(b));
    }
}