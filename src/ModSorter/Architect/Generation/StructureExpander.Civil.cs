using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 土木系（スロープ・橋）。床/壁/屋根/開口部を一切通さず、座標リストを直接返す別系統。
// ExpandCore は structure_type で早期リターンし、ここへ丸ごと委譲する。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // スロープ（坂道）: ridge_axis で傾斜方向を選ぶ。
    //   ridge_axis="x"（既定）→ x方向に進むほど高くなる（z方向に幅）。
    //   ridge_axis="z"        → z方向に進むほど高くなる（x方向に幅）。
    // 進行方向の各位置で、床から「その位置の目標高さ」までを body で満たす中実スロープ。
    // 下を base、踏面（各段の最上面）も含めて中身を詰めるので、宙に浮かず歩いて登れる。
    // 高さは進行方向の長さに合わせて h-1 段まで一定割合で上げる。
    private static List<GeneratedBlock> BuildRamp(
        int w, int d, int h, string body, string baseBlock, string? ridgeAxis)
    {
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 進行方向（傾斜が上がる向き）。"z" 指定時のみ z 方向、それ以外は x 方向。
        bool runAlongX = (ridgeAxis ?? "x").Trim().ToLowerInvariant() != "z";

        int runLen = runAlongX ? w : d; // 傾斜方向の長さ
        int crossLen = runAlongX ? d : w; // 幅方向の長さ
        int topY = h - 1; // 最大の高さ（最上段の y）

        for (int i = 0; i < runLen; i++)
        {
            // 進行位置 i に対する目標高さ。i=0 で 0 段、i=runLen-1 で topY 段。
            // 1マス進むごとに段が上がる比率を、長さと高さから線形に決める。
            int levelY = (runLen <= 1)
                ? topY
                : (int)System.Math.Round((double)topY * i / (runLen - 1));

            for (int c = 0; c < crossLen; c++)
            {
                int x = runAlongX ? i : c;
                int z = runAlongX ? c : i;

                // 床から levelY までを中実に満たす。最下段(y=0)は base、上は body。
                for (int y = 0; y <= levelY; y++)
                    cells[(x, y, z)] = (y == 0) ? baseBlock : body;
            }
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }

    // 橋（桁橋＋橋脚＋欄干）: ridge_axis で渡す向きを選ぶ。
    //   ridge_axis="x"（既定）→ 橋は x 方向に渡る（z 方向に幅）。
    //   ridge_axis="z"        → 橋は z 方向に渡る（x 方向に幅）。
    // 構成:
    //   ・路面(deck): 高さ deckY に進行方向いっぱいの水平面を敷く。歩いて渡れる。
    //   ・橋脚(pier): 進行方向に等間隔の数か所で、路面の下を地面(y=0)まで柱で支える。
    //   ・欄干(rail): 路面の両縁(幅方向の端)に高さ1の手すりを立てる。橋らしさを出す。
    // deckY は h-1 とし、橋脚が地面から路面まで届く。幅が2未満なら欄干は省く。
    private static List<GeneratedBlock> BuildBridge(
        int w, int d, int h, string deck, string pier, string? ridgeAxis)
    {
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 渡る向き。"z" 指定時のみ z 方向、それ以外は x 方向。
        bool runAlongX = (ridgeAxis ?? "x").Trim().ToLowerInvariant() != "z";

        int runLen = runAlongX ? w : d;   // 渡る方向の長さ（スパン）
        int crossLen = runAlongX ? d : w; // 幅方向の長さ
        int deckY = h - 1;                // 路面の高さ（最上段）

        // 進行方向 i・幅方向 c を実座標(x,z)へ変換するローカル関数。
        (int x, int z) ToXz(int i, int c) => runAlongX ? (i, c) : (c, i);

        // 路面: deckY に幅いっぱいの水平面。
        for (int i = 0; i < runLen; i++)
            for (int c = 0; c < crossLen; c++)
            {
                var (x, z) = ToXz(i, c);
                cells[(x, deckY, z)] = deck;
            }

        // 橋脚: 進行方向に等間隔の数か所で、路面の下(y=0..deckY-1)を柱で支える。
        // 本数は概ね4マスごと。両端は必ず脚を置いて橋台にする。
        int pierStep = Math.Max(4, runLen / 4);
        var pierPositions = AxisPositions(0, runLen - 1, pierStep);
        foreach (int i in pierPositions)
            for (int c = 0; c < crossLen; c++)
            {
                var (x, z) = ToXz(i, c);
                for (int y = 0; y < deckY; y++)
                    cells[(x, y, z)] = pier;
            }

        // 欄干: 路面の両縁(幅方向の端 c=0 と c=crossLen-1)に高さ1の手すり。
        // 幅が2未満なら省略（路面と重なってしまうため）。
        if (crossLen >= 2)
        {
            for (int i = 0; i < runLen; i++)
            {
                var (xa, za) = ToXz(i, 0);
                var (xb, zb) = ToXz(i, crossLen - 1);
                cells[(xa, deckY + 1, za)] = pier;
                cells[(xb, deckY + 1, zb)] = pier;
            }
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }
}
