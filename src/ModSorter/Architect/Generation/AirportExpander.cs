using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 空港の平面土木施設（structure_type="airport:<種類>"）の座標生成。
// harbor と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・開口部・
// 入口保証・フットプリントマスクは一切通らない。既存の中分類には影響しない。
//
//   runway  … 滑走路。国内の主要空港は幅 45m（進入区分により 30m・60m もある）。
//             舗装の外側にショルダーが付き、幅は誘導路で 9.5m 級。
//             標識は中心線標識（長 30m・間隔 20m の破線）、進入端標識（縦縞 8 本を
//             中心線対称に配置）、接地帯標識（進入端から 150m ごとの対の帯）、
//             着陸目標点標識（進入端から 400m。滑走路長 2500m 級のとき）。
//             縦縞の寸法は幅 30m 以上の滑走路とそれ未満で別に定められている。
//   taxiway … 誘導路。幅は 23m 以上（大型機の就航する路線で 30m 級）。
//             中心線標識は黄色の実線 1 本、両縁に誘導路縁標識の 2 本線が走る。
//             固定障害物との間隔は 39m 以上で、その外側が整地区域になる。
//   apron   … エプロン。スポット（駐機場）単位で区画され、各スポットに
//             リードインライン（誘導線）とストップマークが引かれる。
//             スポット間はブラスト間隔を取り、外周に走行路（タキシレーン）が回る。
//
// 平面なので断面は「進入端側が z=0、逆側が z の増加方向」で組み、
// 最後に Rotate で向きを回す。1マス=1m。舗装は y=0 の 1 層だけで、
// 標識は同じ y=0 の塗り分け（別ブロック）で表現する。灯火だけ y=1 に載る。
//
// StructureSpec との対応。
//   width          … 幅（x 方向）。滑走路 45 / 誘導路 23 / エプロンは区画幅
//   depth          … 長さ（z 方向）。64 マス上限のため実物の一部を切り出す粒度
//   airport_shoulder … ショルダー幅（片側）、airport_marking … 標識の有無
//   airport_center_step … 中心線標識の周期（0 で実線）
//   airport_threshold … 進入端標識の縦縞の本数（0 で無し）
//   airport_touchdown … 接地帯標識の対の数（0 で無し）
//   airport_edge_light … 縁灯の間隔（0 で灯火なし）
//   airport_spots  … エプロンのスポット数、airport_spot_width … スポットの幅
//   facade_face    … 進入端・接続側の向き（既定 south）
//   floor_block=舗装 / accent_block=標識（白・黄）/ base_block=ショルダー
//   seat_block=縁灯 / wall_block=区画線・ストップマーク
public static class AirportExpander
{
    public const string Prefix = "airport:";

    // StructureExpander から呼ぶ判定。"airport:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "taxiway":
            case "taxi": return "taxiway";
            case "apron":
            case "ramp": return "apron";
            default: return "runway";
        }
    }

    private sealed class Palette
    {
        public readonly string Pave, Mark, Shoulder, Light, Line;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Pave = Pick(spec.FloorBlock, allowed, fallback);
            Mark = Pick(spec.AccentBlock, allowed, Pave);
            Shoulder = Pick(spec.BaseBlock, allowed, Pave);
            Light = Pick(spec.SeatBlock, allowed, Mark);
            Line = Pick(spec.WallBlock, allowed, Mark);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        switch (KindOf(spec.StructureType))
        {
            case "taxiway": BuildTaxiway(cells, spec, p); break;
            case "apron": BuildApron(cells, spec, p); break;
            default: BuildRunway(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== 滑走路 =====
    // 幅 45m を既定にし、舗装面の上に中心線・進入端・接地帯・着陸目標点の各標識を置く。
    // 実物の標識は m 単位で決まっているが、延長が 64 マス上限なので周期を保った縮約にする。
    private static void BuildRunway(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 12, 64);                       // 幅（実物 45m 級）
        int len = Clamp(spec.Depth, 16, 64);                     // 延長
        int shoulder = Clamp(spec.AirportShoulder ?? 7, 0, 12);  // ショルダー（片側）
        int cstep = Clamp(spec.AirportCenterStep ?? 5, 0, 12);   // 中心線の周期
        int thr = Clamp(spec.AirportThreshold ?? 8, 0, 16);      // 進入端の縦縞の本数
        int tdz = Clamp(spec.AirportTouchdown ?? 3, 0, 6);       // 接地帯標識の対の数
        int elight = Clamp(spec.AirportEdgeLight ?? 6, 0, 16);   // 縁灯の間隔
        bool marking = spec.AirportMarking;

        // ショルダー。舗装の外側へ左右に張り出す（負座標は Normalize で寄る）。
        if (shoulder > 0)
        {
            Fill(cells, -shoulder, -1, 0, 0, 0, len - 1, p.Shoulder);
            Fill(cells, w, w + shoulder - 1, 0, 0, 0, len - 1, p.Shoulder);
        }

        // 舗装面。
        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        if (marking)
        {
            int cx = (w - 1) / 2;

            // 中心線標識。実物は長 30m・間隔 20m の破線なので、描く方を長く取る。
            if (cstep > 0)
            {
                int on = Math.Max(2, cstep * 3 / 5);
                for (int z = 0; z < len; z++)
                    if (z % cstep < on) cells[(cx, 0, z)] = p.Mark;
            }
            else
            {
                for (int z = 0; z < len; z++) cells[(cx, 0, z)] = p.Mark;
            }

            // 進入端標識。中心線を挟んで対称に並ぶ縦縞。実物は 8 本（幅 45m のとき）。
            if (thr > 0)
            {
                int half = thr / 2;
                int barLen = Math.Max(3, len / 8);
                for (int i = 0; i < half; i++)
                {
                    int off = 2 + i * 2;
                    foreach (int x in new[] { cx - off, cx + off })
                        if (x >= 0 && x < w)
                            Fill(cells, x, x, 0, 0, 2, 2 + barLen - 1, p.Mark);
                }
            }

            // 接地帯標識。進入端から一定間隔で、中心線の左右に対で並ぶ帯。
            if (tdz > 0)
            {
                int start = 2 + Math.Max(3, len / 8) + 3;
                int step = Math.Max(4, (len - start) / Math.Max(1, tdz));
                int barLen = Math.Max(2, step / 3);
                for (int i = 0; i < tdz; i++)
                {
                    int z0 = start + i * step;
                    if (z0 + barLen > len) break;
                    foreach (int x in new[] { cx - 4, cx + 4 })
                        if (x >= 0 && x < w)
                            Fill(cells, x, x, 0, 0, z0, z0 + barLen - 1, p.Mark);
                }
            }
        }

        // 滑走路縁灯。舗装の両縁に沿って一定間隔で灯火を置く。
        if (elight > 0)
        {
            for (int z = elight / 2; z < len; z += elight)
            {
                cells[(0, 1, z)] = p.Light;
                cells[(w - 1, 1, z)] = p.Light;
            }
        }
    }

    // ===== 誘導路 =====
    // 幅 23m 以上が基準。中心線は黄の実線 1 本、両縁に縁標識の線が走る。
    private static void BuildTaxiway(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 8, 48);                        // 幅（実物 23m 以上）
        int len = Clamp(spec.Depth, 8, 64);                      // 延長
        int shoulder = Clamp(spec.AirportShoulder ?? 9, 0, 16);  // ショルダー（実物 9.5m 級）
        int elight = Clamp(spec.AirportEdgeLight ?? 8, 0, 16);   // 縁灯の間隔
        bool marking = spec.AirportMarking;

        if (shoulder > 0)
        {
            Fill(cells, -shoulder, -1, 0, 0, 0, len - 1, p.Shoulder);
            Fill(cells, w, w + shoulder - 1, 0, 0, 0, len - 1, p.Shoulder);
        }

        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        if (marking)
        {
            // 中心線標識。誘導路は実線。
            int cx = (w - 1) / 2;
            Fill(cells, cx, cx, 0, 0, 0, len - 1, p.Mark);

            // 誘導路縁標識。舗装の縁から 1 マス内側に 2 本線で走る。
            foreach (int x in new[] { 1, w - 2 })
                if (x > 0 && x < w - 1)
                    Fill(cells, x, x, 0, 0, 0, len - 1, p.Line);
        }

        if (elight > 0)
            for (int z = elight / 2; z < len; z += elight)
            {
                cells[(0, 1, z)] = p.Light;
                cells[(w - 1, 1, z)] = p.Light;
            }
    }

    // ===== エプロン =====
    // スポット（駐機場）単位で区画し、各スポットにリードインラインとストップマークを引く。
    // 外周には走行路（タキシレーン）が回るので、奥側に 1 本ぶんの帯を空ける。
    private static void BuildApron(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int spots = Clamp(spec.AirportSpots ?? 3, 1, 8);          // スポット数
        int sw = Clamp(spec.AirportSpotWidth ?? 18, 6, 40);       // スポットの幅
        int len = Clamp(spec.Depth, 12, 64);                      // 奥行き（駐機＋走行路）
        int lane = Clamp(spec.AirportShoulder ?? 10, 0, 24);      // 走行路の幅
        bool marking = spec.AirportMarking;

        int w = Math.Min(64, spots * sw);
        int stand = Math.Max(4, len - lane);                      // 駐機区画の奥行き

        // 舗装面。駐機区画と走行路をまとめて 1 面で敷く。
        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        if (!marking) return;

        for (int i = 0; i < spots; i++)
        {
            int x0 = i * sw;
            int x1 = Math.Min(w - 1, x0 + sw - 1);
            if (x0 >= w) break;
            int cx = (x0 + x1) / 2;

            // 区画線。スポットとスポットの境界。手前（駐機区画）だけに引く。
            if (i > 0) Fill(cells, x0, x0, 0, 0, 0, stand - 1, p.Line);

            // リードインライン。走行路から駐機位置へ導く誘導線。
            Fill(cells, cx, cx, 0, 0, 0, stand - 1, p.Mark);

            // ストップマーク。機首の停止位置を示す横棒。
            int sz = Math.Max(1, stand / 4);
            Fill(cells, cx - 2, cx + 2, 0, 0, sz, sz, p.Mark);
        }

        // 走行路の中心線。駐機区画の奥を横切る。
        if (lane > 0)
        {
            int lz = stand + lane / 2;
            if (lz < len) Fill(cells, 0, w - 1, 0, 0, lz, lz, p.Mark);
        }
    }

    // ===== 共通ヘルパー =====

    private static void Fill(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string block)
    {
        if (x1 < x0 || y1 < y0 || z1 < z0) return;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, y, z)] = block;
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 進入端側を z の小さい側（north）として組んだものを、指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string face)
    {
        if (face == "north" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = face switch
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
