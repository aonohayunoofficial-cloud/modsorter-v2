using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ShipExpander の主力艦・特殊艦ビルダー（partial）。
// cruiser / battleship / carrier / submarine。護衛艦系は ShipExpander.Naval.cs。
public static partial class ShipExpander
{
    // battleship: 幅広・重厚。多段の艦橋＋主砲塔（前2基・後1基、背負い式）。
    private static void BuildBattleship(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;

        // 中央の多段艦橋（細めで縦に高い塔。左右は絞るが前後はあまり絞らない）。
        int clen = Math.Max(3, d / 6);
        int clo = (d - clen) / 2, chi = clo + clen - 1;
        int totalHt = Math.Max(4, h - deckY - 1);
        int stages = Math.Min(4, Math.Max(3, totalHt / 2)); // 3〜4段で縦長に
        int stageHt = Math.Max(1, totalHt / stages);
        int bx0 = x0 + 1, bx1 = x1 - 1;            // 最初から左右を1マス狭めて細く
        int bzlo = clo, bzhi = chi, by = deckY + 1;
        for (int s = 0; s < stages; s++)
        {
            AddSuperstructure(cells, bx0, bx1, bzlo, bzhi, by, stageHt, w, d, sup);
            if (s == 0) AddWindows(cells, bx0, bx1, bzlo, bzhi, by, stageHt, glass);
            by += stageHt;
            // 左右は毎段1マス絞る。前後は2段に1回だけ絞る（縦長を保つ）。
            bx0 = Math.Min(w / 2, bx0 + 1); bx1 = Math.Max(w / 2, bx1 - 1);
            if (s % 2 == 1)
            {
                bzlo = Math.Min((bzlo + bzhi) / 2, bzlo + 1);
                bzhi = Math.Max((bzlo + bzhi) / 2, bzhi - 1);
            }
            if (bx1 < bx0 || bzhi < bzlo) break;
        }
        string sternFace = bowNorth ? "south" : "north";
        AddDoor(cells, x0, x1, clo, chi, deckY + 1, sternFace);

        // 主砲塔。船首側に2基（後ろの1基を1段高く＝背負い式）＋船尾側に1基（後ろ向き）。
        int cx = w / 2;
        int b1 = BowSpotZ(d, bowNorth, 0.12);        // 最前部
        int sternDir = bowNorth ? 1 : -1;            // 船尾へ向かう向き
        int b2 = b1 + sternDir * 6;                  // 台(3)＋空き(3)で中心間6マス離す
        int s1 = SternSpotZ(d, bowNorth, 0.14);      // 船尾
        AddTurret(cells, cx, b1, deckY + 1, deckY + 1, w, d, bowNorth, true, sup);
        AddTurret(cells, cx, b2, deckY + 2, deckY + 1, w, d, bowNorth, true, sup);
        AddTurret(cells, cx, s1, deckY + 1, deckY + 1, w, d, bowNorth, false, sup);
    }

    // cruiser: 駆逐艦と戦艦の中間。2段の艦橋（前寄り）＋主砲塔2基（前1・後1）。
    private static void BuildCruiser(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;

        // 艦橋は中央よりやや船首寄りの2段。
        int clen = Math.Max(3, d / 5);
        int cCenter = BowSpotZ(d, bowNorth, 0.42);
        int clo = cCenter - clen / 2, chi = clo + clen - 1;
        clo = Clamp(clo, 1, d - 2); chi = Clamp(chi, 1, d - 2);
        int totalHt = Math.Max(2, h - deckY - 1);
        int lower = Math.Max(1, totalHt * 2 / 3);
        AddSuperstructure(cells, x0, x1, clo, chi, deckY + 1, lower, w, d, sup);
        AddWindows(cells, x0, x1, clo, chi, deckY + 1, lower, glass);
        // 上段は一回り細く。
        int ux0 = Math.Min(w / 2, x0 + 1), ux1 = Math.Max(w / 2, x1 - 1);
        AddSuperstructure(cells, ux0, ux1, clo + 1, chi - 1, deckY + 1 + lower,
                          Math.Max(1, totalHt - lower), w, d, sup);
        string sternFace = bowNorth ? "south" : "north";
        AddDoor(cells, x0, x1, clo, chi, deckY + 1, sternFace);

        // 主砲塔（前1・後1）。前は船首向き、後は船尾向き。
        int cx = w / 2;
        AddTurret(cells, cx, BowSpotZ(d, bowNorth, 0.14), deckY + 1, deckY + 1, w, d, bowNorth, true, sup);
        AddTurret(cells, cx, SternSpotZ(d, bowNorth, 0.14), deckY + 1, deckY + 1, w, d, bowNorth, false, sup);
    }

    // carrier: 全通平甲板＋右舷アイランド（島型構造物）。
    private static void BuildCarrier(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string deck, bool bowNorth)
    {
        // 全通平甲板を全面に張り直す（既に BuildHull で甲板はある）。
        for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
                if (cells.ContainsKey((x, deckY, z)))
                    cells[(x, deckY, z)] = deck;
        // 右舷（x=w-1側）に寄せた小さな島型構造物（中央やや後ろ）。
        int ix0 = w - 2, ix1 = w - 1;
        var (slo, shi) = SternBand(d, bowNorth, 0.45);
        int ilen = Math.Max(2, (shi - slo) / 2);
        int lo = (slo + shi) / 2 - ilen / 2, hi = lo + ilen;
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, ix0, ix1, lo, hi, deckY + 1, ht, w, d, sup);
        AddWindows(cells, ix0, ix1, lo, hi, deckY + 1, ht, glass);
        AddDoor(cells, ix0, ix1, lo, hi, deckY + 1, "west"); // 甲板側へ出るドア
    }

    // submarine: 葉巻型（上面を丸めた船体）＋司令塔(sail)1基。
    private static void BuildSubmarine(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        // 上面を丸める: 最上段の左右端を1マス削って蒲鉾型にする。
        for (int z = 1; z < d - 1; z++)
        {
            if (cells.ContainsKey((0, deckY, z))) cells.Remove((0, deckY, z));
            if (cells.ContainsKey((w - 1, deckY, z))) cells.Remove((w - 1, deckY, z));
        }
        // 司令塔（中央に小さな塔＋ハッチ）。
        int clo = d / 2 - 1, chi = d / 2 + 1;
        int cx0 = Math.Max(0, w / 2 - 1), cx1 = Math.Min(w - 1, w / 2 + 1);
        int ht = Clamp(h - deckY, 2, Math.Max(2, h - deckY));
        AddSuperstructure(cells, cx0, cx1, clo, chi, deckY + 1, ht, w, d, sup);
        // ハッチ（司令塔上面中央を1マス開ける）。
        cells.Remove((w / 2, deckY + ht, d / 2));
    }
}
