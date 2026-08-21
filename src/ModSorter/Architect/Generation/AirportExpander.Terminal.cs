using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 旅客ターミナル =====
    // 平面土木ではないが "airport:" 配下なのでここで作る。縮尺は持たず 1マス=1m。
    //
    // 実寸の出典。
    //   ゲート1つあたりの桁行き … 平均 33〜40m（FAA AC 150/5360-13）。ピア全長は 210〜300m。
    //                            エプロンのスポット幅と同じ寸法系なので同じ値を受け取る。
    //   建物の奥行き   … ダブルローデッドの動線幅 30ft（約9m・動く歩道なし）＋
    //                    ラウンジ奥行き 25〜30ft（8〜9m）。片側ピアで 26〜30m。
    //   階構成         … 出発が上階・到着が下階の2層が基本。
    //   搭乗橋         … アプロンドライブ式。伸長 15〜45m、ロタンダ高さ 5m 級（最大8m）、
    //                    トンネルの勾配は 10% 以下。
    //
    // StructureSpec との対応。桁行きは width ではなくゲート数×間隔の従属値。
    //   depth=建物の奥行き（エプロン側が z=0）
    //   airport_gates・airport_gate_spacing … ゲート数と1ゲートあたりの桁行き
    //   airport_levels・airport_level_height … 階数と階高
    //   airport_bridge … 搭乗橋の伸長 / airport_canopy … 車寄せの庇
    //   airport_terminal_roof … "flat" | "vault"
    //   tower_block=躯体 / glazing_block=カーテンウォール / accent_block=方立・腰壁
    //   floor_block=床・搭乗橋の床 / roof_block=屋根 / parapet_block=パラペット
    //   seat_block=天井の照明
    //
    // 頭打ちすると端のゲートだけ切れて左右非対称になるので、収まらないときは
    // 幅を切らずにゲート数を減らす。
    // ゲート中心は i*pitch + pitch/2 で、エプロンのスポット中心と同じ式にしてある。
    // 断面は「エプロン側が z=0」で組み、最後に Rotate で向きを回す。
    private const int TerminalMaxLen = 256; // 桁行きの上限（マス）。超える分はゲート数を減らす

    private static void BuildTerminal(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int pitch = Odd(Clamp(spec.AirportGateSpacing ?? 45, 9, 96));
        int gates = Clamp(spec.AirportGates ?? 3, 1, 8);
        while (gates > 1 && gates * pitch > TerminalMaxLen) gates--;

        int len = gates * pitch;                              // 桁行き（x）
        int depth = Clamp(spec.Depth, 10, 48);                // 奥行き（z）。z=0 がエプロン側
        int levels = Clamp(spec.AirportLevels ?? 2, 1, 4);
        int lh = Clamp(spec.AirportLevelHeight ?? 6, 4, 8);   // 階高。搭乗橋の高さもこれに従う
        int bridge = Clamp(spec.AirportBridge ?? 15, 0, 48);
        int canopy = Clamp(spec.AirportCanopy ?? 6, 0, 16);
        bool vault = string.Equals(
            (spec.AirportTerminalRoof ?? "flat").Trim(), "vault", StringComparison.OrdinalIgnoreCase);

        int top = levels * lh;   // 最上階の天井＝屋根の高さ

        // ===== 床 =====
        Fill(cells, 0, len - 1, 0, 0, 0, depth - 1, p.Pave);
        for (int i = 1; i < levels; i++)
            Fill(cells, 1, len - 2, i * lh, i * lh, 1, depth - 2, p.Pave);

        // ===== カーテンウォール（エプロン側と道路側）=====
        // 各階の床レベルは腰壁、3マスごとに方立、それ以外はガラス。
        for (int y = 1; y < top; y++)
        {
            bool band = (y % lh == 0) || y == 1;
            for (int x = 0; x < len; x++)
            {
                string b = band ? p.Mark : ((x % 3 == 0) ? p.Body : p.Glass);
                cells[(x, y, 0)] = b;
                cells[(x, y, depth - 1)] = b;
            }
        }

        // ===== 妻側の壁 =====
        for (int y = 1; y < top; y++)
            for (int z = 0; z < depth; z++)
            {
                cells[(0, y, z)] = p.Body;
                cells[(len - 1, y, z)] = p.Body;
            }

        // ===== 内部の柱 =====
        int colZ = (depth - 1) / 2;
        for (int x = 8; x < len - 1; x += 9)
            for (int y = 1; y < top; y++)
                cells[(x, y, colZ)] = p.Body;

        // ===== 天井の照明 =====
        for (int i = 0; i < levels; i++)
        {
            int y = (i + 1) * lh - 1;
            for (int x = 4; x < len - 1; x += 8)
                for (int z = 4; z < depth - 1; z += 8)
                    cells[(x, y, z)] = p.Light;
        }

        // ===== 屋根 =====
        if (!vault)
        {
            Fill(cells, 0, len - 1, top, top, 0, depth - 1, p.Roof);
            for (int x = 0; x < len; x++)
            {
                cells[(x, top + 1, 0)] = p.Rail;
                cells[(x, top + 1, depth - 1)] = p.Rail;
            }
            for (int z = 0; z < depth; z++)
            {
                cells[(0, top + 1, z)] = p.Rail;
                cells[(len - 1, top + 1, z)] = p.Rail;
            }
        }
        else
        {
            // かまぼこ屋根。奥行き方向に半円弧を張る。
            double rise = Math.Max(2.0, depth / 4.0);
            var h = new int[depth];
            for (int z = 0; z < depth; z++)
                h[z] = (int)Math.Round(rise * Math.Sin(Math.PI * (z + 0.5) / depth));

            // 妻壁を弧の下まで立ち上げる。
            for (int z = 0; z < depth; z++)
            {
                Fill(cells, 0, 0, top, top + h[z], z, z, p.Body);
                Fill(cells, len - 1, len - 1, top, top + h[z], z, z, p.Body);
            }

            // 弧の面。隣との段差ぶんだけ下へ伸ばして穴を塞ぐ。
            for (int z = 0; z < depth; z++)
            {
                int prev = z > 0 ? h[z - 1] : 0;
                int next = z < depth - 1 ? h[z + 1] : 0;
                int lo = top + Math.Min(h[z], Math.Min(prev, next));
                Fill(cells, 0, len - 1, lo, top + h[z], z, z, p.Roof);
            }
        }

        // ===== 車寄せの庇 =====
        if (canopy > 0)
        {
            int cy = lh;
            Fill(cells, 0, len - 1, cy, cy, depth, depth + canopy - 1, p.Roof);
            for (int x = 4; x < len; x += 8)
                Fill(cells, x, x, 1, cy - 1, depth + canopy - 1, depth + canopy - 1, p.Body);
        }

        // ===== 出入口・搭乗橋 =====
        TerminalGates(cells, p, gates, pitch, depth, levels, lh, bridge);
    }
}
