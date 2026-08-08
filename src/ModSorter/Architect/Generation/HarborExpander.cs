using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 港湾の単体構造物（structure_type="harbor:<種類>"）の座標生成。
// ship / venue / civic と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・開口部・
// 入口保証・フットプリントマスクは一切通らない。既存の中分類には影響しない。
//
//   quay       … 岸壁（重力式ケーソン式係船岸）。計画水深 10m 級、天端高は朔望平均満潮位
//                +0.5〜1.5m。エプロン幅は水深別に 10〜20m で、コンテナ荷役では 30m 級。
//                係船柱（曲柱）の最大間隔は船型別に 10〜45m、岸壁端部から 2.0m の位置。
//                ガントリークレーンの軌間は 30.48m（100ft）が標準。
//   pier       … 桟橋（直杭式横桟橋）。鋼管杭は径 0.6〜1.0m、杭間隔 4〜6m、上部工厚は
//                1.5〜2m。陸側とは幅 8m 前後の渡橋でつなぐ。背後の護岸は別中分類に委ねる。
//   breakwater … 防波堤（混成堤）。基礎マウンド（捨石）の斜面は 1:2、堤体（ケーソン）幅は
//                10m 前後、天端は上部コンクリートで押さえ、海側に消波ブロックを被覆する。
//
// 断面は「海側が z=0、陸側が z の増加方向」で組み、最後に Rotate で海の向きを回す。
// 1マス=1m。捨石マウンドや消波ブロック・防舷材は z<0 へ張り出すが、Normalize で 0 起点へ寄る。
// 水位は y=depth の面（y=0..depth-1 が水面下、y=depth 以上が干出）。
//
// StructureSpec との対応。
//   width          … 延長（x 方向）。depth/height は使わない（断面は harbor_* から組む）
//   harbor_depth   … 計画水深、harbor_crown … 天端高（水面上）
//   harbor_body    … 岸壁・防波堤の堤体幅／桟橋の幅、harbor_apron … エプロン幅
//   harbor_mound   … 基礎マウンド（捨石）の高さ、harbor_armor … 消波ブロックの幅
//   harbor_pile_step … 杭間隔、harbor_slab … 上部工厚、harbor_approach … 渡橋の長さ
//   harbor_gauge   … クレーンレールの軌間（0 でレールなし）
//   harbor_bollard_step … 係船柱の間隔（0 で係船柱なし）、harbor_fender … 防舷材
//   parapet_height … 防波堤の上部工パラペット、facade_face … 海側の向き（既定 south）
//   wall_block=本体コンクリート / floor_block=舗装・上部工天端 / base_block=捨石・中詰
//   accent_block=縁石・レール・杭 / seat_block=係船柱・防舷材 / roof_block=消波ブロック
//   parapet_block=パラペット
public static class HarborExpander
{
    public const string Prefix = "harbor:";

    // StructureExpander から呼ぶ判定。"harbor:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "pier":
            case "jetty": return "pier";
            case "breakwater":
            case "mole": return "breakwater";
            default: return "quay";
        }
    }

    private sealed class Palette
    {
        public readonly string Body, Pave, Rubble, Trim, Fitting, Armor, Parapet;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Body = Pick(spec.WallBlock, allowed, fallback);
            Pave = Pick(spec.FloorBlock, allowed, Body);
            Rubble = Pick(spec.BaseBlock, allowed, Body);
            Trim = Pick(spec.AccentBlock, allowed, Body);
            Fitting = Pick(spec.SeatBlock, allowed, Trim);
            Armor = Pick(spec.RoofBlock, allowed, Rubble);
            Parapet = Pick(spec.ParapetBlock, allowed, Body);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        switch (KindOf(spec.StructureType))
        {
            case "pier": BuildPier(cells, spec, p); break;
            case "breakwater": BuildBreakwater(cells, spec, p); break;
            default: BuildQuay(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== 岸壁（重力式ケーソン式係船岸）=====
    private static void BuildQuay(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 12, 64);                  // 延長
        int depth = Clamp(spec.HarborDepth ?? 10, 3, 24);     // 計画水深
        int crown = Clamp(spec.HarborCrown ?? 2, 1, 6);       // 天端高（水面上）
        int body = Clamp(spec.HarborBody ?? 10, 4, 24);       // ケーソン幅
        int apron = Clamp(spec.HarborApron ?? 20, 4, 40);     // エプロン幅
        int gauge = Math.Max(0, spec.HarborGauge ?? 0);       // クレーンレール軌間
        int bstep = Math.Max(0, spec.HarborBollardStep ?? 0); // 係船柱の間隔

        int h = depth + crown;              // 全高
        int top = h - 1;                    // 天端の y
        int water = depth;                  // 水面の y（この段から上が干出）
        int mound = Clamp(spec.HarborMound ?? 2, 0, Math.Max(0, h - 3));

        // 基礎マウンド（捨石）。ケーソンの下に敷き、海側へ肩を出して 1:2 で下る。
        for (int k = 0; k < mound; k++)
        {
            int reach = 2 * (mound - 1 - k);
            Fill(cells, 0, len - 1, k, k, -reach, body - 1, p.Rubble);
        }

        // ケーソン本体（外殻コンクリート＋中詰）と上部コンクリート。
        Box(cells, 0, len - 1, mound, top - 1, 0, body - 1, p.Body, p.Rubble);
        Fill(cells, 0, len - 1, top, top, 0, body - 1, p.Body);

        // 裏込め石とエプロンの路盤。背面から離れるほど薄くなる（見えない深部は詰めない）。
        int backTop = top - 1;
        for (int i = 0; i < apron; i++)
        {
            int z = body + i;
            int bottom = Math.Min(backTop - 2, mound + 2 * i / 3);
            if (bottom < 0) bottom = 0;
            Fill(cells, 0, len - 1, bottom, backTop, z, z, p.Rubble);
        }

        // エプロン舗装と前面の縁石。
        Fill(cells, 0, len - 1, top, top, body, body + apron - 1, p.Pave);
        for (int x = 0; x < len; x++) cells[(x, top, 0)] = p.Trim;

        // 係船柱（曲柱）。岸壁端部から 2.0m の位置に、指定間隔で並べる。
        if (bstep >= 5)
        {
            int bz = Math.Min(2, body - 1);
            for (int x = bstep / 2; x < len; x += bstep) cells[(x, top + 1, bz)] = p.Fitting;
        }

        // 防舷材。前面に 1マス張り出し、水面付近をまたぐ高さに付く。
        if (spec.HarborFender)
        {
            int y0 = Math.Max(mound, water - 1);
            int y1 = Math.Min(top - 1, water + 1);
            for (int x = 2; x < len - 2; x += 5)
                for (int y = y0; y <= y1; y++) cells[(x, y, -1)] = p.Fitting;
        }

        // ガントリークレーンの軌道。海側レールは前面から 4m、陸側はそこから軌間ぶん。
        int rail0 = 4;
        int rail1 = rail0 + gauge;
        if (gauge >= 6 && rail1 <= body + apron - 1)
            for (int x = 0; x < len; x++)
            {
                cells[(x, top, rail0)] = p.Trim;
                cells[(x, top, rail1)] = p.Trim;
            }
    }

    // ===== 桟橋（直杭式横桟橋）=====
    private static void BuildPier(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 12, 64);                 // 延長
        int wide = Clamp(spec.HarborBody ?? 15, 6, 40);       // 桟橋の幅
        int depth = Clamp(spec.HarborDepth ?? 8, 3, 24);
        int crown = Clamp(spec.HarborCrown ?? 2, 1, 6);
        int step = Clamp(spec.HarborPileStep ?? 5, 3, 10);    // 杭間隔
        int appr = Math.Max(0, spec.HarborApproach ?? 0);     // 渡橋の長さ
        int bstep = Math.Max(0, spec.HarborBollardStep ?? 0);

        int h = depth + crown;
        int top = h - 1;
        int water = depth;
        int slab = Clamp(spec.HarborSlab ?? 2, 1, Math.Max(1, top));
        int slabBottom = top - slab + 1;

        // 鋼管杭。等間隔の格子で海底から上部工の下端まで通す。
        var xs = Grid(len, step);
        var zs = Grid(wide, step);
        foreach (int x in xs)
            foreach (int z in zs)
                Fill(cells, x, x, 0, slabBottom - 1, z, z, p.Trim);

        // 上部工（受梁・床版）と舗装、両縁の縁石。
        Fill(cells, 0, len - 1, slabBottom, top - 1, 0, wide - 1, p.Body);
        Fill(cells, 0, len - 1, top, top, 0, wide - 1, p.Pave);
        for (int x = 0; x < len; x++)
        {
            cells[(x, top, 0)] = p.Trim;
            cells[(x, top, wide - 1)] = p.Trim;
        }

        // 係船柱と防舷材。海側の縁に付く。
        if (bstep >= 5)
        {
            int bz = Math.Min(2, wide - 1);
            for (int x = bstep / 2; x < len; x += bstep) cells[(x, top + 1, bz)] = p.Fitting;
        }
        if (spec.HarborFender)
        {
            int y0 = Math.Max(0, water - 1);
            for (int x = 2; x < len - 2; x += 5)
                for (int y = y0; y <= top - 1; y++) cells[(x, y, -1)] = p.Fitting;
        }

        // 渡橋。陸側へ幅 8m 前後で延ばし、両側の杭で支える。
        if (appr >= 4)
        {
            int bw = Math.Min(8, wide);
            int bx0 = (len - bw) / 2;
            int bx1 = bx0 + bw - 1;
            int bz1 = wide + appr - 1;

            Fill(cells, bx0, bx1, slabBottom, top - 1, wide, bz1, p.Body);
            Fill(cells, bx0, bx1, top, top, wide, bz1, p.Pave);
            for (int z = wide; z <= bz1; z++)
            {
                cells[(bx0, top, z)] = p.Trim;
                cells[(bx1, top, z)] = p.Trim;
            }
            for (int z = wide; z <= bz1; z += step)
            {
                Fill(cells, bx0, bx0, 0, slabBottom - 1, z, z, p.Trim);
                Fill(cells, bx1, bx1, 0, slabBottom - 1, z, z, p.Trim);
            }
        }
    }

    // ===== 防波堤（混成堤）=====
    private static void BuildBreakwater(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 12, 64);                 // 延長
        int depth = Clamp(spec.HarborDepth ?? 10, 3, 24);
        int crown = Clamp(spec.HarborCrown ?? 5, 1, 10);     // 天端高（水面上）
        int body = Clamp(spec.HarborBody ?? 10, 4, 24);      // 堤体（ケーソン）幅
        int armor = Math.Max(0, spec.HarborArmor ?? 0);      // 消波ブロックの幅
        int parapet = Clamp(spec.ParapetHeight ?? 0, 0, 4);

        int h = depth + crown;
        int top = h - 1;
        int mound = Clamp(spec.HarborMound ?? 3, 1, Math.Max(1, h - 3));

        // 基礎マウンド（捨石）。海側・港内側の両方へ 1:2 で広がる。
        for (int k = 0; k < mound; k++)
        {
            int reach = 2 * (mound - 1 - k);
            Fill(cells, 0, len - 1, k, k, -reach, body - 1 + reach, p.Rubble);
        }

        // 堤体（ケーソン）と上部コンクリート。
        Box(cells, 0, len - 1, mound, top - 1, 0, body - 1, p.Body, p.Rubble);
        Fill(cells, 0, len - 1, top, top, 0, body - 1, p.Body);

        // 上部工のパラペット。海側の端を立ち上げて越波を抑える。
        for (int py = 1; py <= parapet; py++)
            for (int x = 0; x < len; x++) cells[(x, top + py, 0)] = p.Parapet;

        // 消波ブロック。堤体の海側前面に寄せ、天端付近から 1:1.33 で下る。
        for (int j = 0; j < armor; j++)
        {
            int t = top - 1 - 3 * j / 4;
            if (t < 1) break;
            Fill(cells, 0, len - 1, 0, t, -1 - j, -1 - j, p.Armor);
        }
    }

    // ===== 共通部品 =====

    private static void Fill(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string block)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++) cells[(x, y, z)] = block;
    }

    // 外殻と中詰を分けた箱。ケーソンの側壁と中詰砂に使う。
    private static void Box(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string shell, string core)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || y == y0 || y == y1 || z == z0 || z == z1;
                    cells[(x, y, z)] = edge ? shell : core;
                }
    }

    // 0..span-1 の範囲に、両端を含めて step 以下の間隔で並ぶ位置を返す（杭の割付）。
    private static List<int> Grid(int span, int step)
    {
        var list = new List<int>();
        if (span <= 1) { list.Add(0); return list; }
        int n = Math.Max(2, (span - 1) / step + 1);
        for (int i = 0; i < n; i++)
            list.Add((int)Math.Round((double)(span - 1) * i / (n - 1)));
        return list.Distinct().ToList();
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 海側を z の小さい側（north）として組んだものを、指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string sea)
    {
        if (sea == "north" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = sea switch
            {
                "south" => (w - 1 - x, d - 1 - z),
                "east" => (d - 1 - z, x),
                "west" => (z, w - 1 - x),
                _ => (x, z)
            };
            dst[(nx, kv.Key.y, nz)] = kv.Value;
        }
        return dst;
    }

    private static List<GeneratedBlock> Normalize(Dictionary<(int x, int y, int z), string> cells)
    {
        if (cells.Count == 0) return new List<GeneratedBlock>();

        int minX = cells.Keys.Min(k => k.x);
        int minY = cells.Keys.Min(k => k.y);
        int minZ = cells.Keys.Min(k => k.z);

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x - minX,
                Y = kv.Key.y - minY,
                Z = kv.Key.z - minZ,
                Id = kv.Value
            })
            .ToList();
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
