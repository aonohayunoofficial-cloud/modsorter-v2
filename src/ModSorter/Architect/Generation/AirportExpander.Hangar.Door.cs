using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 扉 =====
    private static void HangarDoor(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int len, int doorH, int wallTop, string door)
    {
        // まぐさ。扉の上に 1 マスの帯を通し、その上を躯体で塞ぐ。
        Fill(cells, 0, len - 1, doorH + 1, doorH + 1, 0, 0, p.Mark);
        Fill(cells, 0, len - 1, doorH + 2, wallTop, 0, 0, p.Body);

        // 開口の両脇の柱型。
        Fill(cells, 0, 0, 1, doorH, 0, 0, p.Mark);
        Fill(cells, len - 1, len - 1, 1, doorH, 0, 0, p.Mark);

        if (door == "open") return;

        int mid = len / 2;
        for (int x = 1; x < len - 1; x++)
        {
            // 引き分け戸は中央から左右へ、折り戸は等間隔で建具の縦框が入る。
            bool stile = (door == "fold")
                ? (x % 5 == 0)
                : (x == mid || Math.Abs(x - mid) % 8 == 0);

            for (int y = 1; y <= doorH; y++)
            {
                // 上から 2 段目を扉の窓にする。実物も同じ位置に窓が並ぶ。
                bool win = (y == doorH - 1) && !stile && (x % 2 == 0);
                cells[(x, y, 0)] = stile ? p.Mark : (win ? p.Glass : p.Rail);
            }
        }

        // 通用口。扉の端に人の出入りする戸を空ける。
        for (int y = 1; y <= 3; y++)
            cells.Remove((2, y, 0));
    }

    // ===== 附属棟 =====
    // 側面に張り出す 2 層の別棟。工場・部品庫・事務所が入る。
    private static void HangarAnnex(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int len, int depth, int lastZ, int annex)
    {
        if (annex < 4) return;

        int ah = 9;
        int az0 = 2;
        int az1 = Math.Min(lastZ, az0 + Math.Max(8, depth - 6) - 1);

        Fill(cells, len, len + annex - 1, 0, 0, az0, az1, p.Pave);
        Fill(cells, len, len + annex - 1, 5, 5, az0, az1, p.Pave);

        for (int y = 1; y < ah; y++)
        {
            bool band = (y == 5);
            for (int z = az0; z <= az1; z++)
                cells[(len + annex - 1, y, z)] = band ? p.Mark
                    : ((z % 2 == 0) ? p.Body : p.Glass);
            for (int x = len; x < len + annex; x++)
            {
                cells[(x, y, az0)] = band ? p.Mark : p.Body;
                cells[(x, y, az1)] = band ? p.Mark : p.Body;
            }
        }

        Fill(cells, len, len + annex - 1, ah, ah, az0, az1, p.Roof);
        for (int x = len; x < len + annex; x++)
        {
            cells[(x, ah + 1, az0)] = p.Rail;
            cells[(x, ah + 1, az1)] = p.Rail;
        }
        for (int z = az0; z <= az1; z++)
            cells[(len + annex - 1, ah + 1, z)] = p.Rail;

        // 格納庫との連絡口。
        for (int y = 1; y <= 3; y++)
            for (int z = az0 + 2; z <= Math.Min(az0 + 4, az1 - 1); z++)
                cells.Remove((len - 1, y, z));
    }

    // 格納庫の屋根。"arch"（既定） | "flat" | "shed"。
    private static string RoofOf(string? s)
    {
        string v = (s ?? "arch").Trim().ToLowerInvariant();
        return (v == "flat" || v == "shed") ? v : "arch";
    }

    // 格納庫の扉。"slide"（既定） | "fold" | "open"。
    private static string DoorOf(string? s)
    {
        string v = (s ?? "slide").Trim().ToLowerInvariant();
        return (v == "fold" || v == "open") ? v : "slide";
    }
}
