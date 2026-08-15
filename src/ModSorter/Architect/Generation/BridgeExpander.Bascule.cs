using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 跳開橋（structure_type="bridge:bascule_bridge"）。固定トラニオン式。
//
// 縦断は 取付部｜固定径間｜主橋脚（トラニオン）｜可動径間｜主橋脚｜固定径間｜取付部。
// 可動桁はトラニオンを中心に跳開角ぶん回した位置へ、1マス階段状に積んで表す。
// 釣合い錘は桁と反対側の腕に付き、桁が上がるほど橋脚内のピットへ降りる。
//
// 実寸の根拠。
//   跳開角 … 勝鬨橋は70秒で70度まで開く設計（土木学会・双葉跳開橋/勝鬨橋の現状と今後）。
//   葉数   … 単葉（片持ち1枚）と双葉（中央で突き合わせる2枚）。
//   桁高   … 支間長の1/20級。可動桁も同じ比で見る。
public static partial class BridgeExpander
{
    private static void BuildBascule(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        var s = MakeSection(spec);
        int W = s.DeckW;

        int leaves = Clamp(spec.BridgeLeaves ?? 2, 1, 2);
        int leafLen = Clamp(spec.BridgeLeafSpan ?? 20, 6, 60);
        int angle = Clamp(spec.BridgeOpenAngle ?? 0, 0, 70);
        bool cw = spec.BridgeCounterweight;
        bool house = spec.BridgeMachineHouse;

        int fixedSpans = Clamp(spec.BridgeSpans ?? 1, 0, 6);
        int fixedSpan = Clamp(spec.BridgeSpan ?? 20, 8, 80);
        int crossStep = Clamp(spec.BridgeCrossStep ?? 6, 0, 24);
        int clear = Clamp(spec.BridgePierHeight ?? 8, 2, 40);
        int depthRatio = Clamp(spec.BridgeDepthRatio ?? 20, 12, 30);
        int gh = Clamp((int)Math.Round(Math.Max(fixedSpan, leafLen) / (double)depthRatio), 1, 6);

        int soffit = clear;
        int deckY = soffit + gh;
        int topY = deckY + s.TopDy;
        int railTop = topY + s.Rail;

        const int PierLen = 6;                 // 主橋脚の橋軸方向の長さ（錘のピットと機械室）
        int approachLen = fixedSpans * fixedSpan;
        int gap = leaves * leafLen;            // 可動径間（航路）

        int zP1 = approachLen;                 // 左の主橋脚の起点
        int zT1 = zP1 + PierLen;               // 左のトラニオン（可動桁の最初のマス）
        int zT2 = zT1 + gap;                   // 右の主橋脚の起点
        int total = zT2 + PierLen + approachLen;

        int hx0 = house ? -5 : 0;
        int hx1 = house ? W + 4 : W - 1;

        // ===== 固定径間 =====
        BuildFixed(0, zP1 - 1);
        BuildFixed(zT2 + PierLen, total - 1);

        void BuildFixed(int fz0, int fz1)
        {
            if (fz1 < fz0) return;
            Fill(cells, 0, 0, soffit, deckY - 1, fz0, fz1, p.Girder);
            Fill(cells, W - 1, W - 1, soffit, deckY - 1, fz0, fz1, p.Girder);
            if (crossStep > 0)
                for (int z = fz0; z <= fz1; z += crossStep)
                    Fill(cells, 0, W - 1, soffit, soffit, z, z, p.Girder);
            BuildDeckSurface(cells, p, s, deckY, fz0, fz1);
        }

        // 固定径間の中間橋脚。
        for (int i = 1; i < fixedSpans; i++)
        {
            int z = i * fixedSpan;
            Fill(cells, 0, W - 1, 0, soffit - 1, z - 1, z, p.Pier);
            int zr = zT2 + PierLen + i * fixedSpan;
            Fill(cells, 0, W - 1, 0, soffit - 1, zr - 1, zr, p.Pier);
        }

        // 取付部（橋台）。
        if (spec.BridgeAbutment)
        {
            Fill(cells, 0, W - 1, 0, deckY - 1, -3, -1, p.Pier);
            BuildDeckSurface(cells, p, s, deckY, -3, -1);
            Fill(cells, 0, W - 1, 0, deckY - 1, total, total + 2, p.Pier);
            BuildDeckSurface(cells, p, s, deckY, total, total + 2);
        }

        // ===== 主橋脚 =====
        BuildMainPier(zP1);
        BuildMainPier(zT2);

        void BuildMainPier(int pz0)
        {
            int pz1 = pz0 + PierLen - 1;
            Fill(cells, hx0, hx1, 0, soffit - 1, pz0, pz1, p.Pier);
            Fill(cells, 0, W - 1, soffit, deckY - 1, pz0, pz1, p.Pier);   // トラニオン台・ピット壁
            // 橋脚の上は固定の路面。トラニオンは橋脚の航路側の縁にあり、
            // その後方のピットには蓋をして路面が通る。これで閉じているときは
            // 取付部から可動桁まで路面が切れない。
            BuildDeckSurface(cells, p, s, deckY, pz0, pz1);
        }

        // ===== 可動桁 =====
        double rad = angle * Math.PI / 180.0;
        double ca = Math.Cos(rad), sa = Math.Sin(rad);

        BuildLeaf(zT1, +1);
        if (leaves == 2) BuildLeaf(zT2 - 1, -1);

        void BuildLeaf(int ztr, int dir)
        {
            // 0.25マス刻みで進める。角度が急なとき z より y が速く伸びるので、
            // 細かく刻んで階段状の桁が途切れないようにする。
            for (double t = 0.0; t < leafLen; t += 0.25)
            {
                int zz = ztr + (int)Math.Round(dir * t * ca);
                int yy = deckY + (int)Math.Round(t * sa);
                Fill(cells, 0, 0, yy - gh, yy - 1, zz, zz, p.Girder);
                Fill(cells, W - 1, W - 1, yy - gh, yy - 1, zz, zz, p.Girder);
                BuildDeckSurface(cells, p, s, yy, zz, zz);
            }
        }

        // ===== 釣合い錘 =====
        // 閉じているときはトラニオンの後方やや下。開くほど橋脚内のピットへ降りる。
        if (cw)
        {
            int armZ = Math.Max(2, leafLen / 3);     // 後方への腕の長さ
            int armY = Math.Max(2, leafLen / 6);     // 閉時の吊り下げ量
            BuildWeight(zT1, +1, zP1);
            if (leaves == 2) BuildWeight(zT2 - 1, -1, zT2);

            void BuildWeight(int ztr, int dir, int pz0)
            {
                // ピットを橋脚から抜く。錘の可動範囲ぶん確保する。
                int pitTop = deckY - 1;
                int pitBottom = Math.Max(1, deckY - armZ - armY - 2);
                int pz1 = pz0 + PierLen - 1;
                Carve(cells, 1, W - 2, pitBottom, pitTop, pz0 + 1, pz1 - 1);

                // 腕の先端をトラニオン中心に跳開角ぶん回す。
                double vz = -armZ * ca + armY * sa;
                double vy = -armZ * sa - armY * ca;
                int wz = ztr + (int)Math.Round(dir * vz);
                int wy = deckY + (int)Math.Round(vy);

                int hz = Math.Max(1, armZ / 3);
                int hy = Math.Max(2, armY);
                int z0 = Math.Min(wz + hz, wz - hz);
                Fill(cells, 1, W - 2,
                     Math.Max(0, wy - hy), Math.Max(0, wy),
                     Math.Min(wz - hz, wz + hz), Math.Max(wz - hz, wz + hz), p.Pier);

                // 腕（トラニオンと錘を結ぶ材）。
                for (double t = 0; t <= 1.0; t += 0.05)
                {
                    int az = ztr + (int)Math.Round(dir * vz * t);
                    int ay = deckY + (int)Math.Round(vy * t);
                    if (ay < 0 || ay >= deckY) continue;   // 腕は路面の下だけ
                    Fill(cells, 1, 1, ay, ay, az, az, p.Girder);
                    Fill(cells, W - 2, W - 2, ay, ay, az, az, p.Girder);
                }
            }
        }

        // ===== 機械室 =====
        // 主橋脚の上、路肩の外側に張り出す。橋の巻き上げ機械を収める建屋。
        if (house)
        {
            BuildHouse(zP1);
            BuildHouse(zT2);
        }

        void BuildHouse(int pz0)
        {
            int hz0 = pz0 + 1, hz1 = pz0 + PierLen - 2;
            for (int side = 0; side < 2; side++)
            {
                int x0 = side == 0 ? -5 : W;
                int x1 = side == 0 ? -1 : W + 4;
                Fill(cells, x0, x1, soffit, soffit, hz0, hz1, p.Deck);             // 床
                Fill(cells, x0, x1, soffit + 1, soffit + 4, hz0, hz1, p.Curb);     // 躯体
                Fill(cells, x0, x1, soffit + 5, soffit + 5, hz0, hz1, p.Deck);     // 陸屋根
                Carve(cells, x0 + 1, x1 - 1, soffit + 1, soffit + 4, hz0 + 1, hz1 - 1);
                // 外側の面に窓の帯。
                int wx = side == 0 ? x0 : x1;
                Fill(cells, wx, wx, soffit + 3, soffit + 3, hz0 + 1, hz1 - 1, p.Light);
            }
        }

        // ===== 照明 =====
        // 可動桁の上には立てない（跳ね上がるため）。固定径間だけに置く。
        int lightStep = Clamp(spec.BridgeLightStep ?? 30, 0, 80);
        BuildLights(cells, p, s, railTop, lightStep, 0, zP1 - 1);
        BuildLights(cells, p, s, railTop, lightStep, zT2 + PierLen, total - 1);
    }
}
