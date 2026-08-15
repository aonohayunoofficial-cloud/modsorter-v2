using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// アーチ橋（structure_type="bridge:arch_bridge"）。
//
// 形式は3つ。
//   deck    上路式 … アーチが床版の下。リブは床版の下に等間隔で並べ、支柱で受ける。
//   through 下路式 … アーチが床版の上。リブは床版の外側に立て、吊材で吊る。
//   half    中路式 … 床版がライズの中ほど。下は支柱、上は吊材になる。
//
// 実寸の根拠。
//   ライズ比 … 支間の1/5〜1/10（日本大百科全書）。既定1/5。
//   適用支間 … タイドアーチで50〜170m級（JFE）。
//   タイ材   … 下路式のタイドアーチはアーチの水平反力を橋自身で受ける。
public static partial class BridgeExpander
{
    private static void BuildArch(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        var s = MakeSection(spec);
        int W = s.DeckW;

        int span = Clamp(spec.BridgeSpan ?? 60, 16, 200);
        int riseRatio = Clamp(spec.BridgeRiseRatio ?? 5, 4, 10);
        int rise = Math.Max(3, span / riseRatio);
        int vstep = Clamp(spec.BridgeVerticalStep ?? 5, 2, 20);
        int ribs = Clamp(spec.BridgeArchRibs ?? 2, 2, 4);
        bool tie = spec.BridgeTie;
        bool brace = spec.BridgeBracing;
        int springH = Clamp(spec.BridgePierHeight ?? 4, 0, 40);   // 起拱点の高さ
        int approach = spec.BridgeAbutment ? Math.Max(4, span / 8) : 0;

        string type = (spec.BridgeArchType ?? "deck").Trim().ToLowerInvariant();
        if (type != "through" && type != "half") type = "deck";

        int z0 = approach;
        int z1 = z0 + span - 1;
        int total = span + approach * 2;

        int springY = springH;
        int crownY = springY + rise;
        int deckY = type switch
        {
            "deck" => crownY + 2,            // 上路式。クラウンの上に支柱1マス＋床版
            "half" => springY + rise / 2,    // 中路式
            _ => springY,                    // 下路式。床版は起拱点の高さ
        };
        int topY = deckY + s.TopDy;
        int railTop = topY + s.Rail;

        // アーチリブの x。上路式は床版の下、下路式・中路式は床版の外側。
        var rx = new List<int>();
        if (type == "deck")
        {
            for (int i = 0; i < ribs; i++)
                rx.Add(Clamp((int)Math.Round((W - 1.0) * (i + 0.5) / ribs), 0, W - 1));
            rx = rx.Distinct().OrderBy(v => v).ToList();
        }
        else
        {
            rx.Add(-1);
            rx.Add(W);
        }

        int ArchY(int z)
        {
            double u = (2.0 * z - (z0 + z1)) / Math.Max(1, z1 - z0);
            if (u < -1) u = -1;
            if (u > 1) u = 1;
            return springY + (int)Math.Round(rise * (1 - u * u));
        }

        // ===== アーチリブ =====
        // 隣り合う z の高さの差を縦に埋めて、勾配の急な起拱部でも切れないようにする。
        int prev = ArchY(z0);
        for (int z = z0; z <= z1; z++)
        {
            int y = ArchY(z);
            int lo = Math.Min(prev, y);
            int hi = Math.Max(prev, y);
            foreach (int x in rx) Fill(cells, x, x, lo, hi, z, z, p.Cable);
            prev = y;
        }

        // ===== 鉛直材（支柱・吊材）=====
        for (int z = z0; z <= z1; z += vstep)
        {
            int ay = ArchY(z);
            foreach (int x in rx)
            {
                if (ay < deckY - 1) Fill(cells, x, x, ay + 1, deckY - 1, z, z, p.Hanger);
                else if (ay > railTop + 1) Fill(cells, x, x, railTop + 1, ay - 1, z, z, p.Hanger);
            }
        }

        // ===== リブ間の横構 =====
        if (brace && rx.Count >= 2)
            for (int z = z0; z <= z1; z += vstep)
            {
                int ay = ArchY(z);
                if (type != "deck" && ay < railTop + 3) continue;   // 路面を塞がない高さだけ
                Fill(cells, rx[0], rx[rx.Count - 1], ay, ay, z, z, p.Cable);
            }

        // ===== タイ材 =====
        // 下路式・中路式のタイドアーチ。アーチ端どうしを床版の高さで結ぶ。
        if (tie && type != "deck")
            foreach (int x in rx)
                Fill(cells, x, x, deckY, deckY, z0, z1, p.Cable);

        // ===== 起拱部の基礎 =====
        // アーチの推力を受けるので床版より広く取る。
        if (springY > 0)
        {
            int ax0 = Math.Min(0, rx.Min()) - 1;
            int ax1 = Math.Max(W - 1, rx.Max()) + 1;
            Fill(cells, ax0, ax1, 0, springY - 1, z0 - 2, z0 + 1, p.Pier);
            Fill(cells, ax0, ax1, 0, springY - 1, z1 - 1, z1 + 2, p.Pier);
        }

        // ===== 取付部（橋台）=====
        if (approach > 0)
        {
            Fill(cells, 0, W - 1, 0, deckY - 1, 0, approach - 1, p.Pier);
            Fill(cells, 0, W - 1, 0, deckY - 1, total - approach, total - 1, p.Pier);
        }

        // ===== 床版まわり =====
        // 床版の下に縦桁を1マス通し、支柱・吊材の間を持たせる。
        Fill(cells, 0, 0, deckY - 1, deckY - 1, z0, z1, p.Girder);
        Fill(cells, W - 1, W - 1, deckY - 1, deckY - 1, z0, z1, p.Girder);
        BuildDeckSurface(cells, p, s, deckY, 0, total - 1);

        // ===== 照明 =====
        BuildLights(cells, p, s, railTop, Clamp(spec.BridgeLightStep ?? 30, 0, 80), 0, total - 1);
    }
}
