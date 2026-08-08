using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 公共施設（structure_type="civic:<種類>"）の座標生成。
// ship / venue と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・開口部・
// 入口保証・フットプリントマスクは一切通らない。既存の中分類には影響しない。
//
//   gym      … 体育館。バスケットボール競技コート 28×15m、周囲の安全域（サイド・エンド
//              各2m以上）を含めた最小アリーナは 32×19m、公式大会用で 34×22m。
//              有効天井高はバレーボールの 12.5m が上限側の目安。梁間スパンは 6.4m
//              （3.2m モジュール×2）。ステージは間口 10〜12m・奥行 5〜6m・高さ 0.9m。
//   hospital … 病棟。医療法施行規則により一般病床は 1 床あたり 6.4m² 以上、廊下幅は
//              片廊下 1.8m 以上／中廊下 2.7m 以上。ここは中廊下 3m、病室は間口 6m×
//              奥行 7m＝42m²（4 床室で 10.5m²/床）。階高 4m。
//   fire     … 消防署。はしご車は全長 12m・全高 3.5m なので車庫は奥行 12m・有効高 4.5m、
//              1 台あたり間口 5m、シャッター開口 4×4m。ホース乾燥塔（訓練塔）は
//              4〜5 階相当の 16m・平面 4×4m。
//   hall     … 庁舎。執務室は職員 1 人あたり 4.5〜6m²、柱スパン 6.4m、階高 3.9〜4.0m。
//              1〜2 階は窓口のある基壇、上層はセットバックした執務室棟。中央にコア
//              （階段・EV・便所）、正面にアトリウム（吹抜）。
//
// StructureSpec の既存フィールドだけで動く。対応は以下のとおり。
//   width/depth/height … 外形（体育館の height は軒高）
//   floor_levels       … 階数の指定（要素数＋1 階。体育館では先頭の値をギャラリー高さに使う）
//   roof_type          … 体育館の屋根形 "vault"（既定・円弧）/"gable"/"flat"
//   roof_pitch         … 切妻勾配、dome_height … 円弧屋根のライズ、ridge_axis … 棟の向き
//   pilaster_step      … 柱スパン（未指定は 6）、parapet_height/parapet_block … 陸屋根の立ち上がり
//   tower_*            … 消防署のホース乾燥塔、penthouse_* … 庁舎の塔屋
//   glazing_block … 窓、base_block … コートライン、seat_block … 手すり／カウンター
//   facade_face   … 正面（既定 south）、no_entrance … 玄関を開けない
//
// すべて「正面が南（+z 側）」で組み、最後に Rotate で向きを回す。
public static class PublicFacilityExpander
{
    public const string Prefix = "civic:";

    // StructureExpander から呼ぶ判定。"civic:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "hospital":
            case "ward": return "hospital";
            case "fire":
            case "fire_station": return "fire";
            case "hall":
            case "cityhall":
            case "city_hall": return "hall";
            default: return "gym";
        }
    }

    private sealed class Palette
    {
        public readonly string Wall, Floor, Roof, Accent, Glass, Line, Rail, Parapet, Tower, Penthouse;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Wall = Pick(spec.WallBlock, allowed, fallback);
            Floor = Pick(spec.FloorBlock, allowed, Wall);
            Roof = Pick(spec.RoofBlock, allowed, Wall);
            Accent = Pick(spec.AccentBlock, allowed, Wall);
            Glass = Pick(spec.GlazingBlock, allowed, Accent);
            Line = Pick(spec.BaseBlock, allowed, Accent);
            Rail = Pick(spec.SeatBlock, allowed, Accent);
            Parapet = Pick(spec.ParapetBlock, allowed, Wall);
            Tower = Pick(spec.TowerBlock, allowed, Wall);
            Penthouse = Pick(spec.PenthouseBlock, allowed, Wall);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        switch (KindOf(spec.StructureType))
        {
            case "hospital": BuildHospital(cells, spec, p); break;
            case "fire": BuildFireStation(cells, spec, p); break;
            case "hall": BuildCityHall(cells, spec, p); break;
            default: BuildGym(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== 体育館 =====
    private static void BuildGym(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 20, 63);
        int d = Clamp(spec.Depth, 16, 63);
        int eave = Clamp(spec.Height > 0 ? spec.Height : 13, 8, 24);   // 軒高
        int step = spec.PilasterStep ?? 0;
        if (step < 2) step = 6;                                        // 6.4m スパン相当
        string shape = (spec.RoofType ?? "vault").Trim().ToLowerInvariant();
        bool ridgeAlongX = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant() != "z";

        // アリーナの床。
        Fill(cells, 0, w - 1, 0, 0, 0, d - 1, p.Floor);

        // ステージ。正面の反対（z=0 側）に置き、床から 1 マス上げる（舞台高 0.9m）。
        int stageD = d >= 24 ? 6 : (d >= 20 ? 5 : 0);
        int stageW = Math.Min(w - 4, 12);
        if (stageD > 0 && stageW >= 6)
        {
            int stageX = (w - stageW) / 2;
            Fill(cells, stageX, stageX + stageW - 1, 0, 1, 1, stageD, p.Accent);
            Fill(cells, stageX, stageX + stageW - 1, 1, 1, 1, stageD, p.Floor);
        }
        else
        {
            stageD = 0;
        }

        // コートライン。競技コート 28×15m を中央に取り、外周に安全域を残す。
        int court = Math.Min(28, w - 4);                    // 長辺（x 方向）
        int courtD = Math.Min(15, d - stageD - 4);          // 短辺（z 方向）
        if (court >= 20 && courtD >= 11)
        {
            int cx0 = (w - court) / 2;
            int cx1 = cx0 + court - 1;
            int cz0 = stageD + (d - stageD - courtD) / 2;
            int cz1 = cz0 + courtD - 1;

            for (int x = cx0; x <= cx1; x++)
            {
                cells[(x, 0, cz0)] = p.Line;
                cells[(x, 0, cz1)] = p.Line;
            }
            for (int z = cz0; z <= cz1; z++)
            {
                cells[(cx0, 0, z)] = p.Line;
                cells[(cx1, 0, z)] = p.Line;
            }

            int mid = (cx0 + cx1) / 2;
            for (int z = cz0; z <= cz1; z++) cells[(mid, 0, z)] = p.Line;   // センターライン

            int ccz = (cz0 + cz1) / 2;                                      // センターサークル 半径1.8m
            for (int x = mid - 2; x <= mid + 2; x++)
                for (int z = ccz - 2; z <= ccz + 2; z++)
                {
                    double r = Math.Sqrt((x - mid) * (x - mid) + (z - ccz) * (z - ccz));
                    if (r >= 1.5 && r <= 2.4) cells[(x, 0, z)] = p.Line;
                }
        }

        // 外壁。柱型を step ごとに立てる。
        Walls(cells, 0, w - 1, 0, d - 1, 1, eave - 1, p.Wall, p.Accent, step);

        // ハイサイドライト。壁の上端寄りを採光帯にする（柱型は残す）。
        int bandTop = eave - 2;
        int bandBottom = Math.Max(2, eave - 4);
        for (int y = bandBottom; y <= bandTop; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                if (x % step == 0) continue;
                cells[(x, y, 0)] = p.Glass;
                cells[(x, y, d - 1)] = p.Glass;
            }
            for (int z = 1; z < d - 1; z++)
            {
                if (z % step == 0) continue;
                cells[(0, y, z)] = p.Glass;
                cells[(w - 1, y, z)] = p.Glass;
            }
        }

        // ギャラリー（2 階の回廊・ランニングコース）。幅 3 マス、内側に手すり。
        int gy = spec.FloorLevels.Count > 0 ? spec.FloorLevels[0] : 5;
        const int gw = 3;
        if (gy >= 4 && gy <= eave - 4 && w > 2 * (gw + 2) && d > 2 * (gw + 2))
        {
            for (int x = 1; x < w - 1; x++)
                for (int z = Math.Max(1, stageD + 1); z < d - 1; z++)
                {
                    int m = Math.Min(Math.Min(x, w - 1 - x), Math.Min(z, d - 1 - z));
                    if (m > gw) continue;
                    cells[(x, gy, z)] = p.Floor;
                    if (m == gw) cells[(x, gy + 1, z)] = p.Rail;
                }
        }

        // 屋根。体育館は円弧屋根が既定。切妻・陸屋根も選べる。
        if (shape == "flat")
        {
            Fill(cells, 0, w - 1, eave, eave, 0, d - 1, p.Roof);
            Parapet(cells, 0, w - 1, 0, d - 1, eave, Math.Max(1, spec.ParapetHeight ?? 1), p.Parapet);
        }
        else if (shape == "gable" || shape == "gable_stairs")
        {
            GableRoof(cells, 0, w - 1, 0, d - 1, eave,
                Math.Max(1, spec.RoofPitch ?? 2), ridgeAlongX, p.Roof, p.Wall);
        }
        else
        {
            int span = ridgeAlongX ? d - 1 : w - 1;
            int rise = Clamp(spec.DomeHeight ?? Math.Max(2, span / 5), 2, 24);
            VaultRoof(cells, 0, w - 1, 0, d - 1, eave, rise, ridgeAlongX, p.Roof, p.Wall);
        }

        if (!spec.NoEntrance) Entrance(cells, w, d, p);
    }

    // ===== 病棟 =====
    private static void BuildHospital(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        const int fh = 4;          // 階高 4m（天井高 2.1m 以上＋設備）
        const int corridor = 3;    // 中廊下 2.7m
        const int bay = 6;         // 病室の間口 6m

        int w = Clamp(spec.Width, 20, 63);
        int d = Clamp(spec.Depth, 17, 63);
        int floors = spec.FloorLevels.Count > 0
            ? Clamp(spec.FloorLevels.Count + 1, 1, 12)
            : Clamp(((spec.Height > 0 ? spec.Height : fh * 4 + 1) - 1) / fh, 1, 12);

        int roomD = (d - corridor) / 2;   // 病室の奥行
        int zc0 = roomD;                  // 北側の廊下間仕切り
        int zc1 = d - 1 - roomD;          // 南側の廊下間仕切り

        for (int f = 0; f < floors; f++)
        {
            int y0 = f * fh;
            int yTop = y0 + fh - 1;

            Fill(cells, 0, w - 1, y0, y0, 0, d - 1, p.Floor);
            Walls(cells, 0, w - 1, 0, d - 1, y0 + 1, yTop, p.Wall, p.Accent, 0);

            // 廊下の間仕切りと病室の間仕切り。
            for (int y = y0 + 1; y <= yTop; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    cells[(x, y, zc0)] = p.Wall;
                    cells[(x, y, zc1)] = p.Wall;
                }
                for (int x = bay; x < w - 1; x += bay)
                {
                    for (int z = 1; z < zc0; z++) cells[(x, y, z)] = p.Wall;
                    for (int z = zc1 + 1; z < d - 1; z++) cells[(x, y, z)] = p.Wall;
                }
            }

            // 病室の出入口（廊下側）と外壁の窓。
            for (int x0 = 1; x0 + bay <= w - 1; x0 += bay)
            {
                int cx = x0 + bay / 2;
                for (int y = y0 + 1; y <= Math.Min(y0 + 3, yTop); y++)
                {
                    cells.Remove((cx, y, zc0));
                    cells.Remove((cx, y, zc1));
                }
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    cells[(x, y0 + 2, 0)] = p.Glass;
                    cells[(x, y0 + 2, d - 1)] = p.Glass;
                }
            }

            // 階段室。廊下の西端を仕切り、廊下側に出入口を開ける。
            const int sx = 4;
            for (int y = y0 + 1; y <= yTop; y++)
                for (int z = zc0 + 1; z < zc1; z++) cells[(sx, y, z)] = p.Accent;
            for (int y = y0 + 1; y <= Math.Min(y0 + 3, yTop); y++)
                cells.Remove((sx, y, (zc0 + zc1) / 2));

            // ナースステーションのカウンター（腰高）。
            int nx = w / 2;
            for (int x = nx - 3; x <= nx + 2; x++) cells[(x, y0 + 1, zc0 + 1)] = p.Rail;
        }

        int roofY = floors * fh;
        Fill(cells, 0, w - 1, roofY, roofY, 0, d - 1, p.Roof);
        Parapet(cells, 0, w - 1, 0, d - 1, roofY, Math.Max(1, spec.ParapetHeight ?? 1), p.Parapet);

        if (!spec.NoEntrance) Entrance(cells, w, d, p);
    }

    // ===== 消防署 =====
    private static void BuildFireStation(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        const int garageD = 12;    // はしご車の全長
        const int garageH = 5;     // 車庫の有効高 4.5m
        const int bayW = 5;        // 1 台あたりの間口
        const int fh = 4;

        int w = Clamp(spec.Width, 12, 63);
        int d = Clamp(spec.Depth, 18, 63);
        int floors = Clamp(spec.FloorLevels.Count > 0 ? spec.FloorLevels.Count + 1 : 2, 1, 6);

        int gz0 = d - garageD;     // 車庫は正面（z=d-1）側
        int officeD = gz0;         // 事務・待機室棟の奥行
        int officeTop = floors * fh;

        // 車庫。
        Fill(cells, 0, w - 1, 0, 0, gz0, d - 1, p.Floor);
        Walls(cells, 0, w - 1, gz0, d - 1, 1, garageH - 1, p.Wall, p.Accent, 0);
        Fill(cells, 0, w - 1, garageH, garageH, gz0, d - 1, p.Roof);

        // シャッター。開口 4×4m を 1 台につき 1 か所、正面に等間隔で抜く。
        int bays = Clamp((w - 1) / bayW, 1, 8);
        for (int b = 0; b < bays; b++)
        {
            int cx = (int)Math.Round((b + 0.5) * (w - 1.0) / bays);
            for (int x = Math.Max(1, cx - 2); x <= Math.Min(w - 2, cx + 1); x++)
            {
                for (int y = 1; y <= 4; y++) cells.Remove((x, y, d - 1));
                cells[(x, garageH, d - 1)] = p.Accent;   // まぐさ
            }
        }

        // 事務・待機室棟。
        for (int f = 0; f < floors; f++)
        {
            int y0 = f * fh;
            Fill(cells, 0, w - 1, y0, y0, 0, officeD - 1, p.Floor);
            Walls(cells, 0, w - 1, 0, officeD - 1, y0 + 1, y0 + fh - 1, p.Wall, p.Accent, 0);

            for (int x = 2; x < w - 2; x += 2) cells[(x, y0 + 2, 0)] = p.Glass;
            for (int z = 2; z < officeD - 2; z += 2)
            {
                cells[(0, y0 + 2, z)] = p.Glass;
                cells[(w - 1, y0 + 2, z)] = p.Glass;
            }

            // 車庫と事務室をつなぐ出入口（1 階のみ）。両方の壁を抜く。
            if (f == 0)
                for (int y = 1; y <= 3; y++)
                {
                    cells.Remove((w / 2, y, officeD - 1));
                    cells.Remove((w / 2, y, gz0));
                }
        }
        Fill(cells, 0, w - 1, officeTop, officeTop, 0, officeD - 1, p.Roof);
        Parapet(cells, 0, w - 1, 0, officeD - 1, officeTop,
            Math.Max(1, spec.ParapetHeight ?? 1), p.Parapet);

        // ホース乾燥塔（訓練塔）。事務室棟の東端に取り付く。
        int tw = Clamp(spec.TowerWidth ?? 4, 3, 8);
        int th = Clamp(spec.TowerHeight ?? 16, 4, 40);
        int tx0 = Math.Max(0, w - tw);
        int tz1 = Math.Min(d - 1, tw - 1);

        Fill(cells, tx0, w - 1, 0, 0, 0, tz1, p.Floor);
        Walls(cells, tx0, w - 1, 0, tz1, 1, th, p.Tower, p.Accent, 0);
        for (int y = fh; y < th; y += fh)
            Fill(cells, tx0 + 1, w - 2, y, y, 1, Math.Max(1, tz1 - 1), p.Floor);
        for (int y = 2; y < th - 1; y++) cells.Remove((w - 1, y, tz1 / 2));   // 縦スリット

        int towerTop = th + 1;
        if ((spec.TowerRoof ?? "flat").Trim().ToLowerInvariant() == "spire")
        {
            for (int k = 0; k <= tw / 2; k++)
            {
                int a = tx0 + k, b2 = w - 1 - k, c = k, e = tz1 - k;
                if (a > b2 || c > e) break;
                Fill(cells, a, b2, towerTop + k, towerTop + k, c, e, p.Roof);
            }
        }
        else
        {
            Fill(cells, tx0, w - 1, towerTop, towerTop, 0, tz1, p.Roof);
        }
    }

    // ===== 庁舎 =====
    private static void BuildCityHall(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        const int fh = 4;         // 階高 3.9〜4.0m
        const int setback = 3;    // 基壇からのセットバック

        int w = Clamp(spec.Width, 20, 63);
        int d = Clamp(spec.Depth, 16, 63);
        int floors = Clamp(spec.FloorLevels.Count > 0 ? spec.FloorLevels.Count + 1 : 5, 2, 14);
        int podium = Math.Min(2, floors - 1);
        int step = spec.PilasterStep ?? 0;
        if (step < 2) step = 6;                       // 6.4m スパン相当

        const int coreW = 6;
        int coreD = Math.Min(6, d - 6);
        int cx0 = (w - coreW) / 2;
        int cz0 = (d - coreD) / 2;

        for (int f = 0; f < floors; f++)
        {
            bool low = f < podium;
            int x0 = low ? 0 : setback;
            int x1 = low ? w - 1 : w - 1 - setback;
            int z0 = low ? 0 : setback;
            int z1 = low ? d - 1 : d - 1 - setback;
            int y0 = f * fh;

            Fill(cells, x0, x1, y0, y0, z0, z1, p.Floor);
            Walls(cells, x0, x1, z0, z1, y0 + 1, y0 + fh - 1, p.Wall, p.Accent, step);

            // 横連窓。柱型の位置は avoid する。
            for (int x = x0 + 1; x < x1; x++)
            {
                if ((x - x0) % step == 0) continue;
                cells[(x, y0 + 2, z0)] = p.Glass;
                cells[(x, y0 + 2, z1)] = p.Glass;
            }
            for (int z = z0 + 1; z < z1; z++)
            {
                if ((z - z0) % step == 0) continue;
                cells[(x0, y0 + 2, z)] = p.Glass;
                cells[(x1, y0 + 2, z)] = p.Glass;
            }

            // コア（階段・EV・便所）。全階を貫き、廊下側に出入口を開ける。
            Walls(cells, cx0, cx0 + coreW - 1, cz0, cz0 + coreD - 1,
                y0 + 1, y0 + fh - 1, p.Accent, p.Accent, 0);
            for (int y = y0 + 1; y <= y0 + 3; y++)
                cells.Remove((cx0 + coreW / 2, y, cz0 + coreD - 1));
        }

        // アトリウム（吹抜）。基壇の正面中央の床を抜き、正面をガラスにする。
        if (podium >= 2)
        {
            int ax0 = Math.Max(1, w / 2 - 4);
            int ax1 = Math.Min(w - 2, w / 2 + 3);
            int az0 = Math.Max(cz0 + coreD + 1, d - 6);
            int az1 = d - 2;
            if (az1 >= az0)
            {
                for (int y = fh; y < podium * fh; y += fh) Clear(cells, ax0, ax1, y, y, az0, az1);
                for (int x = ax0; x <= ax1; x++)
                    for (int y = 1; y < podium * fh; y++) cells[(x, y, d - 1)] = p.Glass;
            }
        }

        // 基壇の屋上（セットバックで残る環）。
        int podiumTop = podium * fh;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                bool underTower = x >= setback && x <= w - 1 - setback
                               && z >= setback && z <= d - 1 - setback;
                if (!underTower) cells[(x, podiumTop, z)] = p.Roof;
            }
        Parapet(cells, 0, w - 1, 0, d - 1, podiumTop, 1, p.Parapet);

        // 執務室棟の屋根。
        int towerTop = floors * fh;
        Fill(cells, setback, w - 1 - setback, towerTop, towerTop, setback, d - 1 - setback, p.Roof);
        Parapet(cells, setback, w - 1 - setback, setback, d - 1 - setback, towerTop,
            Math.Max(1, spec.ParapetHeight ?? 1), p.Parapet);

        // 塔屋（機械室・階段室）。
        int phW = spec.PenthouseWidth ?? 0;
        int phD = spec.PenthouseDepth ?? 0;
        int phH = spec.PenthouseHeight ?? 0;
        if (phW >= 3 && phD >= 3 && phH > 0)
        {
            int px0 = Math.Max(setback, (w - phW) / 2);
            int pz0 = Math.Max(setback, (d - phD) / 2);
            int px1 = Math.Min(px0 + phW - 1, w - 1 - setback);
            int pz1 = Math.Min(pz0 + phD - 1, d - 1 - setback);
            if (px1 > px0 + 1 && pz1 > pz0 + 1)
            {
                Walls(cells, px0, px1, pz0, pz1, towerTop + 1, towerTop + phH,
                    p.Penthouse, p.Penthouse, 0);
                Fill(cells, px0, px1, towerTop + phH + 1, towerTop + phH + 1, pz0, pz1, p.Roof);
            }
        }

        if (!spec.NoEntrance) Entrance(cells, w, d, p);
    }

    // ===== 共通部品 =====

    private static void Fill(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string block)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++) cells[(x, y, z)] = block;
    }

    private static void Clear(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++) cells.Remove((x, y, z));
    }

    // 矩形の外周壁。step が 2 以上なら等間隔に柱型を入れる。角は必ず柱型。
    private static void Walls(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int y0, int y1, string wall, string accent, int step)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                    if (!edge) continue;

                    bool corner = (x == x0 || x == x1) && (z == z0 || z == z1);
                    bool pilaster = step > 1 &&
                        (((z == z0 || z == z1) && (x - x0) % step == 0) ||
                         ((x == x0 || x == x1) && (z - z0) % step == 0));

                    cells[(x, y, z)] = corner || pilaster ? accent : wall;
                }
    }

    // 陸屋根の立ち上がり。
    private static void Parapet(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int roofY, int height, string block)
    {
        for (int y = roofY + 1; y <= roofY + height; y++)
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (x == x0 || x == x1 || z == z0 || z == z1) cells[(x, y, z)] = block;
    }

    // 切妻屋根。棟の向きに直交する方向へ pitch マスごとに 1 段上げ、妻面も塞ぐ。
    private static void GableRoof(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int eaveY, int pitch, bool ridgeAlongX,
        string roof, string gable)
    {
        pitch = Math.Max(1, pitch);
        int a = ridgeAlongX ? z0 : x0;
        int b = ridgeAlongX ? z1 : x1;

        for (int t = a; t <= b; t++)
        {
            int k = Math.Min(t - a, b - t) / pitch;

            if (ridgeAlongX)
                for (int x = x0; x <= x1; x++) cells[(x, eaveY + k, t)] = roof;
            else
                for (int z = z0; z <= z1; z++) cells[(t, eaveY + k, z)] = roof;

            for (int y = eaveY; y < eaveY + k; y++)
            {
                if (ridgeAlongX)
                {
                    cells[(x0, y, t)] = gable;
                    cells[(x1, y, t)] = gable;
                }
                else
                {
                    cells[(t, y, z0)] = gable;
                    cells[(t, y, z1)] = gable;
                }
            }
        }
    }

    // 円弧屋根。棟に直交する方向へ半楕円のアーチを架け、妻面を塞ぐ。
    private static void VaultRoof(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int eaveY, int rise, bool ridgeAlongX,
        string roof, string gable)
    {
        int a = ridgeAlongX ? z0 : x0;
        int b = ridgeAlongX ? z1 : x1;
        double c = (a + b) / 2.0;
        double half = Math.Max(1.0, (b - a) / 2.0);

        int Profile(int t)
        {
            double u = (t - c) / half;
            double v = Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
            return eaveY + (int)Math.Round(rise * v);
        }

        for (int t = a; t <= b; t++)
        {
            int y = Profile(t);
            int neighbor = Math.Min(Profile(Math.Max(a, t - 1)), Profile(Math.Min(b, t + 1)));
            int lo = Math.Min(y, neighbor + 1);

            for (int yy = lo; yy <= y; yy++)
            {
                if (ridgeAlongX)
                    for (int x = x0; x <= x1; x++) cells[(x, yy, t)] = roof;
                else
                    for (int z = z0; z <= z1; z++) cells[(t, yy, z)] = roof;
            }

            for (int yy = eaveY; yy < lo; yy++)
            {
                if (ridgeAlongX)
                {
                    cells[(x0, yy, t)] = gable;
                    cells[(x1, yy, t)] = gable;
                }
                else
                {
                    cells[(t, yy, z0)] = gable;
                    cells[(t, yy, z1)] = gable;
                }
            }
        }
    }

    // 玄関。正面（z=d-1）の中央を幅 3・高さ 3 で抜き、まぐさと庇を付ける。
    private static void Entrance(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, Palette p)
    {
        int cx = (w - 1) / 2;
        for (int x = cx - 1; x <= cx + 1; x++)
            for (int y = 1; y <= 3; y++) cells.Remove((x, y, d - 1));

        for (int x = cx - 2; x <= cx + 2; x++)
        {
            cells[(x, 4, d - 1)] = p.Accent;
            cells[(x, 4, d)] = p.Roof;
        }
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 南向きで組んだものを指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string front)
    {
        if (front == "south" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = front switch
            {
                "north" => (w - 1 - x, d - 1 - z),
                "east" => (z, w - 1 - x),
                "west" => (d - 1 - z, x),
                _ => (x, z)
            };
            dst[(nx, kv.Key.y, nz)] = kv.Value;
        }
        return dst;
    }

    private static List<GeneratedBlock> Normalize(Dictionary<(int x, int y, int z), string> cells)
    {
        if (cells.Count == 0) return new List<GeneratedBlock>();

        int minX = cells.Keys.Min(k => k.x);
        int minY = cells.Keys.Min(k => k.y);
        int minZ = cells.Keys.Min(k => k.z);

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x - minX,
                Y = kv.Key.y - minY,
                Z = kv.Key.z - minZ,
                Id = kv.Value
            })
            .ToList();
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
