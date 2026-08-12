using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 跨線橋。通路が x 方向（線路を横切る向き）に走る。
// y=0 はレール面。height は桁下高さ＝レール面から桁の下端まで。
//
// ===== 実寸の出典 =====
//   桁下高さ … 建築限界は直流電化で高さ5700mm。跨線橋はこれを侵せないので 6 以上。
//              非電化・地下式なら下げられるが、既定は電化区間に合わせる。
//   通路幅   … 3m 級。階段幅が3mを超えると中間手すりが要る（建築基準法）。
//   階段     … 一般用は幅75cm以上・蹴上げ22cm以下・踏面21cm以上。
//              バリアフリー誘導基準は幅140cm以上・蹴上げ16cm以下・踏面30cm以上。
//              1マス=1m では蹴上げ1マス固定なので、踏面（run）で勾配を合わせる。
//              run=2 で約26.6度＝実物の蹴上げ0.16m/踏面0.30m（約28度）に最も近い。
public static partial class RailwayExpander
{
    private static string StairOf(string? v) => (v ?? "both").Trim().ToLowerInvariant() switch
    {
        "one" => "one",
        "none" => "none",
        _ => "both",
    };

    private static void BuildOverpass(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int span = Clamp(spec.RailSpan ?? 20, 6, 128);
        int w = Clamp(spec.Width, 2, 12);
        int h = Clamp(spec.Height, 6, 24);
        string stair = StairOf(spec.RailStair);
        int sw = Clamp(spec.RailStairWidth ?? w, 2, 12);
        int run = Clamp(spec.RailStairRun ?? 2, 1, 4);
        int wallH = Clamp(spec.RailWallHeight ?? 2, 1, 4);
        bool covered = spec.RailCovered;
        int pierStep = Clamp(spec.RailPierStep ?? 0, 0, 64);
        int lightStep = Clamp(spec.RailLightStep ?? 8, 0, 32);
        string shape = RoofShapeOf(spec.RailCanopyRoof);
        int pitch = Clamp(spec.RailRoofPitch ?? 3, 1, 8);

        if (sw > w) sw = w;
        int deckY = h;

        // 桁と床
        Fill(cells, 0, span - 1, deckY, deckY, 0, w - 1, p.Girder);

        // 腰壁・手すり
        Fill(cells, 0, span - 1, deckY + 1, deckY + wallH, 0, 0, p.Fence);
        Fill(cells, 0, span - 1, deckY + 1, deckY + wallH, w - 1, w - 1, p.Fence);

        // 屋根。腰壁の上に柱を立て、頭上に2マス空けてから架ける。
        if (covered)
        {
            int postTop = deckY + wallH + 2;
            for (int x = 0; x < span; x += 5)
            {
                Fill(cells, x, x, deckY + wallH + 1, postTop, x, x, p.Body);
                Fill(cells, x, x, deckY + wallH + 1, postTop, 0, 0, p.Body);
                Fill(cells, x, x, deckY + wallH + 1, postTop, w - 1, w - 1, p.Body);
            }
            RoofSheet(cells, shape, false, 0, w - 1, postTop + 1, pitch, 0, span - 1, p.Girder);
        }

        // 照明。屋根つきなら天井、なしなら腰壁の天端に置く。
        if (lightStep > 0)
        {
            int ly = covered ? deckY + wallH + 2 : deckY + wallH;
            for (int x = lightStep / 2; x < span; x += lightStep)
                cells[(x, ly, w / 2)] = p.Trim;
        }

        // 橋台と中間橋脚。階段が付く側は階段躯体が支えるので橋台は立てない。
        bool stairLeft = stair == "both" || stair == "one";
        bool stairRight = stair == "both";
        if (!stairLeft) Abutment(cells, 0, w, deckY, p);
        if (!stairRight) Abutment(cells, span - 1, w, deckY, p);
        if (pierStep > 0)
            for (int x = pierStep; x < span - 1; x += pierStep)
                Abutment(cells, x, w, deckY, p);

        // 階段
        if (stairLeft) Stair(cells, -1, 0, span, w, sw, deckY, run, wallH, p);
        if (stairRight) Stair(cells, 1, 0, span, w, sw, deckY, run, wallH, p);
    }

    // 橋台・橋脚。通路の両縁に1本ずつ地面まで落とす。
    private static void Abutment(Dictionary<(int x, int y, int z), string> cells,
        int x, int w, int deckY, Palette p)
    {
        Fill(cells, x, x, 0, deckY - 1, 0, 0, p.Body);
        Fill(cells, x, x, 0, deckY - 1, w - 1, w - 1, p.Body);
    }

    // 階段。デッキから地面へ1段=1m で下る。踏面は run マス。
    // dir=-1 で x のマイナス側、+1 でプラス側へ伸びる。下端に踊り場を付ける。
    private static void Stair(Dictionary<(int x, int y, int z), string> cells,
        int dir, int x0, int span, int w, int sw, int deckY, int run, int wallH, Palette p)
    {
        int z0 = (w - sw) / 2;
        int z1 = z0 + sw - 1;
        int edge = dir < 0 ? x0 - 1 : span;

        for (int k = 1; k <= deckY; k++)
        {
            int y = deckY - k;
            int a = edge + dir * ((k - 1) * run);
            int b = edge + dir * (k * run - 1);
            int lo = Math.Min(a, b), hi = Math.Max(a, b);

            Fill(cells, lo, hi, y, y, z0, z1, p.Pave);          // 踏面
            Fill(cells, lo, hi, 0, y - 1, z0, z1, p.Body);      // 躯体
            Fill(cells, lo, hi, y + 1, y + wallH, z0, z0, p.Fence);
            Fill(cells, lo, hi, y + 1, y + wallH, z1, z1, p.Fence);
        }

        // 踊り場（2マス）
        int px = edge + dir * (deckY * run);
        int qx = edge + dir * (deckY * run + 1);
        int plo = Math.Min(px, qx), phi = Math.Max(px, qx);
        Fill(cells, plo, phi, 0, 0, z0, z1, p.Pave);
        Fill(cells, plo, phi, 1, wallH, z0, z0, p.Fence);
        Fill(cells, plo, phi, 1, wallH, z1, z1, p.Fence);
    }
}
