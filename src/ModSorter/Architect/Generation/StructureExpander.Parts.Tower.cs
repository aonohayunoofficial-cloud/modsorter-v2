using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 付加部品のうち塔の本体。StructureExpander の partial。
// 配置の決定(TowerSpots)と頂部(BuildTowerCap)は StructureExpander.Parts.Tower.Cap.cs。
public static partial class StructureExpander
{
    // 塔（鐘塔・尖塔・ミナレット）: 建物の平面内に正方形の塔を立て、屋根より上へ突き出す。
    // 四周の壁を y=1 から塔の上端(topY = h-1+tower_height)まで塞ぎ、内側は抜いて吹き抜けにする。
    // 内側を抜くと下の屋根面に穴が空くが、四周の壁と頂部で覆われるので外から内部は見えない。
    // 位置は tower_align、頂部の形は tower_roof で決める。
    // 塔は開口部の適用より後に作られるため、正面中央に塔を置くと壁に開けたドア・大開口が
    // 塔の壁で塞がれる。正面側の外周に接する塔には足元の入口をここで開け直す。
    private static void BuildTower(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, StructureSpec spec,
        int w, int d, int h, string tower, string roof)
    {
        int s = Clamp(spec.TowerWidth ?? 0, 0, Math.Min(w, d));
        int th = Clamp(spec.TowerHeight ?? 0, 0, 32);
        if (s < 3 || th < 1) return;

        string align = (spec.TowerAlign ?? "front").Trim().ToLowerInvariant();
        string cap = (spec.TowerRoof ?? "spire").Trim().ToLowerInvariant();
        string face = (spec.FacadeFace ?? "south").Trim().ToLowerInvariant();
        if (face != "north" && face != "south" && face != "east" && face != "west")
            face = "south";

        int topY = h - 1 + th;               // 塔の壁の上端
        int xMax = Math.Max(0, w - s), zMax = Math.Max(0, d - s);

        // 塔の左手前角(x0,z0)を align から作る。正面は facade_face で決まる。
        foreach (var (rx0, rz0) in TowerSpots(align, face, w, d, s))
        {
            int x0 = Clamp(rx0, 0, xMax);
            int z0 = Clamp(rz0, 0, zMax);
            int x1 = x0 + s - 1, z1 = z0 + s - 1;

            // 平面マスクから外れる位置（L字の欠けの上など）には立てない。宙抜けを防ぐ。
            bool inMask = true;
            for (int x = x0; x <= x1 && inMask; x++)
                for (int z = z0; z <= z1; z++)
                    if (!foot.Contains((x, z))) { inMask = false; break; }
            if (!inMask) continue;

            // 内側は吹き抜け。屋根・中間床・パラペットが塔の中に残ると頂部と二重になるので抜く。
            for (int x = x0 + 1; x <= x1 - 1; x++)
                for (int z = z0 + 1; z <= z1 - 1; z++)
                    for (int y = 1; y <= topY; y++)
                        cells.Remove((x, y, z));

            // 四周の壁。y=1 から上端まで塞ぐ。
            for (int y = 1; y <= topY; y++)
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (x != x0 && x != x1 && z != z0 && z != z1) continue;
                        cells[(x, y, z)] = tower;
                    }

            // 鐘楼の開口。上端の2段だけ四面の中央を抜く。建物の壁の高さより下は抜かない。
            if (spec.TowerBelfry && th >= 4)
            {
                int bm = s / 2;
                for (int y = topY - 2; y <= topY - 1; y++)
                {
                    if (y <= h - 1) continue;
                    cells.Remove((x0 + bm, y, z0));
                    cells.Remove((x0 + bm, y, z1));
                    cells.Remove((x0, y, z0 + bm));
                    cells.Remove((x1, y, z0 + bm));
                }
            }

            // 足元の入口。塔が正面側の外周に接しているときだけ、その面の中央を抜く。
            bool touchFront =
                (face == "south" && z1 == d - 1) ||
                (face == "north" && z0 == 0) ||
                (face == "east" && x1 == w - 1) ||
                (face == "west" && x0 == 0);
            if (touchFront)
            {
                int doorW = s >= 5 ? 3 : 1;
                int doorH = Clamp(h - 2, 2, 4);
                int mx = x0 + s / 2, mz = z0 + s / 2;
                for (int y = 1; y <= doorH; y++)
                    for (int o = -(doorW / 2); o <= doorW / 2; o++)
                    {
                        if (face == "south") cells.Remove((mx + o, y, z1));
                        else if (face == "north") cells.Remove((mx + o, y, z0));
                        else if (face == "east") cells.Remove((x1, y, mz + o));
                        else cells.Remove((x0, y, mz + o));
                    }
            }

            BuildTowerCap(cells, x0, z0, s, topY, cap, roof);
        }
    }
}
