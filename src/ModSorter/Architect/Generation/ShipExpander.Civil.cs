using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ShipExpander の民間船ビルダー（partial）。
// motorboat / trawler / caravel / galleon / liner / cargo。
public static partial class ShipExpander
{
    // motorboat: 開放甲板＋中央の低い操縦席(コンソール)＋前面に風防＋縁の手すり。
    private static void BuildMotorboat(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        int cx = w / 2;

        // 操縦席(コンソール)は船体の中央。幅いっぱいではなく中央の小さな塊。
        int clen = Math.Max(1, d / 6);          // 前後に短い
        int cCenter = d / 2;                     // 船の中央
        int clo = Clamp(cCenter - clen / 2, 1, d - 2);
        int chi = Clamp(clo + clen - 1, 1, d - 2);
        int cWidthHalf = Math.Max(0, (w - 2) / 4); // 船幅の半分程度
        int cx0 = Clamp(cx - cWidthHalf, 1, w - 2);
        int cx1 = Clamp(cx + cWidthHalf, 1, w - 2);

        int ht = Math.Max(1, Math.Min(2, h - deckY - 1)); // 低い操縦席
        AddSuperstructure(cells, cx0, cx1, clo, chi, deckY + 1, ht, w, d, sup);

        // 前面(船首側)に風防＝窓。乗り込み用にドアも船首向きの面へ。
        string bowFace = bowNorth ? "north" : "south";
        AddWindows(cells, cx0, cx1, clo, chi, deckY + 1, ht, glass);
        AddDoor(cells, cx0, cx1, clo, chi, deckY + 1, bowFace);

        // 縁の手すり（開放甲板を囲う）。
        AddRail(cells, w, d, deckY, 0, d - 1, sup);
    }

    // trawler: 船首寄りに背の高い操舵室＋マスト、船尾は開放作業甲板。
    private static void BuildTrawler(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        var (blo, bhi) = BowBand(d, bowNorth, 0.35);
        int x0 = 1, x1 = w - 2;
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, x0, x1, blo, bhi, deckY + 1, ht, w, d, sup);
        // 操舵室の船尾向き面にドア、左右に窓、船首向きに窓。
        string sternFace = bowNorth ? "south" : "north";
        AddDoor(cells, x0, x1, blo, bhi, deckY + 1, sternFace);
        AddWindows(cells, x0, x1, blo, bhi, deckY + 1, ht, glass);
        // マスト（操舵室の少し後ろに1本）。
        int mz = bowNorth ? bhi + 1 : blo - 1;
        AddMast(cells, w / 2, Clamp(mz, 0, d - 1), deckY + 1, Math.Max(2, h - deckY), w, d, hull);
        // 船尾側は開放作業甲板＝手すり。
        var (slo, shi) = SternBand(d, bowNorth, 0.6);
        AddRail(cells, w, d, deckY, slo, shi, hull);
    }

    // caravel: 船尾楼＋2〜3本マスト。
    private static void BuildCaravel(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        var (lo, hi) = SternBand(d, bowNorth, 0.3);
        int x0 = 1, x1 = w - 2;
        int ht = Math.Max(3, (h - deckY) * 2 / 3);
        AddSuperstructure(cells, x0, x1, lo, hi, deckY + 1, ht, w, d, sup);
        string bowFace = bowNorth ? "north" : "south";
        AddDoor(cells, x0, x1, lo, hi, deckY + 1, bowFace);
        AddWindows(cells, x0, x1, lo, hi, deckY + 1, ht, glass);
        // マスト2〜3本を船の長さに沿って等間隔。
        int mastH = Math.Max(3, h - deckY + 2);
        foreach (int mz in AxisPositions(2, d - 3, Math.Max(3, d / 3)))
            AddMast(cells, w / 2, mz, deckY + 1, mastH, w, d, hull);
        AddRail(cells, w, d, deckY, 0, d - 1, hull);
    }

    // galleon: 高い船尾楼＋やや低く細い船首楼＋3〜4本マスト＋砲門（舷側の窓列）。
    private static void BuildGalleon(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 船首は細いので、船首楼は左右を1マスずつ絞る（船体からはみ出さない）。
        int bx0 = Math.Min(w / 2, x0 + 1);
        int bx1 = Math.Max(w / 2, x1 - 1);
        int ht = Math.Max(2, (h - deckY) * 3 / 4);

        // 船尾楼（高く・全幅）。
        var (slo, shi) = SternBand(d, bowNorth, 0.28);
        AddSuperstructure(cells, x0, x1, slo, shi, deckY + 1, ht, w, d, sup);
        AddWindows(cells, x0, x1, slo, shi, deckY + 1, ht, glass);

        // 船首楼（低く・細く・船首寄り）。
        var (blo, bhi) = BowBand(d, bowNorth, 0.16);
        int bowHt = Math.Max(1, ht - 2);
        AddSuperstructure(cells, bx0, bx1, blo, bhi, deckY + 1, bowHt, w, d, sup);

        // 砲門＝船体舷側に窓列。
        int gy = Math.Max(1, deckY - 1);
        for (int z = 3; z < d - 3; z += 2)
        {
            if (cells.ContainsKey((0, gy, z))) cells[(0, gy, z)] = glass;
            if (cells.ContainsKey((w - 1, gy, z))) cells[(w - 1, gy, z)] = glass;
        }

        // マスト3〜4本。
        int mastH = Math.Max(4, h - deckY + 3);
        foreach (int mz in AxisPositions(3, d - 4, Math.Max(3, d / 4)))
            AddMast(cells, w / 2, mz, deckY + 1, mastH, w, d, hull);
        AddRail(cells, w, d, deckY, 0, d - 1, hull);
    }

    // liner: 多層の上部構造物＋煙突＋舷側の窓列。
    private static void BuildLiner(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 上部構造物を船の中央 6割に、多層（段々に短く）で積む。
        int avail = h - deckY - 1;
        int layers = Clamp(avail, 1, 4);
        for (int i = 0; i < layers; i++)
        {
            double frac = 0.6 - i * 0.1;
            int len = Math.Max(1, (int)Math.Round(d * frac));
            int lo = (d - len) / 2, hi = lo + len - 1;
            AddSuperstructure(cells, x0, x1, lo, hi, deckY + 1 + i, 1 + (avail / layers), w, d, sup);
        }
        // 煙突（中央やや後ろに1〜2本）。
        var (slo, shi) = SternBand(d, bowNorth, 0.5);
        int funnelZ = (slo + shi) / 2;
        AddMast(cells, w / 2, funnelZ, deckY + 1, Math.Max(2, avail), w, d, sup);
        // 舷側の窓列（客室の丸窓）。
        int wy = deckY;
        for (int z = 2; z < d - 2; z += 2)
        {
            if (cells.ContainsKey((0, wy, z))) cells[(0, wy, z)] = glass;
            if (cells.ContainsKey((w - 1, wy, z))) cells[(w - 1, wy, z)] = glass;
        }
        // 乗降口ドア（中央舷側）。
        int mid = d / 2;
        cells.Remove((0, deckY, mid));
        cells.Remove((w - 1, deckY, mid));
    }

    // cargo: 船尾寄りに高いブリッジ＋長い平甲板（コンテナ/タンク見立て）。
    private static void BuildCargo(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 船尾ブリッジ（高い塊）。
        var (slo, shi) = SternBand(d, bowNorth, 0.2);
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, x0, x1, slo, shi, deckY + 1, ht, w, d, sup);
        string bowFace = bowNorth ? "north" : "south";
        AddDoor(cells, x0, x1, slo, shi, deckY + 1, bowFace);
        AddWindows(cells, x0, x1, slo, shi, deckY + 1, ht, glass);
        // 前方は長い平甲板のまま（積み荷はユーザーが後から載せる想定）。
    }
}
