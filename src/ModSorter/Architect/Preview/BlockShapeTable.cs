namespace ModSorter.Architect.Preview;

// プレビュー描画用の「ブロックの見た目寸法」テーブル。
// 未登録のブロックは 1×1×1・オフセット0・向き非依存として扱う(呼び出し側の既定)。
// 寸法はブロック1マス=1.0 を基準とした割合。中心基準で描く前提。
//
// axisDependent=true のブロックは、軸(x/y/z)方向を「長辺」とし、
// 他2軸を痩せさせる。テーブルには基準の向き(軸=y想定)で寸法を書き、
// 実際の軸に合わせて呼び出し側で回転させる。
public static class BlockShapeTable
{
    public sealed class Shape
    {
        public double Sx, Sy, Sz;         // 寸法(1.0=1マス)
        public double Ox, Oy, Oz;         // 中心からのオフセット(通常0)
        public bool AxisDependent;        // 軸で長辺方向が変わるか(shaft/cog等)
    }

    // baseId(状態[...]を除いたID) → 形状。
    private static readonly Dictionary<string, Shape> Table =
        new(StringComparer.Ordinal)
        {
            // --- Create: 痩せ系(軸方向に細い棒) ---
            // shaft は軸方向に1.0、他2軸を0.3の細い棒。
            ["create:shaft"] = new()
            {
                Sx = 0.3,
                Sy = 1.0,
                Sz = 0.3,
                AxisDependent = true
            },
            // gantry_shaft も細い棒として扱う(shaftと同形)。
            ["create:gantry_shaft"] = new()
            {
                Sx = 0.35,
                Sy = 1.0,
                Sz = 0.35,
                AxisDependent = true
            },
            // cogwheel: 軸に垂直な円盤。軸方向は薄く(0.25)、他2軸は1マスいっぱい。
            ["create:cogwheel"] = new()
            {
                Sx = 1.0,
                Sy = 0.25,
                Sz = 1.0,
                AxisDependent = true
            },
            // --- Create: はみ出し系(1マスより大きい) ---
            // large_cogwheel: 軸に垂直な大きい円盤。軸方向は薄く、他2軸を2.0に広げる。
            ["create:large_cogwheel"] = new()
            {
                Sx = 2.0,
                Sy = 0.35,
                Sz = 2.0,
                AxisDependent = true
            },
        };

    public static Shape? Get(string baseId)
        => Table.TryGetValue(baseId, out var s) ? s : null;

    // 軸(x/y/z)に合わせて、基準寸法(軸=y想定)を回転させた実寸法を返す。
    // axisDependent でないブロックはそのまま返す。
    // axis が null(未解決/非依存)なら基準寸法のまま。
    public static (double sx, double sy, double sz) Resolve(Shape s, string? axis)
    {
        if (!s.AxisDependent || axis == null)
            return (s.Sx, s.Sy, s.Sz);

        // 基準は「長辺 or 円盤の軸」が y。実軸に合わせて入れ替える。
        // y基準の (Sx,Sy,Sz) を、軸がxなら x が長辺方向、zなら z が長辺方向へ。
        return axis switch
        {
            "x" => (s.Sy, s.Sx, s.Sz),  // y↔x 入れ替え
            "z" => (s.Sx, s.Sz, s.Sy),  // y↔z 入れ替え
            _ => (s.Sx, s.Sy, s.Sz),  // y はそのまま
        };
    }
}
