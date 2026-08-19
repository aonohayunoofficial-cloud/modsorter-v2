using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 発電所。IndustryExpander の partial。
// 港湾・空港と同じ方針で、1基まるごとではなく構成する単体構造物ごとに生成する。
// 1マス=1m。canonical の正面は +z（南）で、全体の向きは Build 末尾の Rotate で回す。
//
// 実寸の出典。
//   タービン建屋 … 実施例で長さ60m×幅36m×高さ32m、別例で83m×83m×高さ36m
//     〔環境影響評価書 発電所設備の諸元〕。機器の吊り出しに天井クレーンを使うので
//     屋根直下に走行梁が通る〔turbine hall の構成〕。
//   ボイラ建屋（排熱回収ボイラ）… 長さ45m×幅30m×高さ40m。上載荷重が大きい煙突・
//     排熱回収ボイラ・タービン建屋は基礎スラブ4m厚〔東京都 発電所の建設費〕。
//   煙突 … 東京電力の15火力で37本・最低85m・最高230m・平均170m〔東電報〕。
//     100m級で口径5.7m・2基〔新仙台火力リプレース〕。集合煙突は複数の内筒を
//     1つの外筒に納める。地上高60m以上に航空障害灯が必要〔国交省 航空障害灯〕。
//   自然通風冷却塔 … 高さ190mの実機で底部半径65.25m・喉部42m・頂部43.45m、
//     喉部は全高の0.75〔A Study on Dynamic Behavior of Natural Draft Cooling Towers〕。
//     高さ202m・底部直径141mの例、高さ131.1m・底部98.0m・頂部58.18mの例がある。
//     シェルはV字の斜め柱（レーカーコラム、直径1.2m・高さ9m）で支える
//     〔Design and construction aspects of natural draft cooling towers in India〕。
//   格納容器 … ABWR は内径29m・内高29.5m〔東芝レビュー ABWR の原子炉システム〕。
//     PWR はドーム内半径22.8m・ドーム高さ23.5m級。原子炉建屋の基礎スラブは83m×83m。
//   変電ヤード … 門型（ガントリー）で引留め、三相の母線を渡す。変圧器は防火壁で仕切る。
public static partial class IndustryExpander
{
    // ===== 平面の円（中心を実数で持つ）=====
    // 段ごとに径が変わる殻（煙突のテーパ、冷却塔の双曲面）に使う。直径を箱に収める
    // Disc/Ring では中心がマス境界に寄って段ごとにずれるので、中心を実数で持つ。
    private static void ShellDisc(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cz, double r, int y0, int y1, string id)
    {
        if (r < 0.5) return;
        int x0 = (int)Math.Floor(cx - r), x1 = (int)Math.Ceiling(cx + r);
        int z0 = (int)Math.Floor(cz - r), z1 = (int)Math.Ceiling(cz + r);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    double dx = x - cx, dz = z - cz;
                    if (dx * dx + dz * dz <= r * r) cells[(x, y, z)] = id;
                }
    }

    // 厚み1マスの円環。
    private static void ShellRing(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cz, double r, int y0, int y1, string id)
    {
        if (r < 1.0) return;
        double ri = r - 1.0;
        int x0 = (int)Math.Floor(cx - r), x1 = (int)Math.Ceiling(cx + r);
        int z0 = (int)Math.Floor(cz - r), z1 = (int)Math.Ceiling(cz + r);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    double dx = x - cx, dz = z - cz;
                    double d2 = dx * dx + dz * dz;
                    if (d2 <= r * r && d2 > ri * ri) cells[(x, y, z)] = id;
                }
    }

    // ===== ボイラ建屋・タービン建屋 =====
    // 骨組みは共通で、boiler のときだけ架構を密にして上部から煙道を出す。
    private static void BuildPowerHall(Dictionary<(int x, int y, int z), string> cells, Props props,
        StructureSpec spec, Palette p, bool boiler)
    {
        int len = Clamp(spec.PowerLength ?? (boiler ? 45 : 60), 10, 120);
        int wid = Clamp(spec.PowerWidth ?? (boiler ? 30 : 36), 10, 120);
        int hgt = Clamp(spec.PowerHeight ?? (boiler ? 40 : 32), 8, 80);
        int bay = Clamp(spec.PowerBay ?? 9, 3, 24);
        int lv = Clamp(spec.PowerLevels ?? (boiler ? 4 : 2), 1, 8);

        // 基礎スラブ。上載荷重が大きい建屋は4m厚を打つので、外周へ1マス出して厚く見せる。
        Fill(cells, -1, len, 0, 0, -1, wid, p.Base);

        // 外壁。
        Fill(cells, 0, len - 1, 1, hgt - 1, 0, 0, p.Shell);
        Fill(cells, 0, len - 1, 1, hgt - 1, wid - 1, wid - 1, p.Shell);
        Fill(cells, 0, 0, 1, hgt - 1, 1, wid - 2, p.Shell);
        Fill(cells, len - 1, len - 1, 1, hgt - 1, 1, wid - 2, p.Shell);

        // 見え掛かりの鉄骨柱。柱間隔ごとに四周へ立て、四隅は必ず柱にする。
        for (int x = 0; x < len; x += bay)
        {
            Fill(cells, x, x, 1, hgt - 1, 0, 0, p.Accent);
            Fill(cells, x, x, 1, hgt - 1, wid - 1, wid - 1, p.Accent);
        }
        for (int z = 0; z < wid; z += bay)
        {
            Fill(cells, 0, 0, 1, hgt - 1, z, z, p.Accent);
            Fill(cells, len - 1, len - 1, 1, hgt - 1, z, z, p.Accent);
        }
        Fill(cells, len - 1, len - 1, 1, hgt - 1, 0, 0, p.Accent);
        Fill(cells, len - 1, len - 1, 1, hgt - 1, wid - 1, wid - 1, p.Accent);
        Fill(cells, 0, 0, 1, hgt - 1, wid - 1, wid - 1, p.Accent);

        // 採光帯。壁の上部に1段まわす。柱の位置は避ける。
        if (spec.PowerLouver)
        {
            int y = Math.Max(2, hgt - 5);
            for (int x = 1; x < len - 1; x++)
                if (x % bay != 0)
                {
                    cells[(x, y, 0)] = p.Glaze;
                    cells[(x, y, wid - 1)] = p.Glaze;
                }
            for (int z = 1; z < wid - 1; z++)
                if (z % bay != 0)
                {
                    cells[(0, y, z)] = p.Glaze;
                    cells[(len - 1, y, z)] = p.Glaze;
                }
        }

        // 運転床・中間床。段数で割って入れる。
        for (int i = 1; i < lv; i++)
        {
            int y = 1 + (hgt - 1) * i / lv;
            Fill(cells, 1, len - 2, y, y, 1, wid - 2, p.Deck);
        }

        // 内部の鉄骨架構。ボイラ建屋は本体を吊るので柱間隔ごと、タービン建屋は倍の間隔。
        int step = boiler ? bay : bay * 2;
        for (int x = bay; x < len - 1; x += step)
            for (int z = bay; z < wid - 1; z += step)
                Fill(cells, x, x, 1, hgt - 1, z, z, p.Blade);

        // 屋根とパラペット。
        Fill(cells, 0, len - 1, hgt, hgt, 0, wid - 1, p.Roof);
        for (int x = 0; x < len; x++)
        {
            cells[(x, hgt + 1, 0)] = p.Rail;
            cells[(x, hgt + 1, wid - 1)] = p.Rail;
        }
        for (int z = 0; z < wid; z++)
        {
            cells[(0, hgt + 1, z)] = p.Rail;
            cells[(len - 1, hgt + 1, z)] = p.Rail;
        }

        // 天井クレーン。走行梁を両側の壁沿いに通し、横行するガーダと巻上機を架ける。
        if (spec.PowerCrane)
        {
            int y = hgt - 3;
            Fill(cells, 1, len - 2, y, y, 1, 1, p.Blade);
            Fill(cells, 1, len - 2, y, y, wid - 2, wid - 2, p.Blade);
            int gx = Math.Max(2, len / 3);
            Fill(cells, gx, gx + 1, y + 1, y + 1, 1, wid - 2, p.Blade);
            Fill(cells, gx, gx + 1, y - 1, y, wid / 2, wid / 2, p.Accent);
        }

        // 機器搬入口。妻側（-x 面）に大扉を開け、まぐさを入れる。
        if (spec.PowerGate)
        {
            int ow = Math.Min(wid - 4, 8), oh = Math.Min(hgt - 3, 10);
            int z0 = (wid - ow) / 2;
            for (int y = 1; y <= oh; y++)
                for (int z = z0; z < z0 + ow; z++)
                    cells.Remove((0, y, z));
            Fill(cells, 0, 0, oh + 1, oh + 1, z0 - 1, z0 + ow, p.Accent);
        }

        // ボイラ建屋は上部から煙道を出す。煙突・タービン建屋と繋ぐ位置の目印になる。
        if (boiler)
        {
            int d = Math.Max(3, Math.Min(6, wid / 3));
            int y = Math.Max(2, hgt - 8);
            int z0 = (wid - d) / 2;
            Fill(cells, len, len + 5, y, y + d - 1, z0, z0 + d - 1, p.Accent);
        }

        // 付属棟（管理・電気室）。長手の側面に低く付ける。建屋の壁を4面目に使う。
        if (spec.PowerAnnex)
        {
            int ah = Math.Max(4, hgt / 3), aw = 8;
            int z1 = wid + aw - 1;
            Fill(cells, 2, len - 3, 0, 0, wid, z1, p.Base);
            Fill(cells, 2, len - 3, 1, ah - 1, z1, z1, p.Shell);
            Fill(cells, 2, 2, 1, ah - 1, wid, z1, p.Shell);
            Fill(cells, len - 3, len - 3, 1, ah - 1, wid, z1, p.Shell);
            Fill(cells, 2, len - 3, ah, ah, wid, z1, p.Roof);
            for (int x = 5; x < len - 4; x += 4)
                for (int y = 2; y <= Math.Min(3, ah - 1); y++)
                    cells[(x, y, z1)] = p.Glaze;
            cells.Remove((3, 1, z1));
            cells.Remove((3, 2, z1));
        }

        // 航空障害灯。地上高60m以上で必要。
        if (spec.PowerLight && hgt >= 60)
        {
            cells[(0, hgt + 1, 0)] = p.Light;
            cells[(len - 1, hgt + 1, 0)] = p.Light;
            cells[(0, hgt + 1, wid - 1)] = p.Light;
            cells[(len - 1, hgt + 1, wid - 1)] = p.Light;
        }
    }

    // ===== 煙突 =====
    // 外筒は底部から頂部へ直線でテーパを付け、内筒（フルー）を通す。
    private static void BuildStack(Dictionary<(int x, int y, int z), string> cells, Props props,
        StructureSpec spec, Palette p)
    {
        int hgt = Clamp(spec.PowerHeight ?? 120, 10, 240);
        int db = Clamp(spec.PowerDiameter ?? 14, 3, 40);
        int dt = Clamp(spec.PowerTopDiameter ?? 6, 2, db);
        int flues = Clamp(spec.PowerCount ?? 2, 0, 4);

        double rb = db / 2.0, rt = dt / 2.0;
        ShellDisc(cells, 0, 0, rb + 2.0, 0, 0, p.Base);

        // 内筒は径を一定にする。集合煙突では頂部の口径に収まるよう寄せて並べる。
        double ri = flues <= 1 ? Math.Max(1.0, rt - 1.0) : Math.Max(1.0, rt / 2.2);
        double rc = flues <= 1 ? 0.0 : Math.Max(0.0, rt - ri - 0.5);

        for (int y = 1; y <= hgt; y++)
        {
            double r = StackRadius(rb, rt, hgt, y);
            ShellRing(cells, 0, 0, r, y, y, y == hgt ? p.Accent : p.Shell);
            for (int i = 0; i < flues; i++)
            {
                double a = 2 * Math.PI * i / Math.Max(1, flues);
                ShellRing(cells, rc * Math.Cos(a), rc * Math.Sin(a), ri, y, y, p.Accent);
            }
        }

        // 踊り場。20マスごとに外へ張り出す。はしごより先に置いて、はしごで抜く。
        if (spec.PowerLadder)
        {
            for (int y = 20; y < hgt; y += 20)
            {
                double r = StackRadius(rb, rt, hgt, y);
                ShellRing(cells, 0, 0, r + 1.6, y, y, p.Deck);
                ShellRing(cells, 0, 0, r + 1.6, y + 1, y + 1, p.Rail);
            }
            for (int y = 1; y <= hgt; y++)
            {
                double r = StackRadius(rb, rt, hgt, y);
                var key = ((int)Math.Round(r) + 1, y, 0);
                cells[key] = LadderId;
                props[key] = new Dictionary<string, string> { ["facing"] = "east" };
            }
        }

        // 航空障害灯。60m以上で必要で、高いものは段を増やす。
        if (spec.PowerLight && hgt >= 60)
            for (int y = hgt; y >= 45; y -= 45)
            {
                double r = StackRadius(rb, rt, hgt, y);
                for (int i = 0; i < 4; i++)
                {
                    double a = Math.PI / 2 * i;
                    cells[((int)Math.Round(r * Math.Cos(a)), y, (int)Math.Round(r * Math.Sin(a)))] = p.Light;
                }
            }
    }

    private static double StackRadius(double rb, double rt, int hgt, int y)
    {
        double f = (y - 1) / (double)Math.Max(1, hgt - 1);
        return rb + (rt - rb) * f;
    }

    // ===== 自然通風冷却塔 =====
    // 喉部を全高の3/4に置き、上下を別の双曲線で結ぶ。底部・喉部・頂部の径は実機どおり。
    private static void BuildCoolingTower(Dictionary<(int x, int y, int z), string> cells, Props props,
        StructureSpec spec, Palette p)
    {
        int hgt = Clamp(spec.PowerHeight ?? 100, 20, 220);
        int db = Clamp(spec.PowerDiameter ?? 76, 12, 200);
        int dth = Clamp(spec.PowerThroat ?? 49, 6, db);
        int dtp = Clamp(spec.PowerTopDiameter ?? 52, dth, db);
        int inlet = Clamp(spec.PowerInlet ?? 9, 2, Math.Max(2, hgt / 3));
        int cols = Clamp(spec.PowerCount ?? 24, 4, 96);

        double rb = db / 2.0, rt = dth / 2.0, rp = dtp / 2.0;
        double yt = inlet + (hgt - inlet) * 0.75;
        double bl = Bhyp(rb, rt, yt - inlet);
        double bu = Bhyp(rp, rt, hgt - yt);

        // 水盤。堤を回して中に水を張る。
        ShellDisc(cells, 0, 0, rb + 2.0, 0, 0, p.Base);
        if (spec.PowerBasin)
        {
            ShellRing(cells, 0, 0, rb + 2.0, 1, 2, p.Base);
            ShellDisc(cells, 0, 0, rb + 1.0, 1, 1, WaterId);
        }

        // 双曲面のシェル。
        for (int y = inlet; y <= hgt; y++)
        {
            double r = HyperRadius(rt, yt, bl, bu, y);
            ShellRing(cells, 0, 0, r, y, y, y == hgt ? p.Accent : p.Shell);
        }

        // 斜め柱。地表の堤からシェル下端へ、1組を2本のV字で立てる。
        double rin = HyperRadius(rt, yt, bl, bu, inlet);
        double off = Math.PI / cols * 0.8;
        for (int i = 0; i < cols; i++)
        {
            double a = 2 * Math.PI * i / cols;
            RakerColumn(cells, a - off, a, rb + 1.0, rin, inlet, p.Accent);
            RakerColumn(cells, a + off, a, rb + 1.0, rin, inlet, p.Accent);
        }

        // 散水部と充填材。空気取入口の上を格子で塞ぐ。
        ShellDisc(cells, 0, 0, rin - 1.0, inlet + 1, inlet + 1, p.Lattice);

        if (spec.PowerLight && hgt >= 60)
            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI / 2 * i;
                cells[((int)Math.Round(rp * Math.Cos(a)), hgt, (int)Math.Round(rp * Math.Sin(a)))] = p.Light;
            }
    }

    // 双曲線 r(y) = rt*sqrt(1+((y-yt)/b)^2)。喉部の上下で b を分ける。
    private static double HyperRadius(double rt, double yt, double bl, double bu, int y)
    {
        double b = y <= yt ? bl : bu;
        double d = (y - yt) / b;
        return rt * Math.Sqrt(1.0 + d * d);
    }

    // 端の半径 ro・喉半径 rt・その間の高さ dy から双曲線の b を出す。
    private static double Bhyp(double ro, double rt, double dy)
    {
        double k = (ro / rt) * (ro / rt) - 1.0;
        if (k <= 1e-6 || dy <= 0) return 1e9;
        return dy / Math.Sqrt(k);
    }

    // 地表（角度 a0・半径 r0）からシェル下端（角度 a1・半径 r1）へ引く1マス幅の斜め柱。
    private static void RakerColumn(Dictionary<(int x, int y, int z), string> cells,
        double a0, double a1, double r0, double r1, int top, string id)
    {
        int steps = Math.Max(4, top * 4);
        for (int k = 0; k <= steps; k++)
        {
            double f = k / (double)steps;
            double a = a0 + (a1 - a0) * f;
            double r = r0 + (r1 - r0) * f;
            int y = 1 + (int)Math.Round((top - 1) * f);
            cells[((int)Math.Round(r * Math.Cos(a)), y, (int)Math.Round(r * Math.Sin(a)))] = id;
        }
    }

    // ===== 原子炉格納容器 =====
    private static void BuildContainment(Dictionary<(int x, int y, int z), string> cells, Props props,
        StructureSpec spec, Palette p)
    {
        bool box = string.Equals((spec.PowerShape ?? "cylinder").Trim(), "box",
            StringComparison.OrdinalIgnoreCase);
        int d = Clamp(spec.PowerDiameter ?? 40, 12, 90);
        int h = Clamp(spec.PowerHeight ?? 30, 8, 80);
        int wall = Clamp(spec.PowerWall ?? 2, 1, 5);

        if (box)
        {
            // BWR の原子炉建屋。角形の厚壁で、上部に燃料取替床が入る。
            int o = -(d / 2);
            Fill(cells, o - 1, o + d, 0, 0, o - 1, o + d, p.Base);
            for (int k = 0; k < wall; k++)
            {
                Fill(cells, o + k, o + d - 1 - k, 1, h - 1, o + k, o + k, p.Shell);
                Fill(cells, o + k, o + d - 1 - k, 1, h - 1, o + d - 1 - k, o + d - 1 - k, p.Shell);
                Fill(cells, o + k, o + k, 1, h - 1, o + k, o + d - 1 - k, p.Shell);
                Fill(cells, o + d - 1 - k, o + d - 1 - k, 1, h - 1, o + k, o + d - 1 - k, p.Shell);
            }
            Fill(cells, o, o + d - 1, h, h, o, o + d - 1, p.Roof);

            int fy = Math.Max(3, h - 10);
            Fill(cells, o + wall, o + d - 1 - wall, fy, fy, o + wall, o + d - 1 - wall, p.Deck);

            // ドライウェル（内部の円筒）と使用済燃料プールの開口。
            ShellRing(cells, 0, 0, d * 0.30, 1, fy - 1, p.Accent);
            int pw = Math.Max(4, d / 6);
            for (int x = o + wall + 1; x <= o + wall + pw; x++)
                for (int z = o + wall + 1; z <= o + wall + pw; z++)
                    cells.Remove((x, fy, z));

            // 天井クレーン。
            if (spec.PowerCrane)
            {
                int y = h - 3;
                Fill(cells, o + wall, o + d - 1 - wall, y, y, o + wall, o + wall, p.Blade);
                Fill(cells, o + wall, o + d - 1 - wall, y, y, o + d - 1 - wall, o + d - 1 - wall, p.Blade);
                Fill(cells, 0, 1, y + 1, y + 1, o + wall, o + d - 1 - wall, p.Blade);
            }

            if (spec.PowerGate)
            {
                for (int y = 1; y <= 7; y++)
                    for (int x = o + d - 1 - wall; x <= o + d - 1; x++)
                        for (int z = -2; z <= 2; z++)
                            cells.Remove((x, y, z));
                Fill(cells, o + d - 1, o + d - 1, 1, 8, -3, 3, p.Accent);
            }
        }
        else
        {
            // PWR の格納容器。円筒に半球ドームを載せる。
            double r = d / 2.0;
            ShellDisc(cells, 0, 0, r + 2.0, 0, 0, p.Base);
            for (int k = 0; k < wall; k++) ShellRing(cells, 0, 0, r - k, 1, h, p.Shell);
            ShellDisc(cells, 0, 0, r - wall, 1, 1, p.Deck);

            int hd = Math.Max(2, (int)Math.Round(r));
            for (int k = 1; k <= hd; k++)
            {
                double rr = r * Math.Sqrt(Math.Max(0.0, 1.0 - (double)k * k / (hd * (double)hd)));
                if (rr < 1.5)
                {
                    ShellDisc(cells, 0, 0, Math.Max(1.0, rr + 1.0), h + k, h + k, p.Roof);
                    break;
                }
                for (int j = 0; j < wall; j++) ShellRing(cells, 0, 0, rr - j, h + k, h + k, p.Roof);
            }

            // 機器搬入用のエアロック。
            if (spec.PowerGate)
            {
                for (int y = 1; y <= 7; y++)
                    for (int x = (int)Math.Floor(r) - wall; x <= (int)Math.Ceiling(r); x++)
                        for (int z = -2; z <= 2; z++)
                            cells.Remove((x, y, z));
                for (int y = 1; y <= 8; y++)
                {
                    cells[((int)Math.Round(r), y, -3)] = p.Accent;
                    cells[((int)Math.Round(r), y, 3)] = p.Accent;
                }
            }
        }

        // 補助建屋。-x 側へ低く付ける。
        if (spec.PowerAnnex)
        {
            int ah = Math.Max(5, (int)(h * 0.6));
            int aw = Math.Max(8, d / 3), al = Math.Max(10, d / 2);
            int x1 = -(int)Math.Round(d / 2.0) - 1, x0 = x1 - aw + 1;
            int z0 = -al / 2, z1 = al / 2;
            Fill(cells, x0, x1, 0, 0, z0, z1, p.Base);
            Fill(cells, x0, x1, 1, ah - 1, z0, z0, p.Shell);
            Fill(cells, x0, x1, 1, ah - 1, z1, z1, p.Shell);
            Fill(cells, x0, x0, 1, ah - 1, z0, z1, p.Shell);
            Fill(cells, x0, x1, ah, ah, z0, z1, p.Roof);
            for (int z = z0 + 2; z < z1; z += 3)
                for (int y = 2; y <= Math.Min(3, ah - 1); y++)
                    cells[(x0, y, z)] = p.Glaze;
        }

        if (spec.PowerLight && h >= 60)
            cells[(0, h + Math.Max(2, d / 2) + 1, 0)] = p.Light;
    }

    // ===== 変電ヤード =====
    private static void BuildSwitchyard(Dictionary<(int x, int y, int z), string> cells, Props props,
        StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.PowerLength ?? 60, 16, 200);
        int wid = Clamp(spec.PowerWidth ?? 40, 16, 200);
        int gh = Clamp(spec.PowerHeight ?? 16, 6, 40);
        int bays = Clamp(spec.PowerCount ?? 4, 1, 16);
        int trs = Clamp(spec.PowerTransformers ?? 2, 0, 8);

        // 敷地（砕石舗装）。
        Fill(cells, 0, len - 1, 0, 0, 0, wid - 1, p.Base);

        // 引留めの門型。回線数ぶん等間隔に立て、下に断路器とがいしを並べる。
        int span = Math.Max(6, len / Math.Max(1, bays));
        for (int i = 0; i < bays; i++)
        {
            int x = 4 + i * span;
            if (x >= len - 4) break;
            for (int dx = 0; dx <= 1; dx++)
            {
                Fill(cells, x + dx, x + dx, 1, gh, 4, 4, p.Blade);
                Fill(cells, x + dx, x + dx, 1, gh, wid - 5, wid - 5, p.Blade);
            }
            Fill(cells, x, x + 1, gh, gh, 4, wid - 5, p.Blade);
            for (int z = 9; z < wid - 8; z += 8)
            {
                Fill(cells, x, x, 1, 4, z, z, p.Accent);
                cells[(x, 5, z)] = p.Glaze;
            }
            if (spec.PowerLight) cells[(x, gh + 1, 4)] = p.Light;
        }

        // 母線。三相ぶんを長手に渡す。
        int pitch = Math.Max(2, (wid - 12) / 3);
        for (int i = 0; i < 3; i++)
        {
            int z = 6 + i * pitch;
            if (z >= wid - 4) break;
            Fill(cells, 2, len - 3, gh - 2, gh - 2, z, z, p.Lattice);
        }

        // 変圧器。防火壁で仕切り、放熱器とブッシングを付ける。
        for (int i = 0; i < trs; i++)
        {
            int x = 5 + i * 14;
            if (x + 9 >= len) break;
            int z = wid - 12;
            Fill(cells, x, x + 7, 1, 5, z, z + 5, p.Accent);
            Fill(cells, x, x + 7, 6, 6, z, z + 5, p.Deck);
            Fill(cells, x, x + 7, 1, 4, z + 6, z + 6, p.Rail);
            Fill(cells, x - 1, x + 8, 1, 7, z - 2, z - 2, p.Shell);
            cells[(x + 2, 7, z + 2)] = p.Lattice;
            cells[(x + 5, 7, z + 3)] = p.Lattice;
        }

        // 制御建屋。
        int cl = Math.Min(14, len - 6), cw = Math.Max(6, Math.Min(9, wid / 4));
        int bx0 = 2, bx1 = 2 + cl - 1, bz0 = 1, bz1 = cw;
        Fill(cells, bx0, bx1, 1, 4, bz0, bz0, p.Shell);
        Fill(cells, bx0, bx1, 1, 4, bz1, bz1, p.Shell);
        Fill(cells, bx0, bx0, 1, 4, bz0, bz1, p.Shell);
        Fill(cells, bx1, bx1, 1, 4, bz0, bz1, p.Shell);
        Fill(cells, bx0, bx1, 5, 5, bz0, bz1, p.Roof);
        Fill(cells, bx0 + 1, bx1 - 1, 1, 1, bz0 + 1, bz1 - 1, p.Deck);
        for (int x = bx0 + 3; x < bx1; x += 3)
        {
            cells[(x, 2, bz1)] = p.Glaze;
            cells[(x, 3, bz1)] = p.Glaze;
        }
        cells.Remove((bx0 + 1, 1, bz1));
        cells.Remove((bx0 + 1, 2, bz1));

        // 外周フェンス。長手の中央に門を開ける。
        if (spec.PowerFence)
        {
            for (int x = 0; x < len; x++)
                for (int y = 1; y <= 3; y++)
                {
                    cells[(x, y, 0)] = p.Rail;
                    cells[(x, y, wid - 1)] = p.Rail;
                }
            for (int z = 0; z < wid; z++)
                for (int y = 1; y <= 3; y++)
                {
                    cells[(0, y, z)] = p.Rail;
                    cells[(len - 1, y, z)] = p.Rail;
                }
            for (int x = 0; x < len; x += 6)
                Fill(cells, x, x, 1, 3, 0, 0, p.Accent);
            for (int x = 0; x < len; x += 6)
                Fill(cells, x, x, 1, 3, wid - 1, wid - 1, p.Accent);
            int gx0 = len / 2 - 3;
            for (int x = gx0; x < gx0 + 6; x++)
                for (int y = 1; y <= 3; y++)
                    cells.Remove((x, y, wid - 1));
        }
    }
}
