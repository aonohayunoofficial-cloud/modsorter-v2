using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ModSorter.Architect.Generation;

// ブロックのテクスチャPNGから平均色(代表色)を算出する。
// 計算結果は static 辞書にキャッシュし、アプリ起動中は使い回す。
// 指定されたブロックだけ遅延計算するので、MODが大量でも実際に使う数しか計算しない。
public static class BlockColorSampler
{
    // blockId → 平均色[r,g,b]。null は「計算したが取得不能」を表す negative cache。
    private static readonly Dictionary<string, int[]?> _cache = new();
    private static readonly object _lock = new();

    // 指定ブロックの平均色を返す。取得できなければ null。
    // tp: 既に開いている BlockTextureProvider を渡す(jar探索を再利用するため)。
    public static int[]? GetAverageColor(BlockTextureProvider tp, string blockId)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(blockId, out var cached))
                return cached; // ヒット(null=取得不能も含めてキャッシュ済み)
        }

        int[]? color = Compute(tp, blockId);

        lock (_lock)
        {
            _cache[blockId] = color;
        }
        return color;
    }

    private static int[]? Compute(BlockTextureProvider tp, string blockId)
    {
        try
        {
            byte[]? png = tp.GetTexture(blockId);
            if (png == null || png.Length == 0) return null;

            using var img = Image.Load<Rgba32>(png);

            long sr = 0, sg = 0, sb = 0;
            long count = 0;

            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var p = row[x];
                        // 透明(ガラスの抜けや葉の隙間など)は平均から除外。
                        if (p.A < 16) continue;
                        sr += p.R; sg += p.G; sb += p.B;
                        count++;
                    }
                }
            });

            if (count == 0) return null; // 全部透明など

            return new int[]
            {
                (int)(sr / count),
                (int)(sg / count),
                (int)(sb / count)
            };
        }
        catch
        {
            return null;
        }
    }
}
