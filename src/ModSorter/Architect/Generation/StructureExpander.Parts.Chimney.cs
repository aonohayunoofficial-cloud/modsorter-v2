using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 付加部品のうち煙突。StructureExpander の partial。
// 屋根の生成より後に呼ばれることを前提にしている（列ごとの実際の最高yを見るため）。
public static partial class StructureExpander
{
    // 煙突: 屋根の上に本数ぶん自動で等間隔に立てる。位置は寄せ方向(chimney_align)で決める。
    //   center（既定）… 建物の中心線上に、x軸に沿って等間隔で並ぶ。
    //   north/south   … その面寄り（z を端側へ）に寄せ、x軸に沿って並ぶ。
    //   east/west     … その面寄り（x を端側へ）に寄せ、z軸に沿って並ぶ。
    // 各煙突の(x,z)で、既に積まれた屋根の「その列の最大y」を調べ、そこから上へ
    // chimney_height マス積む。貫通ON(chimney_pierce)なら床上(y=1)から屋根を貫いて通す。
    private static void BuildChimney(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string chimney)
    {
        int count = spec.ChimneyCount;
        if (count <= 0) return;

        int stackH = spec.ChimneyHeight.HasValue && spec.ChimneyHeight.Value > 0
            ? spec.ChimneyHeight.Value : 2;

        string align = (spec.ChimneyAlign ?? "center").Trim().ToLowerInvariant();

        // 端から少し内側に寄せる余白（角に食い込ませない）。
        int margin = 1;

        // 並ぶ軸(along)と、寄せる固定座標を決める。
        // north/south は z を固定して x 方向に並ぶ。east/west は x を固定して z 方向に並ぶ。
        bool alongX; // true: x方向に並ぶ, false: z方向に並ぶ
        int fixedCoord; // 並ぶ軸に直交する側の固定値

        switch (align)
        {
            case "north": alongX = true; fixedCoord = margin; break;                 // z=手前寄り
            case "south": alongX = true; fixedCoord = d - 1 - margin; break;          // z=奥寄り
            case "west": alongX = false; fixedCoord = margin; break;                  // x=左寄り
            case "east": alongX = false; fixedCoord = w - 1 - margin; break;          // x=右寄り
            default: alongX = true; fixedCoord = (d - 1) / 2; break;                  // center: z中央、x方向
        }
        if (fixedCoord < 0) fixedCoord = 0;

        // 太さ → 断面オフセット（中心(cx,cz)からの相対(dx,dz)）と占有幅。
        //   thin   … 中心1マスのみ（従来）。占有幅1。
        //   medium … プラス型（中心を抜いた上下左右4マス・中空）。占有幅3。
        //   thick  … 中央2×2を抜いた4×4外周（12マス・中空2×2）。占有幅4。
        string thickness = (spec.ChimneyThickness ?? "thin").Trim().ToLowerInvariant();
        (int dx, int dz)[] section;
        int footprint;
        if (thickness == "medium")
        {
            section = new[] { (0, -1), (-1, 0), (1, 0), (0, 1) };
            footprint = 3;
        }
        else if (thickness == "thick")
        {
            // -1..2 の 4×4 から、中央2×2(0..1, 0..1)を除いた外周12マス。
            var ring = new List<(int, int)>();
            for (int ox = -1; ox <= 2; ox++)
                for (int oz = -1; oz <= 2; oz++)
                {
                    bool hole = (ox >= 0 && ox <= 1 && oz >= 0 && oz <= 1);
                    if (!hole) ring.Add((ox, oz));
                }
            section = ring.ToArray();
            footprint = 4;
        }
        else
        {
            section = new[] { (0, 0) };
            footprint = 1;
        }

        // 並ぶ軸の有効範囲（角を避けた内側）。太い煙突は断面が縁からはみ出さないよう
        // さらに (footprint-1) ぶん内側へ寄せる。
        int span = alongX ? w : d;
        int extra = footprint - 1;
        int lo = margin + extra, hi = span - 1 - margin - extra;
        if (hi < lo) { lo = 0; hi = span - 1; }

        // 本数クランプ: 並ぶ範囲に占有幅ぶんの間隔で収まる数を上限にする。
        int rangeLen = hi - lo + 1;
        int capacity = Math.Max(1, rangeLen / footprint);
        int n = Math.Min(count, capacity);

        // 固定座標側も断面が範囲外に出ないようクランプ（中/太で端寄せしたとき用）。
        int fixedClamped = Math.Clamp(fixedCoord, extra + 0,
            (alongX ? d : w) - 1 - extra);
        if (fixedClamped < 0) fixedClamped = 0;

        for (int i = 0; i < n; i++)
        {
            int p = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)System.Math.Round((double)(hi - lo) * i / (n - 1));

            int cx = alongX ? p : fixedClamped;
            int cz = alongX ? fixedClamped : p;
            if (cx < 0 || cx >= w || cz < 0 || cz >= d) continue;

            // その中心列の屋根の最高y（既に cells に積まれた最大y）。無ければ壁上端 h-1。
            int topY = h - 1;
            foreach (var k in cells.Keys)
                if (k.x == cx && k.z == cz && k.y > topY) topY = k.y;

            // 積み始めのy。貫通ONは床上(y=1)から、OFFは屋根上端の1つ上から。
            int startY = spec.ChimneyPierce ? 1 : topY + 1;
            // 煙突頂上 = 屋根上端 + stackH。
            int endY = topY + stackH;

            // 断面を全高に積む。medium/thick は中心が抜けるので自然に中空の筒になる。
            foreach (var (ox, oz) in section)
            {
                int bx = cx + ox, bz = cz + oz;
                if (bx < 0 || bx >= w || bz < 0 || bz >= d) continue;
                for (int y = startY; y <= endY; y++)
                    cells[(bx, y, bz)] = chimney;
            }
        }
    }
}
