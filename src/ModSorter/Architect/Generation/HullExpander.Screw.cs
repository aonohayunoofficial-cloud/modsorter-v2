using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 推進器の寸法とプロペラ。機走船（モーターボート・スピードボート・ヨット・
// タグボート・トロール船・近代軍艦5種ほか計12船種）が共通で使う部品なので、
// 船種ごとのビルダーを作らず HullExpander の1枚として持つ。
// 軸・シャフトストラット・舵は HullExpander.Screw.Shaft.cs にある。
//
// 実物の根拠:
//   プロペラ径は「下端が船体の最下点より下へ出ない・上端が最軽喫水線より下」で
//   決まる（DMS "Propellers By the Numbers"）。実船の径/喫水は
//   リバティ船 SS Jeremiah O'Brien が5.5m/8.46m＝0.65、
//   バートラム46が28in＝0.71m/1.37m＝0.52。ここは0.65を取る。
//   舵面積は水面下側面積 L×T の1.5%級（Wärtsilä Encyclopedia）。
//   羽根は実物3〜5枚で、振動を避けるため奇数が好まれる（DMS）が、
//   1マス=1mの格子では90度おきの4枚しか置けないので4枚で表す。
//
// 中世の中心線舵（HullExpander.Beam.cs の BuildSternRudder）とは別物。
// あちらは船尾材の後ろへ吊る舵で z=-1 を使うため、両方を同時には置かない。
public static partial class HullExpander
{
    // 推進器の寸法。Extent（UI に出す外寸）と BuildScrew の両方がこれを通るので、
    // スライダーの表示値と生成物の外寸が食い違わない。
    private sealed class ScrewFit
    {
        public readonly int Count;       // 軸数 0〜2
        public readonly int Dia;         // プロペラ径（奇数マス）
        public readonly int Chord;       // 舵の前後長
        public readonly int PropBottom;  // プロペラ下端の y（基線より下なら負）
        public readonly int ShaftY;      // 軸心の y（プロペラの中心）
        public readonly bool Rudder;
        public readonly int[] Xs;        // 軸の x

        public ScrewFit(StructureSpec spec, Form f)
        {
            Count = Clamp(spec.HullScrews ?? 0, 0, 2);

            // 径。喫水の0.65倍を上限に、船尾のアパーチャ（船底が基線から上がって
            // できる空き）＋1マスへ収める。こうすると下端は基線より1マス下までに
            // 収まるので、竜骨の張り出しが1以上あれば船体の最下点より下へ出ない。
            // 船尾の立ち上がり（hull_stern_rise）を上げるとアパーチャが深くなり、
            // 排水量型では実物の径へ近づく。
            int b0 = f.BottomY(0);
            int d = Math.Min((int)Math.Round(0.65 * f.WL), b0 + 1);
            if (d < 1) d = 1;
            if ((d & 1) == 0) d--;              // 中心を格子へ合わせるため奇数へ丸める
            Dia = Math.Max(1, d);

            // 上端は船底の1マス下、かつ喫水線より下（水面から出ると空転する）。
            int top = Math.Min(b0 - 1, f.WL);
            PropBottom = top - Dia + 1;
            ShaftY = PropBottom + (Dia - 1) / 2;

            // 舵の前後長。舵面積は L×T の1.5%級だが、1マス=1mでは丈（径ぶん）と
            // 前後長の2つしか持てないので前後長を径の半分に取る。
            Chord = Clamp(Dia / 2, 1, 3);

            // 中心線舵を選んでいるときはそちらが z=-1 を使うので舵を置かない。
            // 同じ座標を2つの部材で奪い合わせない。
            Rudder = Count > 0 && !(spec.HullSternRudder ?? false);

            Xs = ShaftXs(f, Count);
        }

        // 軸の x。1軸は船体中央、2軸は型幅の1/4ずつ左右へ振る。
        // 振り幅は喫水線での船尾の幅へ収める。船体の影から外へ出すと、
        // 上から見て船体の外にプロペラが並ぶ形になる。
        private static int[] ShaftXs(Form f, int count)
        {
            if (count <= 0) return Array.Empty<int>();

            int cx = (f.B - 1) / 2;
            if (count == 1) return new[] { cx };

            f.Span(f.HalfAt(0, f.WL), out int x0, out int x1);
            int dx = Math.Max(1, f.B / 4);
            int a = Math.Clamp(cx - dx, x0, x1);
            int b = Math.Clamp(cx + dx, x0, x1);
            if (a == b) return new[] { cx };    // 船尾が細くて2軸が入らない
            return new[] { a, b };
        }
    }

    // 推進器の組み立て。プロペラ → 軸 → 舵の順。素材は舵・貫通横梁と同じ Fitting。
    private static void BuildScrew(
        Dictionary<(int x, int y, int z), string> cells, Form f, StructureSpec spec, TopPalette t)
    {
        var s = new ScrewFit(spec, f);
        if (s.Count <= 0) return;

        foreach (int xc in s.Xs)
        {
            PutProp(cells, f, s, t, xc);
            PutShaft(cells, f, s, t, xc);
            if (s.Rudder) PutScrewRudder(cells, f, s, t, xc);
        }
    }

    // プロペラ。船尾の station（z=0）へ、ハブと90度おきの4枚羽根を x–y 面へ置く。
    // 軸は z 方向なので、円盤は軸に直角な面＝x–y 面に立つ。径1マスならハブだけ。
    private static void PutProp(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, ScrewFit s, TopPalette t, int xc)
    {
        int r = (s.Dia - 1) / 2;

        PutIfEmpty(cells, f, (xc, s.ShaftY, 0), t.Fitting);
        for (int k = 1; k <= r; k++)
        {
            PutIfEmpty(cells, f, (xc - k, s.ShaftY, 0), t.Fitting);
            PutIfEmpty(cells, f, (xc + k, s.ShaftY, 0), t.Fitting);
            PutIfEmpty(cells, f, (xc, s.ShaftY - k, 0), t.Fitting);
            PutIfEmpty(cells, f, (xc, s.ShaftY + k, 0), t.Fitting);
        }
    }

    // station z・高さ y に船体があるか。船底線（BottomY）だけで見ると、基線で
    // 船底が竜骨の1列に落ちる深いVの滑走艇で「真上に船体がある」と取り違える。
    // 外板の実際の位置はその高さでの半幅から出る。
    private static bool Inside(Form f, int x, int y, int z)
    {
        if (z < 0 || z > f.L - 1) return false;
        if (y < f.BottomY(z) || y > f.DeckY(z)) return false;
        f.Span(f.HalfAt(z, y), out int x0, out int x1);
        return x >= x0 && x <= x1;
    }

    // 置かれていないセルにだけ置く。竜骨・外板・中心線舵と座標を奪い合わせない。
    // 型幅の外へも出さない。外寸の幅は船体の張り出しから取るので、
    // ここで外へ出すと表示値と生成物が食い違う。
    private static void PutIfEmpty(
        Dictionary<(int x, int y, int z), string> cells, Form f,
        (int x, int y, int z) key, string id)
    {
        if (key.x < 0 || key.x > f.B - 1) return;
        if (!cells.ContainsKey(key)) cells[key] = id;
    }
}
