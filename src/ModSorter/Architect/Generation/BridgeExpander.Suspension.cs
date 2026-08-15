using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 吊り橋（structure_type="bridge:suspension_bridge"）。
//
// 縦断は アンカレイジ｜側径間｜主塔｜中央径間｜主塔｜側径間｜アンカレイジ。
// 主ケーブルは中央径間を放物線、側径間を主塔頂からアンカレイジ天端へ落とす曲線で描く。
// ケーブル面と主塔は床版の外側（x=-1 と x=W）に置くので、車道の幅には影響しない。
//
// 実寸の根拠。
//   サグ比       … 1/10 前後（安芸灘大橋 サグ74.0m・中央支間750m ＝ 1/10）。
//   主塔高       … 床版上にサグ＋余裕。既定はサグの1.25倍。
//   側径間       … 中央径間の20〜60%。既定40%。
//   ハンガー間隔 … 10〜20m級（明石海峡大橋14m）。既定10。
//   補剛桁高     … 実橋は支間の1/100級だが1マス=1mでは潰れるので最小2マス。
public static partial class BridgeExpander
{
    private static void BuildSuspension(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        var s = MakeSection(spec);
        int W = s.DeckW;

        // ===== 支間割 =====
        int main = Clamp(spec.BridgeSpan ?? 120, 40, 240);
        int sideRatio = Clamp(spec.BridgeSideRatio ?? 40, 20, 60);
        int side = Math.Max(8, main * sideRatio / 100);
        bool anchor = spec.BridgeAnchorage;
        int anchorLen = anchor ? Math.Max(6, side / 5) : 0;

        // ===== 高さ =====
        int clear = Clamp(spec.BridgePierHeight ?? 14, 4, 60);      // 桁下高
        int stiff = Clamp(spec.BridgeStiffenDepth ?? 2, 1, 8);      // 補剛桁高
        int soffit = clear;
        int deckY = soffit + stiff;
        int topY = deckY + s.TopDy;
        int railTop = topY + s.Rail;

        int sagRatio = Clamp(spec.BridgeSagRatio ?? 10, 8, 12);
        int sag = Math.Max(3, main / sagRatio);
        int towerAbove = spec.BridgeTowerHeight ?? 0;
        if (towerAbove <= 0) towerAbove = sag + Math.Max(3, sag / 4);
        towerAbove = Clamp(towerAbove, sag + 2, 160);
        int towerTop = deckY + towerAbove;

        // ===== 縦断の配置 =====
        int zDeck0 = anchorLen;
        int t1 = zDeck0 + side;              // 主塔1
        int t2 = t1 + main;                  // 主塔2
        int zDeck1 = t2 + side - 1;
        int total = zDeck1 + 1 + anchorLen;

        int zA = anchorLen > 0 ? anchorLen / 2 : 0;   // ケーブル定着点
        int yA = deckY;
        int xL = -1, xR = W;                          // ケーブル面・主塔

        // ===== アンカレイジと取付部 =====
        if (anchor)
        {
            Fill(cells, xL - 2, xR + 2, 0, deckY - 1, 0, anchorLen - 1, p.Pier);
            Fill(cells, xL - 2, xR + 2, 0, deckY - 1, total - anchorLen, total - 1, p.Pier);
            BuildDeckSurface(cells, p, s, deckY, 0, anchorLen - 1);
            BuildDeckSurface(cells, p, s, deckY, total - anchorLen, total - 1);
        }

        // ===== 主ケーブル =====
        // 中央径間は放物線。側径間は主塔頂とアンカレイジを結び、わずかに垂らす。
        int CableY(int z)
        {
            if (z >= t1 && z <= t2)
            {
                double u = (2.0 * z - (t1 + t2)) / Math.Max(1, t2 - t1);
                return (int)Math.Round(towerTop - sag * (1 - u * u));
            }
            double t, y;
            if (z < t1)
            {
                t = (t1 - z) / (double)Math.Max(1, t1 - zA);
                if (t > 1) t = 1;
            }
            else
            {
                int zB = total - 1 - zA;
                t = (z - t2) / (double)Math.Max(1, zB - t2);
                if (t > 1) t = 1;
            }
            y = towerTop + (yA - towerTop) * t - sag * t * (1 - t);
            return (int)Math.Round(y);
        }

        int prevY = CableY(zA);
        for (int z = zA; z <= total - 1 - zA; z++)
        {
            int y = CableY(z);
            int lo = Math.Min(prevY, y);
            int hi = Math.Max(prevY, y);
            Fill(cells, xL, xL, lo, hi, z, z, p.Cable);
            Fill(cells, xR, xR, lo, hi, z, z, p.Cable);
            prevY = y;
        }

        // ===== 主塔 =====
        string towerType = (spec.BridgeTowerType ?? "portal").Trim().ToLowerInvariant();
        BuildTower(t1);
        BuildTower(t2);

        void BuildTower(int zt)
        {
            int z0 = zt - 1, z1 = zt;   // 橋軸方向の厚み2
            Fill(cells, xL, xL, 0, towerTop, z0, z1, p.Pier);
            Fill(cells, xR, xR, 0, towerTop, z0, z1, p.Pier);
            Fill(cells, xL, xR, towerTop, towerTop, z0, z1, p.Pier);   // 上横梁

            if (towerType == "h")
            {
                // H型。桁下にも下横梁を入れる。
                if (soffit >= 2) Fill(cells, xL, xR, soffit - 2, soffit - 2, z0, z1, p.Pier);
                return;
            }
            if (towerType == "truss")
            {
                // トラス塔。高さ6ごとに向きを変えた斜材で筋交いにする。
                bool up = true;
                for (int y = railTop + 2; y + 6 <= towerTop; y += 6)
                {
                    for (int i = 0; i <= W + 1; i++)
                    {
                        double f = i / (double)Math.Max(1, W + 1);
                        int yy = y + (int)Math.Round((up ? f : 1 - f) * 6);
                        Fill(cells, xL + i, xL + i, yy, yy, z0, z1, p.Pier);
                    }
                    up = !up;
                }
                return;
            }
            // 門型。路面の上に中横梁を1本。
            Fill(cells, xL, xR, railTop + 2, railTop + 2, z0, z1, p.Pier);
        }

        // ===== 補剛桁と床版 =====
        Fill(cells, 0, 0, soffit, deckY - 1, zDeck0, zDeck1, p.Girder);
        Fill(cells, W - 1, W - 1, soffit, deckY - 1, zDeck0, zDeck1, p.Girder);

        int crossStep = Clamp(spec.BridgeCrossStep ?? 6, 0, 24);
        if (crossStep > 0)
            for (int z = zDeck0; z <= zDeck1; z += crossStep)
                Fill(cells, 0, W - 1, soffit, soffit, z, z, p.Girder);

        BuildDeckSurface(cells, p, s, deckY, zDeck0, zDeck1);

        // ===== ハンガー =====
        int hangStep = Clamp(spec.BridgeHangerStep ?? 10, 2, 30);
        for (int z = zDeck0; z <= zDeck1; z += hangStep)
        {
            if (Math.Abs(z - t1) <= 1 || Math.Abs(z - t2) <= 1) continue;   // 主塔位置は避ける
            int cy = CableY(z);
            if (cy <= railTop + 1) continue;
            Fill(cells, xL, xL, railTop + 1, cy - 1, z, z, p.Hanger);
            Fill(cells, xR, xR, railTop + 1, cy - 1, z, z, p.Hanger);
        }

        // ===== 照明 =====
        BuildLights(cells, p, s, railTop, Clamp(spec.BridgeLightStep ?? 30, 0, 80), 0, total - 1);
    }
}
