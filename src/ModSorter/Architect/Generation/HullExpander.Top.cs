using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 上部構造の組み立て。素の船体（HullExpander.Shell.cs）の上に、
// マスト・帆桁・帆・盾掛け・舵・船首材の飾りを載せる。
//
// 実物の根拠:
//   ゴクスタ船は全長23.22m・幅5.18m、外板16列、片舷16の櫂穴で盾は32枚。
//   マストは船体中央の帆柱受けに立ち、高さは11〜13m と推定される。
//   帆は1枚の横帆で面積110m²級（幅11m×丈10m 相当）。
//   舵は船尾の片舷に吊る舵（クォーターラダー）で、舵柄が船内へ伸びる。
//   舷墻は持たず、最上列の外板が舷縁を兼ねる。盾は舷縁の外側へ掛ける。
//   竜頭は1マス未満の彫刻なので、頭部の高さだけで表す。
public static partial class HullExpander
{
    private sealed class TopPalette
    {
        public readonly string Mast, Sail, Shield, Fitting;

        public TopPalette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Mast = Pick(spec.SuperstructureBlock, allowed, fallback);
            Sail = Pick(spec.RoofBlock, allowed, Mast);
            Shield = Pick(spec.TowerBlock, allowed, Mast);
            Fitting = Pick(spec.SeatBlock, allowed, Mast);
        }
    }

    // 上部構造の寸法。Extent と BuildTopside の両方がこれを通るので、
    // UI に出す外寸と生成物の外寸が食い違わない。
    private sealed class Top
    {
        public readonly int MastCount, MastHeight, SailW, SailH, ShieldPerSide, HeadHeight, TopY;
        public readonly string Sail, Head;
        public readonly bool SteeringOar;
        public readonly int[] MastZs;

        public Top(StructureSpec spec, Form f)
        {
            MastCount = Clamp(spec.HullMastCount ?? 0, 0, 3);
            MastHeight = Clamp(spec.HullMastHeight ?? Math.Max(3, f.L / 2), 2, 64);

            string sail = (spec.HullSail ?? "none").Trim().ToLowerInvariant();
            Sail = sail == "set" || sail == "furled" ? sail : "none";
            SailW = Clamp(spec.HullSailWidth ?? MastHeight, 2, 64);
            SailH = Clamp(spec.HullSailHeight ?? Math.Max(1, MastHeight - 1), 1, 64);

            ShieldPerSide = Clamp(spec.HullShieldPerSide ?? 0, 0, 32);
            SteeringOar = spec.HullSteeringOar ?? false;

            string head = (spec.HullStemHead ?? "none").Trim().ToLowerInvariant();
            Head = head == "spiral" || head == "dragon" ? head : "none";
            HeadHeight = Head == "dragon" ? 5 : Head == "spiral" ? 3 : 0;

            MastZs = new int[MastCount];
            int top = 0;
            for (int i = 0; i < MastCount; i++)
            {
                int z = (int)Math.Round(f.L * (i + 1.0) / (MastCount + 1.0));
                MastZs[i] = Math.Clamp(z, 1, Math.Max(1, f.L - 2));
                int y = f.DeckY(MastZs[i]) + MastHeight;
                if (y > top) top = y;
            }
            if (HeadHeight > 0)
            {
                int y = Math.Max(f.DeckY(0), f.DeckY(f.L - 1)) + HeadHeight;
                if (y > top) top = y;
            }
            TopY = top;
        }
    }

    private static void BuildTopside(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, StructureSpec spec, TopPalette t)
    {
        var top = new Top(spec, f);
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;

        // 1) マスト・帆桁・帆。帆桁と帆を先に置き、最後にマストの列で上書きする。
        //    実船でも帆はマストの後ろを通るので、交差はマストが勝つのが正しい。
        foreach (int z in top.MastZs)
        {
            int baseY = f.DeckY(z) + 1;
            int mastTop = baseY + top.MastHeight - 1;

            if (top.Sail != "none")
            {
                int yardY = Math.Max(baseY, mastTop - 1);
                int xa = cx0 - (top.SailW - 1) / 2;
                int xb = xa + top.SailW - 1;

                // 帆桁。横に寝た丸太なので axis=x を持たせる。回転で axis も回る。
                for (int x = xa; x <= xb; x++)
                {
                    var key = (x, yardY, z);
                    cells[key] = t.Mast;
                    props[key] = new Dictionary<string, string> { ["axis"] = "x" };
                }

                int sailTop = yardY - 1;
                int rows = top.Sail == "furled" ? 1 : top.SailH;
                for (int k = 0; k < rows; k++)
                {
                    int y = sailTop - k;
                    if (y < baseY) break;
                    for (int x = xa; x <= xb; x++) cells[(x, y, z)] = t.Sail;
                }
            }

            for (int y = baseY; y <= mastTop; y++)
                for (int x = cx0; x <= cx1; x++)
                {
                    cells[(x, y, z)] = t.Mast;
                    props.Remove((x, y, z));   // 帆桁の axis が縦のマストに残らないようにする
                }
        }

        // 2) 盾掛け。舷縁（�housing 舷墻があればその天端）の外側へ1マス出して並べる。
        if (top.ShieldPerSide > 0)
        {
            int z0 = Math.Max(1, f.L / 2 - top.ShieldPerSide / 2);
            for (int i = 0; i < top.ShieldPerSide; i++)
            {
                int z = z0 + i;
                if (z >= f.L - 1) break;

                int dk = f.DeckY(z);
                f.Span(f.HalfAt(z, dk), out int x0, out int x1);
                if (x1 - x0 < 1) continue;   // 舷が細るところには掛けない

                int y = dk + f.Bulwark;
                cells[(x0 - 1, y, z)] = t.Shield;
                cells[(x1 + 1, y, z)] = t.Shield;
            }
        }

        // 3) 舵（クォーターラダー）と舵柄。船尾の片舷（+x 側）に吊る。
        //    facade_face の回転で舷は入れ替わる。
        if (top.SteeringOar)
        {
            int z = Math.Max(1, f.L / 8);
            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);

            for (int k = 0; k < 3; k++)
            {
                int y = dk - k;
                if (y < 0) break;
                cells[(x1 + 1, y, z)] = t.Fitting;
            }
            cells[(x1, dk + 1 + f.Bulwark, z)] = t.Fitting;   // 舷内へ伸びる舵柄
        }

        // 4) 船首材・船尾材の飾り。z 方向へは出さず高さだけで表す。
        if (top.HeadHeight > 0)
        {
            PutHead(cells, f, t, top, f.L - 1);
            PutHead(cells, f, t, top, 0);
        }
    }

    private static void PutHead(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, TopPalette t, Top top, int z)
    {
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int baseY = f.DeckY(z) + 1;
        for (int k = 0; k < top.HeadHeight; k++)
        {
            // 上へ行くほど1列に絞る。渦巻きも竜頭も先端は細い。
            int a = k >= top.HeadHeight - 1 ? cx0 : cx0;
            int b = k >= top.HeadHeight - 1 ? cx0 : cx1;
            for (int x = a; x <= b; x++) cells[(x, baseY + k, z)] = t.Fitting;
        }
    }

    // 横に寝たブロックの axis。x と z が入れ替わり、y は変わらない。
    private static string RotateAxis(string axis, int turns)
    {
        if ((turns & 1) == 0) return axis;
        return axis switch { "x" => "z", "z" => "x", _ => axis };
    }
}
