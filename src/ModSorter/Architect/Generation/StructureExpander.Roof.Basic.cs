using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋根（基本形）: 平屋根・切妻・階段ブロック版切妻・片流れ。
// StructureExpander の partial。生成順序は StructureExpander.cs の ExpandCore が持つ。
public static partial class StructureExpander
{
    // 平屋根: 最上層 y=h-1 をマスク内で塞ぐ（フットプリントに沿う）。
    private static void BuildFlatRoof(
        Dictionary<(int x, int y, int z), string> cells, HashSet<(int x, int z)> foot, int h, string roof)
    {
        foreach (var (x, z) in foot)
            cells[(x, h - 1, z)] = roof;
    }

    // 切妻屋根: 棟の向き(ridge_axis)に沿って段々に三角を作る。
    // 屋根は本体の上(y=h から上)に積む。傾斜方向の端から中央へ向け段を上げる。
    // 勾配(spec.RoofPitch)は run 何マスにつき rise 1マス上げるか。
    // 1(既定)=1マスで1段=45°で従来と完全一致。2以上で緩勾配になる。
    private static void BuildGableRoof(
    Dictionary<(int x, int y, int z), string> cells,
    StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        // 棟がx軸に平行 → z方向に傾斜（zの端から中央へ高くなる）
        // 棟がz軸に平行 → x方向に傾斜（xの端から中央へ高くなる）
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w; // 傾斜する方向の長さ
        // 端から中央までの段数。中央で最も高い。
        int half = (slopeLen + 1) / 2;

        // 勾配。null/0/1 は 1（45°・従来どおり）。大きいほど緩い（1〜4に制限）。
        int pitch = spec.RoofPitch.HasValue && spec.RoofPitch.Value >= 1
            ? System.Math.Min(4, spec.RoofPitch.Value)
            : 1;

        for (int i = 0; i < slopeLen; i++)
        {
            // 端(0 と slopeLen-1)が低く、中央が高い。端からの距離を勾配で割って段数にする。
            // pitch=1 なら距離そのまま(=45°、従来と一致)。pitch が大きいほど段が緩くなる。
            int dist = System.Math.Min(i, slopeLen - 1 - i);
            int step = dist / pitch;
            int yLevel = (h - 1) + step; // 壁の最上層と同じ高さ(y=h-1)から積む

            if (ridgeAlongX)
            {
                // 屋根: z=i の列。棟方向(x)は全幅に渡って同じ高さ。
                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = roof;

                // 妻壁: 妻側の面(x=0 と x=w-1)で、壁の上端(y=h-1)から
                //       この列の屋根の手前(yLevel-1)までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // 屋根: x=i の列。棟方向(z)は全奥行きに渡って同じ高さ。
                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = roof;

                // 妻壁: 妻側の面(z=0 と z=d-1)で、壁の上端からこの列の屋根の手前までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }
    }

    // 片流れ屋根(shed/skillion): 一方の端から反対端へ一直線に上がる非対称の傾斜。
    // 棟の向き(ridge_axis)に直交する方向へ傾く。gable と同じ座標系・妻壁充填を流用する。
    // 勾配は RoofPitch(1..4)。pitch マスの水平移動につき1段上がる（pitch=1 で45度）。
    private static void BuildShedRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        // 棟がx軸に平行 → z方向に傾斜（z=0 が低く、z=d-1 へ向け高くなる）
        // 棟がz軸に平行 → x方向に傾斜（x=0 が低く、x=w-1 へ向け高くなる）
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w; // 傾斜する方向の長さ

        // 勾配: RoofPitch 未指定は1(=45度)。大きいほど緩い。1..4にクランプ。
        int pitch = spec.RoofPitch ?? 1;
        if (pitch < 1) pitch = 1;
        if (pitch > 4) pitch = 4;

        for (int i = 0; i < slopeLen; i++)
        {
            // 端(i=0)が最も低く、反対端(i=slopeLen-1)へ一直線に上がる。
            // gable の Min(i, slopeLen-1-i) と違い、距離 i をそのまま使うのが片流れ。
            int step = i / pitch;
            int yLevel = (h - 1) + step; // 壁の最上層(y=h-1)から積む

            if (ridgeAlongX)
            {
                // 屋根: z=i の列。棟方向(x)は全幅に渡って同じ高さ。
                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = roof;

                // 妻壁: 傾斜に直交する2面(x=0 と x=w-1)を、壁の上端(y=h-1)から
                //       この列の屋根の手前(yLevel-1)まで埋める。左右で高さが変わり階段状に閉じる。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // 屋根: x=i の列。棟方向(z)は全奥行きに渡って同じ高さ。
                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = roof;

                // 妻壁: 傾斜に直交する2面(z=0 と z=d-1)を埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }

        // ===== 棟に平行な2端面（低い側・高い側）の妻壁立ち上げ（方式①）=====
        // shed は傾斜方向の両端(棟平行の面)が屋根の下に三角の穴を残す。
        // 各端面を、その端に対応する屋根高さ yLevel-1 まで壁で立ち上げて塞ぐ。
        {
            // 傾斜方向の両端インデックスと、それぞれの屋根高さ。
            int loEnd = 0;                       // 低い側 i=0
            int hiEnd = slopeLen - 1;            // 高い側 i=slopeLen-1
            int loY = (h - 1) + (loEnd / pitch); // 低い側の屋根高さ（=h-1）
            int hiY = (h - 1) + (hiEnd / pitch); // 高い側の屋根高さ

            if (ridgeAlongX)
            {
                // 端面は z=0（低）と z=d-1（高）。棟方向 x を全幅、y は h-1..端の屋根手前。
                for (int x = 0; x < w; x++)
                {
                    for (int y = h - 1; y < loY; y++) cells[(x, y, 0)] = wall;
                    for (int y = h - 1; y < hiY; y++) cells[(x, y, d - 1)] = wall;
                }
            }
            else
            {
                // 端面は x=0（低）と x=w-1（高）。棟方向 z を全奥行き。
                for (int z = 0; z < d; z++)
                {
                    for (int y = h - 1; y < loY; y++) cells[(0, y, z)] = wall;
                    for (int y = h - 1; y < hiY; y++) cells[(w - 1, y, z)] = wall;
                }
            }
        }
    }


    // 切妻屋根（階段ブロック版）: 各段の屋根面を階段ブロックにし、
    // 傾斜の下り方向へ facing を向ける。棟（最上段）はフルブロックで尖らせる。
    // roof には階段ブロックID（例: minecraft:oak_stairs）が渡る想定。
    // 状態は id に "[facing=...,half=bottom]" を埋め込む（プレビューは baseId で判定）。
    private static void BuildGableStairsRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w;
        int half = (slopeLen + 1) / 2;

        // 棟の位置（傾斜方向の中央）。ここはフルブロックで尖らせる。
        int ridgeLo = (slopeLen - 1) / 2;
        int ridgeHi = slopeLen / 2;

        for (int i = 0; i < slopeLen; i++)
        {
            int step = System.Math.Min(i, slopeLen - 1 - i);
            int yLevel = (h - 1) + step;

            bool isRidge = (i == ridgeLo || i == ridgeHi);
            // 軒側へ下る向き。前半(i<中央)は一方向、後半は逆向き。
            bool lowerSide = (i < slopeLen / 2); // 端0側か

            if (ridgeAlongX)
            {
                // 階段の facing: z方向に傾斜。下り側を向く。
                // 端0側は south(z+方向へ下る)を向く＝facing=south、反対側は north。
                string facing = lowerSide ? "south" : "north";
                string block = isRidge ? roof : StairId(roof, facing);

                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = block;

                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // x方向に傾斜。端0側は east(x+方向へ下る)、反対側は west。
                string facing = lowerSide ? "east" : "west";
                string block = isRidge ? roof : StairId(roof, facing);

                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = block;

                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }
    }

    // 階段ブロックの id に向き状態を埋め込む。素材が階段でない場合もIDだけ付ける
    // （Minecraft側で無効なら無視されるだけ。基本は roof に *_stairs を選ばせる）。
    private static string StairId(string block, string facing)
        => $"{block}[facing={facing},half=bottom]";
}
