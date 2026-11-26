using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// コックピット（甲板の一部だけを開けた座席の凹み）・風防・サンパッド。
// 開放艇（HullExpander.Shell.cs / Thwart.cs）は全長で甲板を開けるが、
// ランナバウトは前甲板と船尾の甲板を張ったまま、中央の範囲だけを凹ませる。
// 甲板を開けるのは BuildBareHull（Opens を受け取る）で、ここは床・座席・
// 風防・サンパッドを置くだけを受け持つ。
//
// 実物の根拠（リーヴァ・アクアラマ・スペシャル 1972〜96）:
//   全長8.78m・型幅2.60m・喫水0.63m。前甲板・中央のコックピット・
//   機関室の上のサンパッド・巻き込み風防の並び（classicboats.co）。
//   コックピットは甲板から凹んだ床に座席2列と立ち位置を持つ（Capri Luxury Boats）。
// 1マス=1mでの表し方:
//   凹みの深さは1マス（甲板の1段下が床）。立てば腰から上が甲板より上に出る。
//   座席は床の上の下付き半ブロックで、座面は床から0.5m（実物の座面高0.45m級）。
//   座席1列は座面1マス＋足元1マスの2マス。最前列の前にも足元を残し、
//   その前の station に風防を立てる。
//   サンパッドは甲板の上に敷くカーペット（厚さ1/16）。半ブロックでは厚すぎる。
//
// デッキハウス・船楼・貨物艙口・開放艇のどれかがあるときは開けない。
// どれも甲板の上か甲板そのものを使うので、同じ座標を奪い合う。
public static partial class HullExpander
{
    // Extent（UI の外寸）と BuildCockpit の両方がこれを通るので値が食い違わない。
    private sealed class CockpitFit
    {
        public readonly int Z0, Z1, Len;   // Len が0ならなし
        public readonly int[] SeatZs;
        public readonly bool Windscreen;
        public readonly int PadZ0, PadZ1;  // PadZ1 < PadZ0 ならなし
        public readonly int TopY;          // 甲板より上へ出る最上段。-1でなし

        public CockpitFit(StructureSpec spec, Form f, Top top)
        {
            SeatZs = Array.Empty<int>();
            PadZ0 = 1;
            PadZ1 = 0;
            TopY = -1;

            int pct = Clamp(spec.HullCockpitLength ?? 0, 0, 60);
            bool blocked = top.OpenBoat || top.HouseDecks > 0 || top.Holds > 0
                || top.CastleAft > 0 || top.CastleFore > 0;
            if (pct <= 0 || blocked) return;

            int len = Math.Max(2, (int)Math.Round(f.L * pct / 100.0));
            int shift = Clamp(spec.HullCockpitShift ?? 0, -60, 60);
            int z0 = Math.Max(1, (f.L - len) / 2 + shift);
            int z1 = Math.Min(f.L - 2, z0 + len - 1);

            // 舷が寄って甲板の内側が無い station（船首の細りなど）は開けない。
            while (z0 <= z1 && Narrow(f, z0)) z0++;
            while (z1 >= z0 && Narrow(f, z1)) z1--;
            if (z1 - z0 + 1 < 2) return;

            Z0 = z0;
            Z1 = z1;
            Len = z1 - z0 + 1;

            // 座席は後端から前へ2マスおき。最前列の前にも足元を1マス残す。
            int rows = Clamp(spec.HullCockpitSeats ?? 0, 0, 4);
            var seats = new List<int>();
            for (int z = z0; z <= z1 - 1 && seats.Count < rows; z += 2) seats.Add(z);
            SeatZs = seats.ToArray();

            // z1 は L-2 以下なので、風防の station z1+1 は必ず船体の内にある。
            int yTop = -1;
            Windscreen = spec.HullWindscreen ?? false;
            if (Windscreen)
                yTop = Math.Max(f.DeckY(z1 + 1), f.DeckY(z1)) + 1;

            // サンパッドはコックピットの後端から船尾側へ。船尾の端の station は空ける。
            int pad = Clamp(spec.HullSunPad ?? 0, 0, 16);
            if (pad > 0)
            {
                PadZ1 = z0 - 1;
                PadZ0 = Math.Max(1, z0 - pad);
                for (int z = PadZ0; z <= PadZ1; z++) yTop = Math.Max(yTop, f.DeckY(z) + 1);
            }
            TopY = yTop;
        }

        public bool Opens(int z) => Len > 0 && z >= Z0 && z <= Z1;

        private static bool Narrow(Form f, int z)
        {
            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            return x1 - x0 < 2;
        }
    }

    // UI の要約文が「実際に開いたか」を見るための口。条件の式を UI 側へ二重に持たない。
    public static int CockpitLength(StructureSpec spec)
    {
        var f = new Form(spec);
        return new CockpitFit(spec, f, new Top(spec, f)).Len;
    }

    // 床 → 座席 → 風防 → サンパッドの順。どれも空いたセルにだけ置く。
    private static void BuildCockpit(
        Dictionary<(int x, int y, int z), string> cells, Form f, CockpitFit c,
        Palette p, TopPalette t, StructureSpec spec, IReadOnlyList<string> allowed)
    {
        if (c.Len <= 0) return;

        string seat = Pick(spec.HullCockpitSeatBlock, allowed, p.Frame);
        string pad = Pick(spec.HullCushionBlock, allowed, seat);

        // 床。甲板を開けたマスの真下（甲板の1段下）へ張る。甲板が残ったマス
        // （真下に船体が無いマス）は開いていないので床も要らない。
        for (int z = c.Z0; z <= c.Z1; z++)
        {
            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            for (int x = x0 + 1; x <= x1 - 1; x++)
            {
                if (cells.ContainsKey((x, dk, z))) continue;
                CockpitPut(cells, (x, dk - 1, z), p.Frame);
            }
        }

        // 座席。開けたマス（甲板の高さ）へ置く。下付きの半ブロックなら座面は床から0.5m。
        foreach (int z in c.SeatZs)
        {
            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            for (int x = x0 + 1; x <= x1 - 1; x++) CockpitPut(cells, (x, dk, z), seat);
        }

        // 風防。前の station の甲板の上へ横一列、最前列の両舷の舷縁の上へ1マスずつ回す。
        // 舷墻があればそちらが先に座標を持っているので置かない。
        if (c.Windscreen)
        {
            int zw = c.Z1 + 1;
            int dw = f.DeckY(zw);
            f.Span(f.HalfAt(zw, dw), out int w0, out int w1);
            for (int x = w0; x <= w1; x++) CockpitPut(cells, (x, dw + 1, zw), t.Glass);

            int dz = f.DeckY(c.Z1);
            f.Span(f.HalfAt(c.Z1, dz), out int s0, out int s1);
            CockpitPut(cells, (s0, dz + 1, c.Z1), t.Glass);
            CockpitPut(cells, (s1, dz + 1, c.Z1), t.Glass);
        }

        // サンパッド。甲板のあるマスの上へだけ敷く（宙に浮かせない）。
        for (int z = c.PadZ0; z <= c.PadZ1; z++)
        {
            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            for (int x = x0; x <= x1; x++)
                if (cells.ContainsKey((x, dk, z))) CockpitPut(cells, (x, dk + 1, z), pad);
        }
    }

    // PutIfEmpty（Screw.cs）は型幅の外を捨てるが、フレアで甲板が型幅より広がる船では
    // 風防・サンパッドが欠けるので、こちらは範囲を絞らずに空きだけを見る。
    private static void CockpitPut(
        Dictionary<(int x, int y, int z), string> cells, (int x, int y, int z) key, string id)
    {
        if (!cells.ContainsKey(key)) cells[key] = id;
    }
}
