using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 貨物ターミナル =====
    // 平面土木ではないが "airport:" 配下なのでここで作る。縮尺は持たず 1マス=1m。
    //
    // 実寸の出典（ACI 会員向け Air Cargo Facility Analysis / FAA）。
    //   トラックドック … 建物床面積 1,000 sq ft あたり 0.6 台（以前は 0.3 台）＝約155㎡に1台。
    //                    扉は幅 9ft（約2.7m）・高さ 10ft（約3m）。
    //   ドック高さ     … 48 インチ（1.2m）が標準。庫内の床はその分だけ地面より高い。
    //   庫内有効高さ   … 22ft（約7m）が従来標準だが今は不足。自動段積みを入れる棟は 40ft（約12m）。
    //   トラック回転   … 建物の面から取付道路まで 150ft（約46m）を空ける。
    //   事務所         … 倉庫面積の 10%。10万 sq ft 以上の棟では独立した事務所が好まれる。
    //   エプロン       … 建物床面積の 4.5 倍。敷地は建物15%・ランドサイド25%・エアサイド60%。
    //
    // StructureSpec との対応。桁行きはドック数×間隔の従属値。
    //   depth=建物の奥行き（エプロン側が z=0）/ height=庫内の有効高さ
    //   airport_docks・airport_dock_pitch … トラックドックの数と間隔
    //   airport_airside_doors・airport_door_width … エアサイドの大型扉
    //   airport_office … 事務所棟の桁行き / airport_canopy … ドック上屋
    //   tower_block=躯体 / glazing_block=高窓・トップライト / accent_block=まぐさ・帯
    //   floor_block=床・エプロン取付け / roof_block=屋根・上屋
    //   parapet_block=シャッター・パラペット / seat_block=庫内の照明
    //
    // 頭打ちすると端のドックだけ切れるので、収まらないときは幅を切らずにドック数を減らす。
    // 断面は「エプロン側が z=0」で組み、最後に Rotate で向きを回す。
    private const int CargoMaxLen = 256; // 桁行きの上限（マス）。超える分はドック数を減らす

    private static void BuildCargoTerminal(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int pitch = Clamp(spec.AirportDockPitch ?? 4, 3, 12);
        int docks = Clamp(spec.AirportDocks ?? 12, 2, 48);
        while (docks > 2 && docks * pitch > CargoMaxLen) docks--;

        int len = docks * pitch;                          // 桁行き（x）
        int depth = Clamp(spec.Depth, 16, 96);            // 奥行き（z）。z=0 がエアサイド
        int clear = Clamp(spec.Height, 5, 20);            // 庫内の有効高さ
        int doors = Clamp(spec.AirportAirsideDoors ?? 2, 0, 8);
        int doorW = Odd(Clamp(spec.AirportDoorWidth ?? 7, 3, 31));
        int canopy = Clamp(spec.AirportCanopy ?? 5, 0, 16);
        int office = Clamp(spec.AirportOffice ?? 24, 0, 64);

        int roofY = clear + 2;                            // 床 y=1 の上に有効高さ clear
        int doorH = Math.Min(clear, 8);
        int lastZ = depth - 1;

        // ===== 地面と床 =====
        // 床はドック高さ 1.2m ぶん地面より上げる（1マス）。
        Fill(cells, 0, len - 1, 0, 1, 0, lastZ, p.Pave);

        // ===== 外壁 =====
        // 最上段の一つ下を高窓にする。倉庫の採光は高窓とトップライトが基本。
        for (int y = 2; y < roofY; y++)
        {
            bool cl = (y == roofY - 2);
            for (int x = 0; x < len; x++)
            {
                string b = (cl && x % 2 == 0) ? p.Glass : p.Body;
                cells[(x, y, 0)] = b;
                cells[(x, y, lastZ)] = b;
            }
            for (int z = 0; z < depth; z++)
            {
                string b = (cl && z % 2 == 0) ? p.Glass : p.Body;
                cells[(0, y, z)] = b;
                cells[(len - 1, y, z)] = b;
            }
        }

        // ===== 庫内の柱と照明 =====
        for (int x = 12; x < len - 1; x += 12)
            for (int z = 12; z < depth - 1; z += 12)
                Fill(cells, x, x, 2, roofY - 1, z, z, p.Body);

        for (int x = 6; x < len - 1; x += 12)
            for (int z = 6; z < depth - 1; z += 12)
                cells[(x, roofY - 1, z)] = p.Light;

        // ===== 屋根・トップライト・パラペット =====
        Fill(cells, 0, len - 1, roofY, roofY, 0, lastZ, p.Roof);

        for (int x = 4; x < len - 1; x += 8)
            for (int z = 4; z < depth - 1; z += 8)
                cells[(x, roofY, z)] = p.Glass;

        for (int x = 0; x < len; x++)
        {
            cells[(x, roofY + 1, 0)] = p.Rail;
            cells[(x, roofY + 1, lastZ)] = p.Rail;
        }
        for (int z = 0; z < depth; z++)
        {
            cells[(0, roofY + 1, z)] = p.Rail;
            cells[(len - 1, roofY + 1, z)] = p.Rail;
        }

        // ===== ドック・上屋・大型扉・事務所棟 =====
        CargoDocks(cells, p, docks, pitch, len, depth, lastZ, roofY, canopy);
        CargoAirsideDoors(cells, p, doors, doorW, len, doorH);
        CargoOffice(cells, p, office, depth);
    }
}
