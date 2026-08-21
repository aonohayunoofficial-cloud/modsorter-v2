using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 滑走路 =====
    private static void BuildRunway(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        int w = Clamp(spec.Width, 6, 64);        // 幅（マス）
        int len = Clamp(spec.Depth, 8, 64);      // 延長（マス）
        double widthM = w * scale;               // 幅の実寸。標識の本数はこれで決まる

        int shoulder = Clamp(spec.AirportShoulder ?? M0(RunwayShoulderM, scale), 0, 16);

        // ショルダー。舗装の外側へ左右に張り出す（負座標は Normalize で寄る）。
        if (shoulder > 0)
        {
            Fill(cells, -shoulder, -1, 0, 0, 0, len - 1, p.Shoulder);
            Fill(cells, w, w + shoulder - 1, 0, 0, 0, len - 1, p.Shoulder);
        }

        // 舗装面。
        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        int cx = (w - 1) / 2;
        int cw = M(CenterWidthM, scale);         // 中心線標識の幅
        int cx0 = cx - (cw - 1) / 2;
        int cx1 = cx0 + cw - 1;

        if (spec.AirportMarking)
        {
            // ===== 中心線標識 =====
            // 実線30m＋間隔20mの破線。周期に 0 を指定すると実線になる。
            double periodM = spec.AirportCenterStep ?? (CenterOnM + CenterOffM);
            if (periodM <= 0)
            {
                Fill(cells, cx0, cx1, 0, 0, 0, len - 1, p.Mark);
            }
            else
            {
                int period = M(periodM, scale);
                int on = Math.Max(1, (int)Math.Round(period * CenterOnM / (CenterOnM + CenterOffM)));
                for (int z = 0; z < len; z++)
                    if (z % period < on) Fill(cells, cx0, cx1, 0, 0, z, z, p.Mark);
            }

            // ===== 進入端標識 =====
            // 本数は幅から決まる（45m なら 12 本）。指定があればそれを優先する。
            int stripes = Clamp(spec.AirportThreshold ?? ThresholdStripes(widthM), 0, 20);
            if (stripes >= 2)
            {
                int half = stripes / 2;
                int sw = M(StripeWidthM, scale);              // 縞の幅
                int z0 = M0(ThresholdOffsetM, scale);         // 進入端からの空き
                int z1 = Math.Min(len - 1, z0 + M(ThresholdStripeLenM, scale) - 1);

                // 片側に使える幅。中心線標識の外側から、舗装縁の 3m 手前まで。
                int inner = cx1 + 1;
                int outer = w - 1 - M0(EdgeClearM, scale);
                int span = outer - inner + 1;

                if (z1 >= z0 && span >= sw)
                {
                    // 最外の縞の外端がちょうど outer に来るよう等間隔に割る。
                    // 縞の幅は実寸を守り、間隔で辻褄を合わせる（縁からの空きが実物どおりになる）。
                    double pitch = (half <= 1) ? 0 : (double)(span - sw) / (half - 1);
                    for (int i = 0; i < half; i++)
                    {
                        int a = inner + (int)Math.Round(i * pitch);
                        int b = Math.Min(outer, a + sw - 1);
                        if (a > outer) break;

                        // 中心線を挟んで対称に置く。
                        Fill(cells, a, b, 0, 0, z0, z1, p.Mark);
                        Fill(cells, Math.Max(0, cx - (b - cx)), Math.Max(0, cx - (a - cx)),
                             0, 0, z0, z1, p.Mark);
                    }
                }
            }

            // ===== 着陸目標点標識 =====
            // 進入端から 400m。延長が足りなければ描かれない（実寸どおりの判定）。
            int aimZ = M0(AimPointM, scale);
            int aimLen = M(AimLenM, scale);
            bool hasAim = aimZ + aimLen <= len;
            if (hasAim)
                PairBand(cells, cx, w, SideOffsetM, AimWidthM, aimZ, aimZ + aimLen - 1, scale, p.Mark);

            // ===== 接地帯標識 =====
            // 進入端から 150m ごとの対。着陸目標点と重なる組は置かない。
            int tdzMax = Clamp(spec.AirportTouchdown ?? 6, 0, 8);
            int tdzLen = M(TdzLenM, scale);
            for (int i = 0; i < tdzMax; i++)
            {
                int tz = M0(TdzFirstM + i * TdzStepM, scale);
                if (tz + tdzLen > len) break;
                if (hasAim && tz < aimZ + aimLen && tz + tdzLen > aimZ) continue;
                PairBand(cells, cx, w, SideOffsetM, TdzWidthM, tz, tz + tdzLen - 1, scale, p.Mark);
            }
        }

        // ===== 滑走路縁灯 =====
        EdgeLights(cells, p, w, len, spec.AirportEdgeLight ?? (int)EdgeLightM, scale);
    }
}
