using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ShipExpander の軍艦ビルダー（partial）。
// destroyer / frigate / cruiser / battleship / carrier / submarine。
public static partial class ShipExpander
{
    // destroyer: 細身の駆逐艦。船首主砲＋細いピラミッド状ブリッジ＋頂上マスト＋船尾ヘリ甲板。
    private static void BuildDestroyer(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        int cx = w / 2;

        // (1) 船首主砲。
        AddTurret(cells, cx, BowSpotZ(d, bowNorth, 0.10), deckY + 1, deckY + 1, w, d, bowNorth, true, sup);

        // (2) ブリッジ帯の前後範囲。船首から3割ほど。
        int blen = Math.Max(3, d / 5);
        int bCenter = BowSpotZ(d, bowNorth, 0.30);
        int blo = Clamp(bCenter - blen / 2, 1, d - 2);
        int bhi = Clamp(blo + blen - 1, 1, d - 2);

        const int STAGE_HT = 3;
        int totalHt = Math.Max(STAGE_HT, h - deckY - 1);
        int stages = Math.Max(2, totalHt / STAGE_HT);

        // ブリッジ幅は船幅の半分程度に細くする。
        int halfW = Math.Max(1, (x1 - x0 + 1) / 2);
        int sx0 = cx - halfW / 2;
        int sx1 = sx0 + halfW - 1;
        sx0 = Clamp(sx0, 1, w - 2); sx1 = Clamp(sx1, 1, w - 2);

        // 前後: 前面を固定し、上段ほど船尾側を下げる（前高後低）。
        int bowEdge = bowNorth ? blo : bhi;
        int sternEdge = bowNorth ? bhi : blo;
        int sternDir = bowNorth ? -1 : 1;

        int curStern = sternEdge, y = deckY + 1;
        for (int s = 0; s < stages; s++)
        {
            int zlo = Math.Min(bowEdge, curStern);
            int zhi = Math.Max(bowEdge, curStern);
            AddSuperstructure(cells, sx0, sx1, zlo, zhi, y, STAGE_HT, w, d, sup);
            if (s == 0)
            {
                AddWindows(cells, sx0, sx1, zlo, zhi, y, STAGE_HT, glass);
                string bowFace = bowNorth ? "north" : "south";
                AddDoor(cells, sx0, sx1, zlo, zhi, y, bowFace);
            }
            y += STAGE_HT;
            if (sx0 < cx) sx0++;
            if (sx1 > cx) sx1--;
            curStern += sternDir;
            if (sx1 < sx0) break;
            if ((bowNorth && curStern < bowEdge) || (!bowNorth && curStern > bowEdge)) break;
        }

        // (3) メインマスト：ブリッジ頂上の中央から細い1本を伸ばす。
        int mastZ = Clamp((bowEdge + sternEdge) / 2, 1, d - 2);
        int mastH = Math.Max(2, (h - deckY) / 2);
        AddMast(cells, cx, mastZ, y, mastH, w, d, sup);

        // (4) 船尾は平らなヘリ甲板として残す。
    }

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

    // frigate: 小型護衛艦。船首主砲＋細いピラミッド状ブリッジ＋頂上マスト＋船尾ハンガー＋ヘリ甲板。
    private static void BuildFrigate(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        int cx = w / 2;

        // (1) 船首の主砲（船首の先端寄り、砲身は前向き）。
        AddTurret(cells, cx, BowSpotZ(d, bowNorth, 0.10), deckY + 1, deckY + 1, w, d, bowNorth, true, sup);

        // (2) ブリッジ帯の前後範囲。船首から3割ほど。
        int blen = Math.Max(3, d / 5);
        int bCenter = BowSpotZ(d, bowNorth, 0.32);
        int blo = Clamp(bCenter - blen / 2, 1, d - 2);
        int bhi = Clamp(blo + blen - 1, 1, d - 2);

        const int STAGE_HT = 3;
        int totalHt = Math.Max(STAGE_HT, h - deckY - 1);
        int stages = Math.Max(2, totalHt / STAGE_HT);

        // ブリッジ幅は船幅の半分程度に細くする（実物: 基部で船幅の約半分）。
        int halfW = Math.Max(1, (x1 - x0 + 1) / 2);
        int sx0 = cx - halfW / 2;
        int sx1 = sx0 + halfW - 1;
        sx0 = Clamp(sx0, 1, w - 2); sx1 = Clamp(sx1, 1, w - 2);

        // 前後: 前面(bowEdge)を固定し、上段ほど船尾側を下げる（前高後低）。
        int bowEdge = bowNorth ? blo : bhi;
        int sternEdge = bowNorth ? bhi : blo;
        int sternDir = bowNorth ? -1 : 1;

        int curStern = sternEdge, y = deckY + 1;
        for (int s = 0; s < stages; s++)
        {
            int zlo = Math.Min(bowEdge, curStern);
            int zhi = Math.Max(bowEdge, curStern);
            AddSuperstructure(cells, sx0, sx1, zlo, zhi, y, STAGE_HT, w, d, sup);
            if (s == 0)
            {
                AddWindows(cells, sx0, sx1, zlo, zhi, y, STAGE_HT, glass);
                string bowFace = bowNorth ? "north" : "south";
                AddDoor(cells, sx0, sx1, zlo, zhi, y, bowFace);
            }
            y += STAGE_HT;
            // 左右を素早く絞る（ピラミッド状）。
            if (sx0 < cx) sx0++;
            if (sx1 > cx) sx1--;
            // 船尾側だけ下げる（前面は固定）。
            curStern += sternDir;
            if (sx1 < sx0) break;
            if ((bowNorth && curStern < bowEdge) || (!bowNorth && curStern > bowEdge)) break;
        }

        // (3) メインマスト：ブリッジ頂上の中央から細い1本を伸ばす。
        int mastZ = Clamp((bowEdge + sternEdge) / 2, 1, d - 2);
        int mastH = Math.Max(2, (h - deckY) / 2);
        AddMast(cells, cx, mastZ, y, mastH, w, d, sup); // y はブリッジ最上段の上

        // (4) 船尾ハンガー：低い箱（ヘリ格納庫）。ブリッジより低くする。
        var (hlo, hhi) = SternBand(d, bowNorth, 0.22);
        int hangarHt = Math.Max(1, totalHt / 2);
        AddSuperstructure(cells, x0, x1, hlo, hhi, deckY + 1, hangarHt, w, d, sup);

        // (5) ハンガーより後ろの最後尾はヘリ甲板として平らに残す（何も置かない）。
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
