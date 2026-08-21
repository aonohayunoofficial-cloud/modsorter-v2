using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 格納庫 =====
    // 平面土木ではないが "airport:" 配下なのでここで作る。縮尺は持たず 1マス=1m。
    //
    // 実寸の出典。
    //   扉の高さ … NFPA 409 は 28ft（8.5m）超を Group I とし消火設備の要求が上がる。
    //              2026 年の改訂でこの境が 35ft（10.7m）へ上がった。
    //   尾翼高さ … CRJ200 6.2m / A320 11.8m / B777 18.5m / A380 24.1m。
    //              扉の高さは尾翼高さ＋1〜1.5m のクリアランスを取る。
    //   扉の幅   … 翼幅＋両側のクリアランス。エプロンのスポット幅と同じ考え方。
    //   実例     … A380 対応で 幅45m×奥行62m×有効高さ18m（機体を持ち上げても収まる高さ）。
    //   附属棟   … 側面に工場・部品庫・事務所を並べる。
    //
    // StructureSpec との対応。width=扉の開口幅 / depth=奥行き / height=庫内の有効高さ。
    //   airport_door_height・airport_door_type … 扉の高さと形式
    //   airport_bays … 収める機体の数 / airport_hangar_roof … 屋根の形
    //   airport_annex … 側面の附属棟の奥行き
    //   tower_block=躯体・トラス / glazing_block=扉の窓・高窓 / accent_block=まぐさ・柱
    //   floor_block=床 / roof_block=屋根 / parapet_block=扉 / seat_block=庫内の照明
    //
    // 扉の開口はスパンそのものなので、無柱にするため開口側には柱を立てない。
    // 断面は「扉がエプロン側＝z=0」で組み、最後に Rotate で向きを回す。
    private static void BuildHangar(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int bays = Clamp(spec.AirportBays ?? 1, 1, 4);
        int span = Odd(Clamp(spec.Width, 11, 128)) * bays;   // 扉の開口幅
        int depth = Clamp(spec.Depth, 12, 96);
        int clear = Clamp(spec.Height, 6, 32);               // 庫内の有効高さ
        int doorH = Clamp(spec.AirportDoorHeight ?? (clear - 2), 4, clear);
        int annex = Clamp(spec.AirportAnnex ?? 0, 0, 24);

        string roof = RoofOf(spec.AirportHangarRoof);
        string door = DoorOf(spec.AirportDoorType);

        int len = span + 2;          // 躯体の外寸。開口の両脇に柱型 1 マス
        int lastZ = depth - 1;
        int wallTop = clear;         // 側壁の頂部

        // ===== 床 =====
        Fill(cells, 0, len - 1, 0, 0, 0, lastZ, p.Pave);

        // ===== 側壁と背面 =====
        for (int y = 1; y <= wallTop; y++)
        {
            bool cl = (y == wallTop - 1);   // 高窓の段
            for (int z = 0; z < depth; z++)
            {
                string b = (cl && z % 2 == 0) ? p.Glass : p.Body;
                cells[(0, y, z)] = b;
                cells[(len - 1, y, z)] = b;
            }
            for (int x = 0; x < len; x++)
                cells[(x, y, lastZ)] = (cl && x % 2 == 0) ? p.Glass : p.Body;
        }

        // 側壁の柱型。9m ごと。開口側（z=0）には立てない。
        for (int z = 9; z < depth - 1; z += 9)
        {
            Fill(cells, 0, 0, 1, wallTop, z, z, p.Mark);
            Fill(cells, len - 1, len - 1, 1, wallTop, z, z, p.Mark);
        }

        // ===== 屋根 =====
        // アーチは奥行き方向ではなく開口方向に架かる（無柱スパンを見せるため）。
        var h = new int[len];
        int rise;
        switch (roof)
        {
            case "flat":
                rise = 0;
                for (int x = 0; x < len; x++) h[x] = 0;
                break;
            case "shed":
                rise = Math.Max(2, len / 8);
                for (int x = 0; x < len; x++) h[x] = rise * x / Math.Max(1, len - 1);
                break;
            default: // arch
                rise = Math.Max(3, len / 6);
                for (int x = 0; x < len; x++)
                    h[x] = (int)Math.Round(rise * Math.Sin(Math.PI * (x + 0.5) / len));
                break;
        }

        // 妻側（背面）と開口側のまぐさ上を弧の下まで立ち上げる。
        for (int x = 0; x < len; x++)
        {
            if (h[x] > 0)
            {
                Fill(cells, x, x, wallTop + 1, wallTop + h[x], lastZ, lastZ, p.Body);
                Fill(cells, x, x, wallTop + 1, wallTop + h[x], 0, 0, p.Body);
            }
        }

        // 屋根面。隣との段差ぶんだけ下へ伸ばして穴を塞ぐ。
        for (int x = 0; x < len; x++)
        {
            int prev = x > 0 ? h[x - 1] : 0;
            int next = x < len - 1 ? h[x + 1] : 0;
            int lo = wallTop + 1 + Math.Min(h[x], Math.Min(prev, next)) - (h[x] > 0 ? 1 : 0);
            if (lo < wallTop + 1) lo = wallTop + 1;
            Fill(cells, x, x, lo, wallTop + 1 + h[x], 0, lastZ, p.Roof);
        }

        // トラス。屋根の裏側に 12m ごとの帯を回す。
        for (int z = 6; z < depth - 1; z += 12)
            for (int x = 1; x < len - 1; x++)
                cells[(x, wallTop + h[x], z)] = p.Body;

        // ===== 庫内の照明 =====
        for (int x = 6; x < len - 1; x += 10)
            for (int z = 6; z < depth - 1; z += 10)
                cells[(x, wallTop + h[x] - 1, z)] = p.Light;

        // ===== 扉・附属棟 =====
        HangarDoor(cells, p, len, doorH, wallTop, door);
        HangarAnnex(cells, p, len, depth, lastZ, annex);
    }
}
