using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 船首の向きの回転と、負座標の正規化。HullExpander.cs が12,628バイトになったので分けた。
//
// canonical は船首が +z（南）。写像は IndustryExpander と同じ (x,z)→(-z,x) で、
// west が1手・north が2手・east が3手。寝た丸太の axis と建材の facing は
// 回転に追従させる（RotateAxis は HullExpander.Rig.cs にある）。
public static partial class HullExpander
{
    private static int Face(string? face) => (face ?? "south").Trim().ToLowerInvariant() switch
    {
        "west" => 1,
        "north" => 2,
        "east" => 3,
        _ => 0,
    };

    private static void Rotate(
        ref Dictionary<(int x, int y, int z), string> cells, ref Props props, int turns)
    {
        int t = turns & 3;
        if (t == 0) return;

        var rc = new Dictionary<(int x, int y, int z), string>();
        var rp = new Props();

        foreach (var kv in cells)
        {
            int x = kv.Key.x, z = kv.Key.z;
            for (int i = 0; i < t; i++)
            {
                int nx = -z;
                int nz = x;
                x = nx;
                z = nz;
            }

            var key = (x, kv.Key.y, z);
            rc[key] = kv.Value;

            if (!props.TryGetValue(kv.Key, out var src)) continue;

            var dst = new Dictionary<string, string>(src);
            if (dst.TryGetValue("facing", out var fc)) dst["facing"] = RotateFacing(fc, t);
            if (dst.TryGetValue("axis", out var ax)) dst["axis"] = RotateAxis(ax, t);
            rp[key] = dst;
        }

        cells = rc;
        props = rp;
    }

    private static string RotateFacing(string face, int turns)
    {
        string[] cycle = { "east", "south", "west", "north" };
        int i = Array.IndexOf(cycle, face);
        if (i < 0) return face;
        return cycle[(i + (turns & 3)) % 4];
    }

    // 竜骨の張り出しで y が負になるので、ここで 0 起点へ寄せる。
    // StructureNbtWriter.Save は負座標を扱えない。
    private static List<GeneratedBlock> Normalize(
        Dictionary<(int x, int y, int z), string> cells, Props props)
    {
        int minX = 0, minY = 0, minZ = 0;
        foreach (var k in cells.Keys)
        {
            if (k.x < minX) minX = k.x;
            if (k.y < minY) minY = k.y;
            if (k.z < minZ) minZ = k.z;
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x - minX,
                Y = kv.Key.y - minY,
                Z = kv.Key.z - minZ,
                Id = kv.Value,
                Properties = props.TryGetValue(kv.Key, out var pr) && pr.Count > 0 ? pr : null
            })
            .ToList();
    }
}
