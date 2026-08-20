using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ShipExpander の護衛艦系ビルダー（partial）。destroyer / frigate。
// どちらも「船首主砲＋細いピラミッド状ブリッジ＋頂上マスト」の構成を共有する。
// 主力艦・特殊艦（cruiser / battleship / carrier / submarine）は ShipExpander.Naval.Capital.cs。
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
}
