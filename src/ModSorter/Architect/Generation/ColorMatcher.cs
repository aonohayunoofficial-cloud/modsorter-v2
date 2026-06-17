using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation
{
    // RGB色をカタログブロックの代表色に近づけるためのマッチャ。
    // 各候補ブロックの色をCIELABに変換しておき、入力色との距離が最小のIDを返す。
    public sealed class ColorMatcher
    {
        // 明度(L)の重み。TRELLIS.2のテクスチャは陰影が焼き込まれており、
        // 同じ素材でも光の当たり方でLが大きくぶれる。Lの重みを下げることで
        // 「陰になって暗いだけの白壁」が黒系ブロックに化けるのを抑える。
        // 1.0で従来通り(均等)。小さいほど明暗の影響を無視して色相・彩度重視。
        // 仮値0.3。暗部が黒に寄りすぎるなら更に下げる(0.2)、
        // のっぺりして立体感が消えるなら上げる(0.5)。
        private const double WeightL = 0.3;
        private const double WeightA = 1.0;
        private const double WeightB = 1.0;

        private readonly List<(string Id, double L, double A, double B)> _cands = new();

        public ColorMatcher(IEnumerable<ModSorter.Architect.BlockCatalogItem> items)
        {
            foreach (var item in items)
            {
                var c = item.Color;
                if (c == null || c.Length < 3) continue;
                var (l, a, b) = RgbToLab(c[0], c[1], c[2]);
                _cands.Add((item.Id, l, a, b));
            }
        }

        public bool HasCandidates => _cands.Count > 0;

        // 診断用: 実際に色マッチに使える(代表色を持つ)候補ブロックの数。
        public int CandidateCount => _cands.Count;


        public string Nearest(int r, int g, int b, string fallback)
        {
            if (_cands.Count == 0) return fallback;

            var (l, a, bb) = RgbToLab(r, g, b);

            string best = fallback;
            double bestDist = double.MaxValue;
            foreach (var cand in _cands)
            {
                double dl = l - cand.L;
                double da = a - cand.A;
                double db = bb - cand.B;
                // 明度重み付きユークリッド距離の平方(ΔE76の重み付き概算)。
                double dist = WeightL * dl * dl + WeightA * da * da + WeightB * db * db;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = cand.Id;
                }
            }
            return best;
        }

        // ---- 色変換 (sRGB 0-255 -> CIELAB, D65) ----
        private static (double L, double A, double B) RgbToLab(int r, int g, int b)
        {
            double rl = SrgbToLinear(r / 255.0);
            double gl = SrgbToLinear(g / 255.0);
            double bl = SrgbToLinear(b / 255.0);

            double x = rl * 0.4124 + gl * 0.3576 + bl * 0.1805;
            double y = rl * 0.2126 + gl * 0.7152 + bl * 0.0722;
            double z = rl * 0.0193 + gl * 0.1192 + bl * 0.9505;

            x /= 0.95047;
            y /= 1.00000;
            z /= 1.08883;

            double fx = LabF(x);
            double fy = LabF(y);
            double fz = LabF(z);

            double L = 116.0 * fy - 16.0;
            double A = 500.0 * (fx - fy);
            double B = 200.0 * (fy - fz);
            return (L, A, B);
        }

        private static double SrgbToLinear(double c)
            => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static double LabF(double t)
            => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;
    }
}