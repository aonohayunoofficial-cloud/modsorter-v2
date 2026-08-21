using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== トラックドック（ランドサイド）とドック上屋 =====
    private static void CargoDocks(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int docks, int pitch, int len, int depth, int lastZ, int roofY, int canopy)
    {
        for (int i = 0; i < docks; i++)
        {
            int cx = i * pitch + pitch / 2;
            int x0 = Math.Max(1, cx - 1);
            int x1 = Math.Min(len - 2, cx + 1);

            Fill(cells, x0, x1, 2, 4, lastZ, lastZ, p.Rail);   // シャッター
            Fill(cells, Math.Max(0, cx - 2), Math.Min(len - 1, cx + 2),
                 5, 5, lastZ, lastZ, p.Mark);                  // まぐさ

            // ドックバンパー。床と同じ高さに出す。
            cells[(x0, 1, depth)] = p.Mark;
            cells[(x1, 1, depth)] = p.Mark;
        }

        if (canopy > 0)
        {
            int cy = Math.Min(6, roofY - 1);
            Fill(cells, 0, len - 1, cy, cy, depth, depth + canopy - 1, p.Roof);
            for (int x = 4; x < len; x += 8)
                Fill(cells, x, x, 1, cy - 1, depth + canopy - 1, depth + canopy - 1, p.Body);
        }
    }

    // ===== エアサイドの大型扉 =====
    private static void CargoAirsideDoors(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int doors, int doorW, int len, int doorH)
    {
        for (int j = 0; j < doors; j++)
        {
            int cx = (2 * j + 1) * len / (2 * doors);
            int x0 = Math.Max(1, cx - doorW / 2);
            int x1 = Math.Min(len - 2, cx + doorW / 2);

            Fill(cells, x0, x1, 2, doorH + 1, 0, 0, p.Rail);
            Fill(cells, Math.Max(0, x0 - 1), Math.Min(len - 1, x1 + 1),
                 doorH + 2, doorH + 2, 0, 0, p.Mark);

            // エプロン側の取付け。床との段差 1m をここで摺り付ける。
            Fill(cells, x0, x1, 0, 0, -4, -1, p.Pave);
            Fill(cells, x0, x1, 1, 1, -1, -1, p.Pave);
        }
    }

    // ===== 事務所棟 =====
    // 倉庫の妻側に付く2層の別棟。階高4、奥行きは倉庫に合わせて最大16。
    private static void CargoOffice(
        Dictionary<(int x, int y, int z), string> cells, Palette p, int office, int depth)
    {
        if (office < 6) return;

        int ow = office;
        int od = Math.Min(depth, 16);
        int oh = 9;

        Fill(cells, -ow, -1, 0, 1, 0, od - 1, p.Pave);
        Fill(cells, -ow, -1, 5, 5, 0, od - 1, p.Pave);

        for (int y = 2; y < oh; y++)
        {
            bool band = (y == 5);
            for (int x = -ow; x <= -1; x++)
            {
                string b = band ? p.Mark : ((x % 2 == 0) ? p.Body : p.Glass);
                cells[(x, y, 0)] = b;
                cells[(x, y, od - 1)] = b;
            }
            for (int z = 0; z < od; z++)
                cells[(-ow, y, z)] = band ? p.Mark : ((z % 2 == 0) ? p.Body : p.Glass);
        }

        Fill(cells, -ow, -1, oh, oh, 0, od - 1, p.Roof);
        for (int z = 0; z < od; z++) cells[(-ow, oh + 1, z)] = p.Rail;
        for (int x = -ow; x <= -1; x++)
        {
            cells[(x, oh + 1, 0)] = p.Rail;
            cells[(x, oh + 1, od - 1)] = p.Rail;
        }

        // 道路側の出入口。
        for (int x = -ow / 2 - 1; x <= -ow / 2 + 1; x++)
            for (int y = 2; y <= 4; y++)
                cells.Remove((x, y, od - 1));

        // 倉庫との連絡口。
        for (int y = 2; y <= 4; y++)
            for (int z = 2; z <= 4; z++)
                cells.Remove((0, y, z));
    }
}
