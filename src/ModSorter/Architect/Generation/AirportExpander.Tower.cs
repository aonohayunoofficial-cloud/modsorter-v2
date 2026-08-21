using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 管制塔 =====
    // 平面土木ではないが "airport:" 配下なのでここで作る。縮尺は持たず 1マス=1m。
    //
    // 実寸の出典。
    //   管制室の床面積 … FAA Order 6480.7D の標準型で 234 / 350 / 625 / 850 sq ft
    //                    ＝ 22 / 33 / 58 / 79 ㎡。羽田の新管制塔は約130㎡・塔高113m級。
    //   平面形         … 正方形・五角形・六角形・八角形・円形。八角形が最多。
    //   窓の傾き       … 鉛直から外へ 15 度（室内の映り込みを天井へ逃がすため）。
    //                    何段で1マス外へ出すかで近似し、4段＝14.0度が15度に最も近い。
    //   腰壁           … 最下段はコンソールが並ぶ高さなので窓ではなく壁にする。
    //   キャットウォーク … 窓の清掃用に管制室の外周へ回す。実物は幅1m級＋手すり。
    //   シャフト       … エレベーター・階段・ケーブルシャフトを収める。外寸6〜10m級。
    //   航空障害灯     … 塔頂と屋根の四方に付ける。
    //
    // StructureSpec との対応。
    //   height=管制室の床の高さ / width・depth=庁舎の平面寸法
    //   airport_cab_width・airport_cab_height・airport_cab_shape・airport_cab_tilt … 管制室
    //   airport_shaft_width・airport_floor_step … シャフト
    //   airport_catwalk … 外周通路 / airport_base_height … 庁舎 / airport_mast … アンテナ柱
    //   airport_edge_light … 0 以外で航空障害灯を点ける
    //   tower_block=塔身 / glazing_block=窓 / accent_block=窓枠・方立・腰壁
    //   floor_block=床・キャットウォーク / roof_block=屋根 / parapet_block=手すり
    //   base_block=庁舎 / seat_block=灯火
    //
    // 断面は「正面（見通す側）が z の小さい側」で組み、最後に Rotate で向きを回す。
    // 中心を (0,0) に置くので座標は負へ出るが、Normalize が 0 起点へ寄せる。
    private static void BuildControlTower(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        string shape = ShapeOf(spec.AirportCabShape);
        int cabW = Odd(Clamp(spec.AirportCabWidth ?? 11, 5, 33));
        int cabR = (cabW - 1) / 2;
        int shaftW = Odd(Clamp(spec.AirportShaftWidth ?? 9, 3, cabW));
        int shaftR = Math.Min(cabR, (shaftW - 1) / 2);
        int cabH = Clamp(spec.AirportCabHeight ?? 4, 2, 12);
        int tilt = Clamp(spec.AirportCabTilt ?? 4, 0, 12);
        int walk = Clamp(spec.AirportCatwalk ?? 1, 0, 3);
        int step = Clamp(spec.AirportFloorStep ?? 5, 0, 16);
        int mast = Clamp(spec.AirportMast ?? 6, 0, 24);
        bool light = (spec.AirportEdgeLight ?? 60) > 0;

        int baseH = Clamp(spec.AirportBaseHeight ?? 0, 0, 24);
        int baseW = Clamp(spec.Width, 0, 64);
        int baseD = Clamp(spec.Depth, 0, 64);
        bool hasBase = baseH >= 3 && baseW >= 7 && baseD >= 7;

        // 管制室の床の高さ。庁舎の屋根より下へは来ない。
        int floorY = Clamp(spec.Height, hasBase ? baseH + 4 : 6, 96);

        // ===== 庁舎 =====
        if (hasBase) TowerBase(cells, p, shape, shaftR, baseW, baseD, baseH);

        // ===== シャフト =====
        PlanFill(cells, shape, shaftR, 0, p.Pave, false);
        for (int y = 1; y < floorY; y++)
            PlanFill(cells, shape, shaftR, y, p.Body, true);

        // 中間床（機械室・休憩室の階）。
        if (step >= 2 && shaftR >= 2)
            for (int y = step; y <= floorY - 2; y += step)
                PlanFill(cells, shape, shaftR - 1, y, p.Pave, false);

        // 正面に走る縦のスリット窓。中間床の位置だけ帯で締める。
        if (floorY >= 12)
            for (int y = 4; y <= floorY - 4; y++)
                cells[(0, y, -shaftR)] = (step >= 2 && y % step == 0) ? p.Mark : p.Glass;

        // 庁舎が無いときはシャフトの足元に出入口を開ける。
        if (!hasBase)
        {
            int dw = shaftR >= 3 ? 1 : 0;
            for (int x = -dw; x <= dw; x++)
                for (int y = 1; y <= 3; y++)
                    cells.Remove((x, y, -shaftR));
        }

        // ===== 管制室の張り出し（シャフトから外へ広げる持ち送り）=====
        for (int k = 1; k <= cabR - shaftR; k++)
        {
            int y = floorY - k;
            if (y <= (hasBase ? baseH + 1 : 1)) break;
            PlanFill(cells, shape, cabR - k, y, p.Mark, true);
        }

        // ===== 管制室から上（キャットウォーク・窓・屋根・アンテナ柱・航空障害灯）=====
        TowerCab(cells, p, shape, cabR, cabH, floorY, tilt, walk, mast, light);
    }
}
