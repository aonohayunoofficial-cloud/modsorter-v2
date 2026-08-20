using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 付加部品: 軒・縁側・煙突・塔・塔の頂部・円柱・列柱・神殿のファサード。
// 屋根の上や平面の外側に足す要素なので、呼ぶ順序が結果を左右する。
// 順序の責任は StructureExpander.Core.cs の ExpandCore が持つ。
// StructureExpander の partial。
//
// 部品ごとにファイルを分けてある。
//   StructureExpander.Parts.cs             軒・縁側
//   StructureExpander.Parts.Chimney.cs     煙突
//   StructureExpander.Parts.Tower.cs       塔の本体
//   StructureExpander.Parts.Tower.Cap.cs   塔の配置決定と頂部
//   StructureExpander.Parts.Column.cs      円柱・列柱・柱位置の等間隔割り
//   StructureExpander.Parts.Temple.cs      神殿のファサード
public static partial class StructureExpander
{
    // 軒の出: 選択された面(north/south/east/west)の外側 eave マスへ、屋根を張り出す。
    //   各面の軒の高さは「屋根の実際の高さ」に合わせる。妻側(傾斜方向)は列ごとに
    //   階段状の高さで、軒先(棟に平行な面)は端の列の高さ(=水平)で伸びる。
    //   隣接2面がともに選択されたときは、その角も屋根の角の高さで埋めて穴を防ぐ。
    // 負座標(x=-1 等)も書くが、呼び出し元の一括シフトで 0 以上へ正規化される。
    private static void BuildEaves(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, StructureSpec spec,
        int w, int d, int h, string roof, string roofType, int eave)
    {
        bool en = spec.EaveNorth, es = spec.EaveSouth, ee = spec.EaveEast, ew = spec.EaveWest;
        if (!en && !es && !ee && !ew) return; // どの面も選ばれていなければ軒なし。

        // 屋根の (x,z) 列の最高y。屋根が無い列は h-1（壁上端）を返す。
        // flat/gable/shed いずれも、既に cells に積まれた屋根の実高さをそのまま使うので
        // 屋根形状に依らず正しい高さで軒が揃う。
        int RoofTop(int x, int z)
        {
            int top = int.MinValue;
            foreach (var k in cells.Keys)
                if (k.x == x && k.z == z && k.y >= h - 1 && k.y > top) top = k.y;
            return top == int.MinValue ? (h - 1) : top;
        }

        // 北面(z=0の外側 z<0)。x=0..w-1 の各列を、その列の屋根高さで z<0 へ伸ばす。
        if (en)
            for (int x = 0; x < w; x++)
            {
                int y = RoofTop(x, 0);
                for (int e = 1; e <= eave; e++) cells[(x, y, -e)] = roof;
            }
        // 南面(z=d-1の外側 z>=d)。
        if (es)
            for (int x = 0; x < w; x++)
            {
                int y = RoofTop(x, d - 1);
                for (int e = 1; e <= eave; e++) cells[(x, y, d - 1 + e)] = roof;
            }
        // 西面(x=0の外側 x<0)。
        if (ew)
            for (int z = 0; z < d; z++)
            {
                int y = RoofTop(0, z);
                for (int e = 1; e <= eave; e++) cells[(-e, y, z)] = roof;
            }
        // 東面(x=w-1の外側 x>=w)。
        if (ee)
            for (int z = 0; z < d; z++)
            {
                int y = RoofTop(w - 1, z);
                for (int e = 1; e <= eave; e++) cells[(w - 1 + e, y, z)] = roof;
            }

        // ===== 角埋め: 隣接2面がともに選ばれたら、その隅の eave×eave を屋根の角高さで埋める =====
        // 角の高さは屋根のその隅(0,0)/(w-1,0)/(0,d-1)/(w-1,d-1)の実高さに合わせる。
        void FillCorner(bool cond, int cornerX, int cornerZ, int sxSign, int szSign)
        {
            if (!cond) return;
            int y = RoofTop(cornerX, cornerZ);
            for (int ex = 1; ex <= eave; ex++)
                for (int ez = 1; ez <= eave; ez++)
                    cells[(cornerX + sxSign * ex, y, cornerZ + szSign * ez)] = roof;
        }
        FillCorner(ew && en, 0, 0, -1, -1);         // 北西
        FillCorner(ee && en, w - 1, 0, +1, -1);      // 北東
        FillCorner(ew && es, 0, d - 1, -1, +1);      // 南西
        FillCorner(ee && es, w - 1, d - 1, +1, +1);  // 南東
    }

    // 縁側／基壇の縁: 平面マスクの外側へ v マスぶん、y=0 に床を敷き足す。
    // マスク内のどのマスからチェビシェフ距離 v 以内かで判定するので、
    // L字・十字などの非矩形でも輪郭に沿って回り縁ができる（角が欠けない）。
    // 建物の真下は既に床があるので触らない。軒と同じく負座標を一時的に作る。
    private static void BuildVeranda(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, int w, int d, int v, string block)
    {
        if (v <= 0) return;

        for (int x = -v; x < w + v; x++)
            for (int z = -v; z < d + v; z++)
            {
                if (foot.Contains((x, z))) continue;

                bool near = false;
                for (int ox = -v; ox <= v && !near; ox++)
                    for (int oz = -v; oz <= v; oz++)
                        if (foot.Contains((x + ox, z + oz))) { near = true; break; }

                if (near) cells[(x, 0, z)] = block;
            }
    }
}
