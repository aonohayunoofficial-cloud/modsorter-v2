using System;

namespace ModSorter.Architect.Generation;

// 断面生成器。主要目と船型パラメータから、station（z 1マスごと）の
// 船底線 BottomY / 甲板高さ DeckY / その高さでの半幅 HalfAt を出す。
// 船種ごとのビルダーはこの Form だけを見て組み立てるので、船型の式は1か所に集まる。
public static partial class HullExpander
{
    private sealed class Form
    {
        // 主要目（1マス=1m）。
        public readonly int L;   // 全長 LOA
        public readonly int B;   // 型幅（水線での最大幅）
        public readonly int D;   // 深さ（基線→船体中央の甲板）
        public readonly int WL;  // 設計喫水

        // 付帯。
        public readonly int FrameStep, KeelDepth, Bulwark;

        private readonly double _half;                  // B/2
        private readonly double _k;                     // 断面のふくらみ指数
        private readonly double _entry, _run;           // 船首テーパー長・船尾の絞り長
        private readonly double _bowExp, _sternExp;     // 水線の肥え
        private readonly double _transom;               // トランサム幅の割合
        private readonly double _flare, _tumble;        // それぞれ tan
        private readonly double _sheerMul;
        private readonly int _stemRun, _sternRise;

        public Form(StructureSpec spec)
        {
            L = Clamp(spec.HullLength ?? 24, 4, 400);
            B = Clamp(spec.HullBeam ?? 6, 2, 80);
            D = Clamp(spec.HullDepth ?? 4, 2, 40);
            WL = Clamp(spec.HullDraft ?? Math.Max(1, D * 2 / 3), 1, D);
            _half = B / 2.0;

            // 断面のふくらみ。0→k=1 の直線V、40付近→k=2 の円弧、100→k=8 のほぼ矩形。
            int sec = Clamp(spec.HullSection ?? 45, 0, 100);
            _k = 1.0 + 7.0 * Math.Pow(sec / 100.0, 2.0);

            // 入角（水線の半角）から船首テーパー長を出す。実船で8〜20度。
            double a = Clamp(spec.HullEntryAngle ?? 18, 5, 60) * Math.PI / 180.0;
            double e = _half / Math.Tan(a);
            double r = L * Clamp(spec.HullRunRatio ?? 30, 5, 60) / 100.0;
            // 船首テーパーと船尾の絞りが全長を食い切ると平行部が消えて船型が破綻するので、
            // 合計が全長を超えるときは比率を保ったまま縮める。
            double lim = Math.Max(2.0, L - 1.0);
            if (e + r > lim) { double s = lim / (e + r); e *= s; r *= s; }
            _entry = Math.Max(1.0, e);
            _run = Math.Max(1.0, r);

            _bowExp = 2.0 - 1.4 * (Clamp(spec.HullBowFullness ?? 45, 0, 100) / 100.0);
            _sternExp = 2.0 - 1.4 * (Clamp(spec.HullSternFullness ?? 60, 0, 100) / 100.0);
            _transom = Clamp(spec.HullTransom ?? 35, 0, 90) / 100.0;

            _flare = Math.Tan(Clamp(spec.HullFlare ?? 12, 0, 45) * Math.PI / 180.0);
            _tumble = Math.Tan(Clamp(spec.HullTumblehome ?? 0, 0, 30) * Math.PI / 180.0);
            _sheerMul = Clamp(spec.HullSheer ?? 100, 0, 400) / 100.0;

            double rake = Clamp(spec.HullStemRake ?? 15, 0, 70) * Math.PI / 180.0;
            _stemRun = Clamp((int)Math.Round(D * Math.Tan(rake)), 0, Math.Max(0, L / 3));
            _sternRise = Clamp(spec.HullSternRise ?? Math.Max(1, WL / 2), 0, D - 1);

            int fs = spec.HullFrameStep ?? 4;
            FrameStep = fs <= 0 ? 0 : Clamp(fs, 2, 32);
            KeelDepth = Clamp(spec.HullKeelDepth ?? 1, 0, 4);
            Bulwark = Clamp(spec.HullBulwark ?? 1, 0, 6);
        }

        // ICLL 1966 の標準シアの縦距。船尾垂線から船首垂線までの7点で、係数に
        // (L/3+10) を掛けた mm がその位置の立ち上がり。船首側が船尾側の2倍になる。
        private static readonly double[] SheerOrd = { 25.0, 11.1, 2.8, 0.0, 5.6, 22.2, 50.0 };

        // z 位置のシア（マス）。station 間は7点を直線でつなぐ。
        private double SheerAt(int z)
        {
            if (_sheerMul <= 0) return 0;
            double t = L <= 1 ? 0.5 : (double)z / (L - 1);
            double s = Math.Clamp(t, 0, 1) * 6.0;
            int i = Math.Min(5, (int)Math.Floor(s));
            double u = s - i;
            double f = SheerOrd[i] + (SheerOrd[i + 1] - SheerOrd[i]) * u;
            return f * (L / 3.0 + 10.0) / 1000.0 * _sheerMul;
        }

        // 甲板の高さ。船体中央が深さ D で、船首・船尾はシアぶん上がる。
        public int DeckY(int z) => Clamp(D + (int)Math.Round(SheerAt(z)), 1, D + 24);

        // 船底線。平行部では基線(0)、船首材の走りのあいだは甲板まで上がり、
        // 船尾の絞りのあいだは船尾の立ち上がりまで上がる。
        public int BottomY(int z)
        {
            int dk = DeckY(z);
            if (_stemRun > 0)
            {
                int z0 = (L - 1) - _stemRun;
                if (z > z0) return Clamp((int)Math.Round(dk * (double)(z - z0) / _stemRun), 0, dk);
            }
            int run = (int)Math.Round(_run);
            if (_sternRise > 0 && run > 0 && z < run)
                return Clamp((int)Math.Round(_sternRise * (double)(run - z) / run), 0, dk);
            return 0;
        }

        // 水線での半幅。船首テーパー・平行部・船尾の絞りの3区間。
        public double HalfBeamAt(int z)
        {
            double zf = (L - 1) - _entry;
            if (z >= zf)
            {
                double u = Math.Clamp((z - zf) / _entry, 0, 1);
                return _half * Math.Pow(1.0 - u, _bowExp);
            }
            if (z <= _run)
            {
                double u = Math.Clamp((_run - z) / _run, 0, 1);
                return _half * (_transom + (1.0 - _transom) * Math.Pow(1.0 - u, _sternExp));
            }
            return _half;
        }

        // station z・高さ y での半幅。喫水線までは超楕円の断面、
        // それより上はフレア（船首寄り）とタンブルホーム（中央〜船尾）で直線的に増減する。
        public double HalfAt(int z, int y)
        {
            int b = BottomY(z), dk = DeckY(z);
            if (y < b || y > dk) return 0;

            double hb = HalfBeamAt(z);
            int wl = Math.Min(WL, dk);
            if (wl <= b) return hb;   // 船首材・船尾の立ち上がりより上は舷側が直立する

            if (y <= wl)
            {
                double rise = (double)(wl - y) / (wl - b);   // 1=船底, 0=喫水線
                return hb * Math.Pow(1.0 - Math.Pow(Math.Clamp(rise, 0, 1), _k), 1.0 / _k);
            }

            double t = L <= 1 ? 1.0 : (double)z / (L - 1);
            double wf = Math.Clamp((t - 0.4) / 0.6, 0, 1);
            double slope = _flare * wf - _tumble * (1.0 - wf);
            return Math.Max(0.0, hb + (y - wl) * slope);
        }

        // 半幅をマスの範囲へ。半幅が0.5未満のところは竜骨の1列（幅が偶数なら2列）に落とす。
        public void Span(double hw, out int x0, out int x1)
        {
            if (hw < 0.5) { x0 = (B - 1) / 2; x1 = B / 2; return; }
            double c = (B - 1) / 2.0;
            x0 = (int)Math.Ceiling(c - hw);
            x1 = (int)Math.Floor(c + hw);
            if (x1 < x0) { x0 = (B - 1) / 2; x1 = B / 2; }
        }

        // canonical での外寸（幅・高さ）。フレアを付けると甲板が型幅より広がるので、
        // 幅は全 station を走査して実際の張り出しから取る。
        public (int W, int H) Bounds()
        {
            int xmin = int.MaxValue, xmax = int.MinValue, ymax = 0;
            for (int z = 0; z < L; z++)
            {
                int dk = DeckY(z);
                if (dk > ymax) ymax = dk;
                for (int y = BottomY(z); y <= dk; y++)
                {
                    Span(HalfAt(z, y), out int a, out int b);
                    if (a < xmin) xmin = a;
                    if (b > xmax) xmax = b;
                }
            }
            if (xmin > xmax) { xmin = 0; xmax = B - 1; }
            return (xmax - xmin + 1, ymax + Bulwark + KeelDepth + 1);
        }
    }
}
