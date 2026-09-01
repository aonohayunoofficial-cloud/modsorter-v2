using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 開放艇の内部。床板（フロアボード）と漕ぎ座（スオート）。
//
// 実物の根拠（イギリス海軍の 32ft カッター）:
//   全長32ft＝9.75m・型幅9ft＝2.74m（1942年ブライトリングシー建造の "Minion"、
//   メドウェイ海事トラスト所蔵）。甲板を持たない開放艇で、内部は船底の床板と、
//   舷縁の高さで左右の舷をつなぐ漕ぎ座だけを持つ。櫂は12挺で、1つの座に左右
//   1挺ずつを配る二段掛け（double-banked）なので座は6。
//   座の間隔は0.9〜1.1m級だが、1マス=1mで隣接させると座が連なって甲板と
//   見分けが付かないため、フレームと同じく2マス以上へ丸める。
//
// 甲板を張らない分岐は HullExpander.Shell.cs にあり、ここは空いた内部へ
// 床板と座を置くだけを受け持つ。呼び出しは HullExpander.Build の末尾。
public static partial class HullExpander
{
    private static void BuildOpenBoat(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, Top top, Palette p, TopPalette t)
    {
        if (!top.OpenBoat) return;

        BuildFloorBoards(cells, f, p);
        BuildThwarts(cells, f, top, t);
    }

    // 床板。船底外板の1段上へ、その高さの半幅に合わせて張る。竜骨の上へ直に敷かず
    // 1段上げるのは、実艇の床板が肋骨とフロア材の上に乗るため。
    // 素材はフレームと同じ accent_block（フロア材）。
    private static void BuildFloorBoards(
        Dictionary<(int x, int y, int z), string> cells, Form f, Palette p)
    {
        for (int z = 1; z < f.L - 1; z++)
        {
            int y = f.BottomY(z) + 1;
            if (y >= f.DeckY(z)) continue;   // 船首材・船尾の立ち上がりで内法が無い station

            f.Span(f.HalfAt(z, y), out int x0, out int x1);
            if (x1 - x0 < 2) continue;       // 舷が寄るところは床板を張る幅がない

            for (int x = x0 + 1; x <= x1 - 1; x++)
            {
                var key = (x, y, z);
                if (cells.ContainsKey(key)) continue;   // 外板・竜骨・肋骨を壊さない
                cells[key] = p.Frame;
            }
        }
    }

    // 漕ぎ座。舷縁の高さで左右の舷をつなぐ。実艇の座は舷縁より0.2m級下がるが、
    // 1マス=1mでは表せないので舷縁と同じ高さへ置く。左右の舷縁のマスは甲板材が
    // 入っているので、座はその内側だけを埋める。素材は舵・舵柄と同じ seat_block。
    private static void BuildThwarts(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.ThwartStep < 2) return;

        for (int z = top.ThwartStep; z < f.L - 1; z += top.ThwartStep)
        {
            int y = f.DeckY(z);
            f.Span(f.HalfAt(z, y), out int x0, out int x1);
            if (x1 - x0 < 2) continue;   // 舷縁が寄るところには座を渡さない

            for (int x = x0 + 1; x <= x1 - 1; x++) cells[(x, y, z)] = t.Fitting;
        }
    }
}
