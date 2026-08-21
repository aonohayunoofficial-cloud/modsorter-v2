using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== ヘリポート =====
    // 平面土木なので Scale で m からマスへ落とす。断面は「進入方向が z=0 側」で組む。
    //
    // 実寸（ICAO Annex 14 Vol.II）。すべて D 値（設計ヘリの全長）から決まる。
    //   FATO   … 1D。限定用途の地上式に限り 0.83D まで縮められる。
    //   TLOF   … 0.83D。FATO の中に置く。
    //   セーフティエリア … FATO の外へ 3m か 0.25D の大きい方。
    //   TLOF 縁灯 … 緑。間隔 5m 以下（円形）。
    //   TD/PM 円  … 内径 0.5D。
    //   H マーキング … D<16m のとき高さ 3m。
    private static void BuildHelipad(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        double dM = Clamp(spec.AirportHeliD ?? 15, 6, 40);
        bool marking = spec.AirportMarking;
        int lift = Clamp(spec.AirportHeliElevated ?? 0, 0, 24);
        bool fullFato = spec.AirportHeliFullFato;

        double fatoM = dM * (fullFato ? 1.0 : 0.83);
        double tlofM = dM * 0.83;
        double safeM = Math.Max(3.0, dM * 0.25);

        int fato = Odd(Math.Max(5, M(fatoM, scale)));
        int tlof = Odd(Math.Max(3, Math.Min(fato, M(tlofM, scale))));
        int safe = Math.Max(1, M(safeM, scale));

        int total = fato + safe * 2;      // セーフティエリア込みの一辺
        int c = total / 2;                // 中心
        int y = lift;                     // 舗装面の高さ

        // ===== 高架式の脚 =====
        if (lift > 0)
        {
            for (int i = 0; i < 4; i++)
            {
                int px = (i % 2 == 0) ? c - fato / 3 : c + fato / 3;
                int pz = (i < 2) ? c - fato / 3 : c + fato / 3;
                Fill(cells, px, px, 0, lift - 1, pz, pz, p.Shoulder);
            }
        }

        // ===== セーフティエリアと FATO =====
        Fill(cells, 0, total - 1, y, y, 0, total - 1, p.Shoulder);

        int f0 = safe, f1 = safe + fato - 1;
        Fill(cells, f0, f1, y, y, f0, f1, p.Pave);

        if (!marking) return;

        // FATO の外周（TLOF ではないので細い線でよい）。
        Fill(cells, f0, f1, y, y, f0, f0, p.Line);
        Fill(cells, f0, f1, y, y, f1, f1, p.Line);
        Fill(cells, f0, f0, y, y, f0, f1, p.Line);
        Fill(cells, f1, f1, y, y, f0, f1, p.Line);

        // ===== TLOF の外周 =====
        int t0 = c - tlof / 2, t1 = c + tlof / 2;
        Fill(cells, t0, t1, y, y, t0, t0, p.Mark);
        Fill(cells, t0, t1, y, y, t1, t1, p.Mark);
        Fill(cells, t0, t0, y, y, t0, t1, p.Mark);
        Fill(cells, t1, t1, y, y, t0, t1, p.Mark);

        // ===== TD/PM 円（内径 0.5D）=====
        int r = Math.Max(2, M(dM * 0.5, scale) / 2);
        for (int dx = -r - 1; dx <= r + 1; dx++)
            for (int dz = -r - 1; dz <= r + 1; dz++)
            {
                double d2 = dx * dx + dz * dz;
                if (d2 <= (r + 0.5) * (r + 0.5) && d2 >= (r - 0.5) * (r - 0.5))
                    cells[(c + dx, y, c + dz)] = p.Mark;
            }

        // ===== H マーキング =====
        // D<16m のとき高さ 3m。円の内側に収まる大きさにする。
        double hHeightM = (dM < 16.0) ? 3.0 : dM * 0.2;
        int hh = Math.Max(3, M(hHeightM, scale));
        if (hh % 2 == 0) hh++;
        int hw = Math.Max(2, hh * 2 / 3);
        if (hw % 2 == 0) hw++;

        int hz0 = c - hh / 2, hz1 = c + hh / 2;
        int hx0 = c - hw / 2, hx1 = c + hw / 2;

        Fill(cells, hx0, hx0, y, y, hz0, hz1, p.Line);   // 縦棒（左）
        Fill(cells, hx1, hx1, y, y, hz0, hz1, p.Line);   // 縦棒（右）
        Fill(cells, hx0, hx1, y, y, c, c, p.Line);       // 横棒

        // ===== TLOF 縁灯（緑・間隔 5m 以下）=====
        int ls = Math.Max(1, M(5.0, scale));
        for (int x = t0; x <= t1; x += ls)
        {
            cells[(x, y + 1, t0)] = p.Light;
            cells[(x, y + 1, t1)] = p.Light;
        }
        for (int z = t0; z <= t1; z += ls)
        {
            cells[(t0, y + 1, z)] = p.Light;
            cells[(t1, y + 1, z)] = p.Light;
        }
    }
}
