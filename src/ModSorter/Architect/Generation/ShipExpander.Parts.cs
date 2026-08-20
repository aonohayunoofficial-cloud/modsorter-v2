using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// ShipExpander の部品ヘルパー群（partial）。
// 上部構造物・ドア・窓・マスト・砲塔・手すりの配置と、船首/船尾の座標ヘルパー、小ヘルパー。
public static partial class ShipExpander
{
    // ===== 上部構造物の箱を置く汎用ヘルパー =====
    // (x0..x1, z0..z1) の範囲に、甲板の上(yFrom)から高さ ht の中空の箱（壁＋天井）を作る。
    // 各 z 断面ごとに船体の左右の縁を調べ、その内側にある柱だけに積む（はみ出さず、痩せもしない）。
    private static void AddSuperstructure(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int yFrom, int ht, int w, int d, string block)
    {
        x0 = Clamp(x0, 0, w - 1); x1 = Clamp(x1, 0, w - 1);
        z0 = Clamp(z0, 0, d - 1); z1 = Clamp(z1, 0, d - 1);
        if (x1 < x0 || z1 < z0 || ht <= 0) return;

        int yTop = yFrom + ht - 1;
        for (int z = z0; z <= z1; z++)
        {
            // この z 断面で船体/甲板セルがある x の左端・右端を求める。
            int hullLo = int.MaxValue, hullHi = int.MinValue;
            for (int hx = 0; hx < w; hx++)
                if (cells.ContainsKey((hx, yFrom - 1, z)))
                {
                    if (hx < hullLo) hullLo = hx;
                    if (hx > hullHi) hullHi = hx;
                }
            if (hullHi < hullLo) continue; // この断面に船体が無ければ積まない。

            // 指定範囲と船体幅の共通部分に上部構造を積む。
            int ax0 = Math.Max(x0, hullLo);
            int ax1 = Math.Min(x1, hullHi);
            if (ax1 < ax0) continue;

            for (int y = yFrom; y <= yTop; y++)
                for (int x = ax0; x <= ax1; x++)
                {
                    bool shell = x == ax0 || x == ax1 || z == z0 || z == z1 || y == yTop;
                    if (shell) cells[(x, y, z)] = block;
                }
        }
    }

    // 上部構造物の指定面の中央にドア（縦2マス）を開ける。
    // face: "north"(z0側) "south"(z1側) "east"(x1側) "west"(x0側)
    private static void AddDoor(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int yFrom, string face)
    {
        int cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        int dx, dz;
        switch (face)
        {
            case "north": dx = cx; dz = z0; break;
            case "south": dx = cx; dz = z1; break;
            case "west": dx = x0; dz = cz; break;
            default: dx = x1; dz = cz; break; // east
        }
        cells.Remove((dx, yFrom, dz));
        cells.Remove((dx, yFrom + 1, dz));
    }

    // 上部構造物の側面（左右舷=x0/x1面）に窓を並べる。glass で埋める（穴ではなくガラス）。
    private static void AddWindows(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int yFrom, int ht, string glass)
    {
        int wy = yFrom + Math.Max(0, ht / 2); // 窓の高さ（中ほど）
        for (int z = z0 + 1; z <= z1 - 1; z += 2)
        {
            if (cells.ContainsKey((x0, wy, z))) cells[(x0, wy, z)] = glass;
            if (cells.ContainsKey((x1, wy, z))) cells[(x1, wy, z)] = glass;
        }
    }

    // マストを1本立てる（甲板の上へ垂直の柱）。
    private static void AddMast(
        Dictionary<(int x, int y, int z), string> cells,
        int x, int z, int yFrom, int height, int w, int d, string block)
    {
        if (x < 0 || x >= w || z < 0 || z >= d) return;
        for (int y = yFrom; y < yFrom + height; y++)
            cells[(x, y, z)] = block;
    }

    // 主砲塔を1基置く（低い台の箱＋指定方向へ水平に伸びる砲身2本）。
    // (cx,cz) が砲塔の中心。yFrom は台の下端。baseFillTo は台座で埋める下限の段
    //（背負い式で浮かせたい時に甲板の段を渡す。埋め不要なら yFrom を渡す）。
    private static void AddTurret(
        Dictionary<(int x, int y, int z), string> cells,
        int cx, int cz, int yFrom, int baseFillTo, int w, int d, bool bowNorth, bool barrelToBow, string block)
    {
        int th = 2; // 台の高さ
        // 台座：baseFillTo から台の上端まで 3x3 で埋める（浮き防止）。
        int fillFrom = Math.Min(baseFillTo, yFrom);
        for (int y = fillFrom; y < yFrom + th; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int z = cz - 1; z <= cz + 1; z++)
                    if (x >= 0 && x < w && z >= 0 && z < d)
                        cells[(x, y, z)] = block;

        int barrelY = yFrom + th - 1;
        int bowDir = bowNorth ? -1 : 1;
        int dir = barrelToBow ? bowDir : -bowDir;
        int barrelLen = 3;
        foreach (int bx in new[] { cx - 1, cx + 1 })
            for (int k = 1; k <= barrelLen; k++)
            {
                int bz = cz + dir * (1 + k);
                if (bx >= 0 && bx < w && bz >= 0 && bz < d)
                    cells[(bx, barrelY, bz)] = block;
            }
    }

    // 甲板の両縁に高さ1の手すり（開放甲板の縁取り）。
    private static void AddRail(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int deckY, int zFrom, int zTo, string block)
    {
        for (int z = Clamp(zFrom, 0, d - 1); z <= Clamp(zTo, 0, d - 1); z++)
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || x == w - 1;
                if (edge && cells.ContainsKey((x, deckY, z)))
                    cells[(x, deckY + 1, z)] = block;
            }
    }

    // 船首・船尾の位置を返すヘルパー（z座標）。
    private static int BowZ(int d, bool bowNorth) => bowNorth ? 0 : d - 1;
    private static int SternZ(int d, bool bowNorth) => bowNorth ? d - 1 : 0;

    // 船尾寄り・船首寄りの帯の z 範囲を返す（frac は船尾/船首からの割合 0..1）。
    private static (int lo, int hi) SternBand(int d, bool bowNorth, double frac)
    {
        int len = Math.Max(1, (int)Math.Round(d * frac));
        return bowNorth ? (d - len, d - 1) : (0, len - 1);
    }
    private static (int lo, int hi) BowBand(int d, bool bowNorth, double frac)
    {
        int len = Math.Max(1, (int)Math.Round(d * frac));
        return bowNorth ? (0, len - 1) : (d - len, d - 1);
    }
    // 船首/船尾から frac の割合だけ入った位置の z を返す（砲塔などの中心用）。
    private static int BowSpotZ(int d, bool bowNorth, double frac)
    {
        int off = Math.Max(1, (int)Math.Round(d * frac));
        return bowNorth ? off : (d - 1 - off);
    }
    private static int SternSpotZ(int d, bool bowNorth, double frac)
    {
        int off = Math.Max(1, (int)Math.Round(d * frac));
        return bowNorth ? (d - 1 - off) : off;
    }

    // ================= 小ヘルパー（StructureExpander と独立に持つ） =================

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

    // lo..hi を step 間隔で並べた位置リスト（両端を含む）。マスト等の等間隔配置用。
    private static List<int> AxisPositions(int lo, int hi, int step)
    {
        var list = new List<int>();
        if (hi <= lo) { list.Add(Clamp(lo, 0, int.MaxValue)); return list; }
        int span = hi - lo;
        int segments = Math.Max(1, span / Math.Max(1, step));
        for (int i = 0; i <= segments; i++)
        {
            int v = lo + (int)Math.Round((double)span * i / segments);
            if (list.Count == 0 || list.Last() != v) list.Add(v);
        }
        return list;
    }
}
