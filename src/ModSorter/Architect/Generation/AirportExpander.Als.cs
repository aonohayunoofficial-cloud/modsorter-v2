using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 進入灯 =====
    // 平面土木なので滑走路と同じく実寸(m)を持ち、Scale で割ってマスへ落とす。
    // 進入端が z=0 で、そこから手前（z の増加方向）へ伸びる。最後に Rotate で向きを回す。
    //
    // 実寸（ICAO Annex 14 Vol.I 第5章）。
    //   CAT I      … センターライン 900m（間隔30m）＋クロスバー 150/300/450/600/750m。
    //                300m のクロスバーは長さ30m、他は外縁を結ぶ線が進入端の300m先で
    //                収束するよう調整する。0〜300m は1灯、300〜600m は2灯、
    //                600〜900m は3灯（灯数で距離が読めるようにするため）。
    //   CAT II/III … CAT I に加えて 270m まで伸びる赤の側方列（間隔30m）。
    //   簡易式     … 420m 以上（間隔60m・30mまで詰めてよい）＋300m に長さ18mか30mの
    //                クロスバー1本。
    //   バレット   … 簡易式で3m以上、他で4m以上。使うときクロスバーは CAT I で 300m の
    //                1本、CAT II/III で 150m と 300m の2本だけ。
    //
    // 900m は Scale=1 だと 900 マスになる。縮尺 5〜10 を選ぶ前提の小分類。
    private const double AlsCat1LenM = 900.0;   // CAT I・CAT II/III のセンターライン長
    private const double AlsSimpleLenM = 420.0; // 簡易式のセンターライン長
    private const double AlsSpacingM = 30.0;    // センターラインの間隔
    private const double AlsSimpleSpacingM = 60.0; // 簡易式の間隔
    private const double AlsCrossbar300M = 30.0; // 300m のクロスバーの長さ
    private const double AlsConvergeM = 300.0;   // 外縁を結ぶ線が収束する位置（進入端の先）
    private const double AlsSideRowM = 270.0;    // CAT II/III の側方列の長さ
    private const double AlsBarretteM = 4.0;     // バレットの長さ（簡易式は 3m）
    private const double PapiOffsetM = 300.0;    // PAPI の位置（進入端から）
    private const double PapiSideM = 15.0;       // PAPI の横距離（滑走路縁から）

    private static void BuildApproachLight(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        string type = AlsTypeOf(spec.AirportAlsType);
        bool barrette = spec.AirportAlsBarrette;
        int trestle = Clamp(spec.AirportAlsTrestle ?? 0, 0, 8);
        bool simple = (type == "simple");

        double lenM = simple ? AlsSimpleLenM : AlsCat1LenM;
        int len = Clamp(spec.Depth, 8, M((int)lenM, scale));   // 実際に描く長さ（マス）
        int rw = Odd(Clamp(spec.Width, 5, 63));                // 滑走路の幅（マス）
        int cx = rw / 2;                                        // 中心線の x

        double spacingM = simple ? AlsSimpleSpacingM : AlsSpacingM;
        int step = Math.Max(1, M(spacingM, scale));
        double barM = simple ? 3.0 : AlsBarretteM;
        int barHalf = barrette ? Math.Max(0, M(barM, scale) / 2) : 0;

        int ty = trestle;   // 灯火を載せる高さ

        // ===== 進入端の帯 =====
        // 滑走路の側を示す基準。ここから手前へ灯列が伸びる。
        Fill(cells, 0, rw - 1, 0, 0, 0, 0, p.Pave);
        Fill(cells, 0, rw - 1, ty + 1, ty + 1, 0, 0, p.Mark);

        // ===== センターライン =====
        for (int k = 1; k * step <= len; k++)
        {
            int z = k * step;
            double distM = z * scale;   // 進入端からの実寸

            // 灯数。0〜300m は1灯、300〜600m は2灯、600〜900m は3灯。
            // 簡易式は距離によらず1灯。
            int lamps = simple ? 1 : (distM <= 300.0 ? 1 : (distM <= 600.0 ? 2 : 3));
            int half = barrette ? barHalf : (lamps - 1);

            Trestle(cells, cx, z, ty, p);
            Fill(cells, cx - half, cx + half, ty + 1, ty + 1, z, z, p.Light);
        }

        // ===== クロスバー・側方列・PAPI =====
        AlsCrossbars(cells, p, type, simple, barrette, barHalf, cx, len, ty, scale);
        AlsSideRows(cells, p, type, cx, rw, len, step, ty, scale);
        AlsPapi(cells, p, spec.AirportPapi, cx, rw, len, scale);
    }

    // 進入灯橋の脚。trestle=0 なら地面に舗装だけ置く。
    private static void Trestle(
        Dictionary<(int x, int y, int z), string> cells, int x, int z, int ty, Palette p)
    {
        if (ty <= 0) { cells[(x, 0, z)] = p.Pave; return; }
        Fill(cells, x, x, 0, ty, z, z, p.Shoulder);
    }

    private static string AlsTypeOf(string? s)
    {
        string v = (s ?? "cat1").Trim().ToLowerInvariant();
        return (v == "cat2" || v == "simple") ? v : "cat1";
    }
}
