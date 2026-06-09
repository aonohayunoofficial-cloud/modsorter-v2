using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// PrimitiveSpec を確定的にボクセル化する。
// 楕円体: (x/rx)^2 + (y/ry)^2 + (z/rz)^2 <= 1 を満たすセルを埋める。
// 中空: 内側の薄い層(しきい値)を除外して殻だけ残す。
public static class PrimitiveExpander
{
    public static List<GeneratedBlock> Expand(PrimitiveSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // 半径の健全化（1〜32）。大きすぎるとブロック数が爆発するため抑える。
        int rx = Clamp(spec.RadiusX, 1, 32);
        int ry = Clamp(spec.RadiusY, 1, 32);
        int rz = Clamp(spec.RadiusZ, 1, 32);

        // sphere は3軸を最小半径に揃える（球らしさを保証）
        string shape = (spec.Shape ?? "ellipsoid").Trim().ToLowerInvariant();
        if (shape == "sphere")
        {
            int r = Math.Min(rx, Math.Min(ry, rz));
            rx = ry = rz = r;
        }

        string fallback = allowedBlocks.Count > 0 ? allowedBlocks[0] : "minecraft:stone";
        string block = Pick(spec.Block, allowedBlocks, fallback);

        var cells = new List<GeneratedBlock>();

        // 原点(0,0,0)を中心に、-r..+r を走査。出力座標は 0 始まりへシフトする。
        for (int x = -rx; x <= rx; x++)
            for (int y = -ry; y <= ry; y++)
                for (int z = -rz; z <= rz; z++)
                {
                    double v = SqNorm(x, rx, y, ry, z, rz);
                    if (v > 1.0) continue; // 楕円体の外側

                    if (spec.Hollow)
                    {
                        // 殻だけ残す: 隣接6方向のいずれかが外側なら表面とみなす
                        bool surface =
                            SqNorm(x + 1, rx, y, ry, z, rz) > 1.0 ||
                            SqNorm(x - 1, rx, y, ry, z, rz) > 1.0 ||
                            SqNorm(x, rx, y + 1, ry, z, rz) > 1.0 ||
                            SqNorm(x, rx, y - 1, ry, z, rz) > 1.0 ||
                            SqNorm(x, rx, y, ry, z + 1, rz) > 1.0 ||
                            SqNorm(x, rx, y, ry, z, rz - 1) > 1.0;
                        if (!surface) continue;
                    }

                    cells.Add(new GeneratedBlock
                    {
                        X = x + rx, // 0始まりへシフト
                        Y = y + ry,
                        Z = z + rz,
                        Id = block
                    });
                }

        return cells
            .OrderBy(b => b.Y).ThenBy(b => b.Z).ThenBy(b => b.X)
            .ToList();
    }

    // (x/rx)^2 + (y/ry)^2 + (z/rz)^2
    private static double SqNorm(int x, int rx, int y, int ry, int z, int rz)
    {
        // 半径0除算を避ける（健全化で1以上だが念のため）
        double fx = rx == 0 ? 0 : (double)x / rx;
        double fy = ry == 0 ? 0 : (double)y / ry;
        double fz = rz == 0 ? 0 : (double)z / rz;
        return fx * fx + fy * fy + fz * fz;
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
