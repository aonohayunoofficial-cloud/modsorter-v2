using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== エプロン =====
    // スポット（駐機場）1 つの寸法は「翼幅＋両側のクリアランス」で決まる。
    // クリアランスは Annex 14 でコード A/B が 3.0m、C が 4.5m、D/E/F が 7.5m。
    // A320（翼幅 35.8m）で約 45m、B777（翼幅 64.8m）で約 80m。
    // ここでは幅を入力として受けず、UI が機体サイズから決めた 1 スポットぶんの幅を
    // airport_spot_width で受け取り、スポット数ぶん横に並べる。全幅の頭打ちはしない。
    // 頭打ちすると端のスポットだけ切れて左右非対称になるため。
    // スポット幅は奇数に丸め、リードインラインを厳密な中央に載せる。
    // 区画線は各スポットが自分の枠の両端に引くので、並べても対称のまま。
    private static void BuildApron(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int spots = Clamp(spec.AirportSpots ?? 3, 1, 12);        // スポット数
        int sw = Clamp(spec.AirportSpotWidth ?? 45, 5, 96);      // 1 スポットの幅
        if (sw % 2 == 0) sw++;                                   // 中央 1 マスを確保
        int lane = Clamp(spec.AirportShoulder ?? 0, 0, 32);      // 走行路（タキシレーン）
        int len = Clamp(spec.Depth, 6, 192);                     // 駐機区画＋走行路
        bool marking = spec.AirportMarking;

        int w = spots * sw;                                      // 全幅は従属値
        int stand = Math.Max(4, len - lane);                     // 駐機区画の奥行き
        int total = stand + lane;

        // 舗装面。駐機区画と走行路をまとめて 1 面で敷く。
        Fill(cells, 0, w - 1, 0, 0, 0, total - 1, p.Pave);

        if (!marking) return;

        int barHalf = Math.Max(1, sw / 6);                       // ストップマークの半幅
        int stopZ = Math.Max(2, stand / 5);                      // 機首の停止位置

        for (int i = 0; i < spots; i++)
        {
            int x0 = i * sw;
            int x1 = x0 + sw - 1;
            int cx = x0 + sw / 2;                                // 奇数幅なので厳密な中央

            // 区画線。自分の枠の両端に引くので、隣り合うスポットの境界は 2 本線になる。
            Fill(cells, x0, x0, 0, 0, 0, stand - 1, p.Line);
            Fill(cells, x1, x1, 0, 0, 0, stand - 1, p.Line);

            // リードインライン。走行路側から停止位置まで。
            Fill(cells, cx, cx, 0, 0, stopZ, stand - 1, p.Mark);

            // ストップマーク。機首の停止位置を示す横棒。中心から左右等幅。
            Fill(cells, cx - barHalf, cx + barHalf, 0, 0, stopZ, stopZ, p.Mark);
        }

        // 走行路の中心線。駐機区画の奥を横切る。
        if (lane > 0)
        {
            int lz = stand + (lane - 1) / 2;
            Fill(cells, 0, w - 1, 0, 0, lz, lz, p.Mark);
        }
    }
}
