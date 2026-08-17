using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 風車・水車。IndustryExpander の partial。
//
// 回転面（ローター面・水輪面）は canonical で x-y 平面に置き、厚みを z 方向に取る。
// canonical の正面は +z（南）で、水車の流れは +x（東）へ向かう。全体の向きは Build 末尾の
// Rotate（facade_face）で回す。
//
// 実寸の出典。
//   近代風車 … 2,000kW級はタワー高さ78m・タワー直径4.3m・ローター直径86m
//              〔上関町 風力発電所 諸元〕。ナセルは長さ10.4m・幅3.5m・輸送時高さ4m、
//              設置時5.4m〔Vestas V90-2.0MW データシート〕。ハブ高さ60〜100m、
//              翼端到達高100〜150m〔日本電機工業会 風車の構造〕。
//   オランダ型 … 下太りの塔身に回転するキャップ（帽子）を載せ、4枚の格子羽根を付ける。
//              外周に作業用のギャラリーを回す形式がある。
//   水車 … 上掛けの実施例で水輪 直径3m・ブレード幅2.5m〔全国小水力利用推進協議会〕、
//          胸掛けで直径3.0m・内幅0.4m〔農業農村工学会〕。世界最大の実働水車 Laxey Wheel は
//          直径22.1m・幅1.8m・バケット192個〔Laxey Wheel〕。
//   水車小屋 … 五間×二間半（約9m×4.5m）、水輪は直径二間（約3.6m）・幅三尺（約0.9m）
//          〔高根沢町史 水車の使用〕。水輪は小屋の外に置き、軸が壁を貫いて中へ入る。
//
// 丸めの扱い。1マス=1m なので、実寸1m未満の水輪の幅・羽根の厚みは1マスとする
// （実寸より厚い方向の丸め）。
public static partial class IndustryExpander
{
    // ===== 回転面（x-y 平面）の描画 =====
    // 厚みぶんを z 方向へ並べる。地面より下（y<0）は置かない。
    private static void Put(Dictionary<(int x, int y, int z), string> cells,
        int x, int y, int z0, int z1, string id)
    {
        if (y < 0) return;
        for (int z = z0; z <= z1; z++) cells[(x, y, z)] = id;
    }

    // 厚み1マスの円環（水輪のリム）。
    private static void RimXY(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cy, double r, int z0, int z1, string id)
    {
        if (r < 1.5) return;
        int x0 = (int)Math.Floor(cx - r), x1 = (int)Math.Ceiling(cx + r);
        int y0 = (int)Math.Floor(cy - r), y1 = (int)Math.Ceiling(cy + r);
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                double dx = x - cx, dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist <= r && dist > r - 1.0) Put(cells, x, y, z0, z1, id);
            }
    }

    // 半径方向の板（翼・羽根・スポーク）。r0 から r1 へ 0.5 刻みで進み、
    // 弦長を根元 chord0 から先端 chord1 へ細める。
    // lattice=true で骨組みだけ残す（オランダ型の羽根は帆桟の格子）。
    private static void BladeXY(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cy, double angDeg, double r0, double r1,
        double chord0, double chord1, int z0, int z1, string id, bool lattice)
    {
        if (r1 <= r0) return;
        double a = angDeg * Math.PI / 180.0;
        double ux = Math.Cos(a), uy = Math.Sin(a);
        double px = -uy, py = ux;

        for (double t = r0; t <= r1 + 1e-9; t += 0.5)
        {
            double f = (t - r0) / (r1 - r0);
            double chord = chord0 + (chord1 - chord0) * f;
            double half = (chord - 1.0) / 2.0;
            for (double s = -half; s <= half + 1e-9; s += 0.5)
            {
                if (lattice)
                {
                    bool edge = Math.Abs(Math.Abs(s) - half) < 0.26;
                    bool rib = Math.Abs(t / 2.0 - Math.Round(t / 2.0)) < 0.13;
                    if (!edge && !rib) continue;
                }
                int x = (int)Math.Round(cx + ux * t + px * s);
                int y = (int)Math.Round(cy + uy * t + py * s);
                Put(cells, x, y, z0, z1, id);
            }
        }
    }

    // ===== 水平な円（実数の中心・半径）=====
    // テーパーする塔身は段ごとに直径が変わるので、整数の箱ではなく実数中心で描く。
    private static void HRing(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cz, double r, int y, string id)
    {
        if (y < 0) return;
        if (r < 1.0)
        {
            cells[((int)Math.Round(cx), y, (int)Math.Round(cz))] = id;
            return;
        }
        int a0 = (int)Math.Floor(cx - r), a1 = (int)Math.Ceiling(cx + r);
        int b0 = (int)Math.Floor(cz - r), b1 = (int)Math.Ceiling(cz + r);
        for (int x = a0; x <= a1; x++)
            for (int z = b0; z <= b1; z++)
            {
                double dx = x - cx, dz = z - cz;
                double dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist <= r && dist > r - 1.0) cells[(x, y, z)] = id;
            }
    }

    private static void HDisc(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cz, double r, int y0, int y1, string id)
    {
        if (r < 0.5) return;
        int a0 = (int)Math.Floor(cx - r), a1 = (int)Math.Ceiling(cx + r);
        int b0 = (int)Math.Floor(cz - r), b1 = (int)Math.Ceiling(cz + r);
        for (int y = y0; y <= y1; y++)
        {
            if (y < 0) continue;
            for (int x = a0; x <= a1; x++)
                for (int z = b0; z <= b1; z++)
                {
                    double dx = x - cx, dz = z - cz;
                    if (dx * dx + dz * dz <= r * r) cells[(x, y, z)] = id;
                }
        }
    }

    // z 軸方向の丸棒（風車の主軸・水車の車軸）。
    private static void AxleZ(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cy, double r, int z0, int z1, string id)
    {
        int a0 = (int)Math.Floor(cx - r), a1 = (int)Math.Ceiling(cx + r);
        int b0 = (int)Math.Floor(cy - r), b1 = (int)Math.Ceiling(cy + r);
        for (int x = a0; x <= a1; x++)
            for (int y = b0; y <= b1; y++)
            {
                double dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                Put(cells, x, y, z0, z1, id);
            }
    }

    // ===== 風車 =====
    private static void BuildWindTurbine(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        if ((spec.IndustryMillType ?? "modern").Trim().ToLowerInvariant() == "dutch")
        {
            BuildDutchMill(cells, spec, p);
            return;
        }

        int th = Clamp(spec.IndustryTowerHeight ?? 78, 6, 120);
        int tb = Clamp(spec.IndustryTowerBase ?? 4, 3, 24);
        int blades = Clamp(spec.IndustryBladeCount ?? 3, 1, 8);
        int thick = Clamp(spec.IndustryRotorWidth ?? 1, 1, 4);
        int ang = Clamp(spec.IndustryRotorAngle ?? 0, 0, 359);

        // ナセル。78m級のタワーで長さ10m級（V90-2.0MW は10.4m×3.5m×高さ4m）。
        int nl = Math.Max(4, (int)Math.Round(th / 7.5));
        int nh = Math.Max(3, Math.Min(6, tb));
        int nw = Math.Max(3, tb - 1);
        int hubY = th + nh / 2;

        // ローター直径。翼端が地面へ潜らないようハブ高さから抑える。
        int rd = Clamp(spec.IndustryRotorDiameter ?? 86, 6, 240);
        rd = Math.Min(rd, 2 * (hubY - 2));
        double R = rd / 2.0;

        // 円形フーチング。実物は直径15〜20m級。
        int pad = Clamp(tb * 4, 6, 32);
        HDisc(cells, 0, 0, pad / 2.0, 0, 0, p.Base);

        // テーパーする鋼製タワー。基部 tb、頂部は tb-2（実物 4.3m→2.5m級）。
        double topD = Math.Max(2.0, tb - 2.0);
        for (int y = 1; y <= th; y++)
        {
            double f = th <= 1 ? 1.0 : (y - 1) / (double)(th - 1);
            HRing(cells, 0, 0, (tb + (topD - tb) * f) / 2.0, y, p.Shell);
        }

        // 塔の出入口。基部の環を2マス抜く。
        if (spec.IndustryManhole)
        {
            int e = (int)Math.Floor(tb / 2.0);
            cells.Remove((0, 1, e));
            cells.Remove((0, 2, e));
        }

        // ナセル。ローター面（+z 側）からタワー背後（-z 側）へ伸びる箱。
        int nx0 = -((nw - 1) / 2), nx1 = nx0 + nw - 1;
        int ny0 = hubY - nh / 2, ny1 = ny0 + nh - 1;
        if (spec.IndustryNacelle)
        {
            Fill(cells, nx0, nx1, ny0, ny1, -nl, -1, p.Accent);
            cells[(0, ny1 + 1, -Math.Max(1, nl / 2))] = p.Light;   // 航空障害灯
        }

        // ハブとローター。ローター面は z=0..thick-1。
        AxleZ(cells, 0, hubY, Math.Max(1.0, tb / 3.0), -1, thick - 1, p.Accent);
        for (int i = 0; i < blades; i++)
            BladeXY(cells, 0, hubY, ang + 360.0 * i / blades,
                Math.Max(1.5, tb / 2.0), R, Math.Max(2.0, tb), 1.0,
                0, thick - 1, p.Blade, false);
    }

    // オランダ型。下太りの塔身＋回転キャップ＋格子羽根。
    private static void BuildDutchMill(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int th = Clamp(spec.IndustryTowerHeight ?? 20, 6, 48);
        int tb = Clamp(spec.IndustryTowerBase ?? 10, 5, 24);
        int sails = Clamp(spec.IndustryBladeCount ?? 4, 2, 8);
        int ang = Clamp(spec.IndustryRotorAngle ?? 0, 0, 359);
        double R = Clamp(spec.IndustryRotorDiameter ?? 26, 6, 80) / 2.0;
        double topD = Math.Max(4.0, tb - 4.0);

        HDisc(cells, 0, 0, tb / 2.0 + 1.0, 0, 0, p.Base);

        for (int y = 1; y <= th; y++)
        {
            double f = th <= 1 ? 1.0 : (y - 1) / (double)(th - 1);
            HRing(cells, 0, 0, (tb + (topD - tb) * f) / 2.0, y, p.Shell);
        }

        // 入口。
        if (spec.IndustryManhole)
        {
            int e = (int)Math.Floor(tb / 2.0);
            cells.Remove((0, 1, e));
            cells.Remove((0, 2, e));
        }

        // 採光窓。4マスおきに前後へ入れる。
        for (int y = 5; y < th; y += 4)
        {
            double f = (y - 1) / (double)Math.Max(1, th - 1);
            int e = (int)Math.Floor((tb + (topD - tb) * f) / 2.0);
            cells[(0, y, e)] = p.Glaze;
            cells[(0, y, -e)] = p.Glaze;
        }

        // 外周ギャラリー（作業デッキ）。
        if (spec.IndustryBalcony && th >= 10)
        {
            int gy = th / 3;
            double f = (gy - 1) / (double)Math.Max(1, th - 1);
            double gr = (tb + (topD - tb) * f) / 2.0 + 1.0;
            HRing(cells, 0, 0, gr, gy, p.Deck);
            HRing(cells, 0, 0, gr, gy + 1, p.Rail);
        }

        // キャップ（回転する帽子）。高さは頂部直径の半分でドーム形。
        int capH = Math.Max(2, (int)Math.Round(topD / 2.0));
        for (int k = 0; k <= capH; k++)
        {
            double r = (topD / 2.0) *
                Math.Sqrt(Math.Max(0.0, 1.0 - (double)k * k / ((double)capH * capH)));
            int y = th + 1 + k;
            if (r < 1.0 || k == capH)
            {
                HDisc(cells, 0, 0, Math.Max(1.0, r), y, y, p.Cap);
                break;
            }
            HRing(cells, 0, 0, r, y, p.Cap);
        }

        // 風車軸と羽根。キャップの +z 側（正面）へ突き出す。
        int hubY = th + 1 + Math.Max(1, capH / 2);
        int zFront = (int)Math.Round(topD / 2.0) + 2;
        AxleZ(cells, 0, hubY, 1.0, 0, zFront, p.Accent);
        for (int i = 0; i < sails; i++)
            BladeXY(cells, 0, hubY, ang + 360.0 * i / sails, 2.0, R,
                4.0, 3.0, zFront, zFront, p.Blade, true);
    }

    // ===== 水車 =====
    private static void BuildWaterWheel(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int wd = Clamp(spec.IndustryRotorDiameter ?? 4, 3, 24);
        int ww = Clamp(spec.IndustryRotorWidth ?? 1, 1, 6);
        int paddles = Clamp(spec.IndustryBladeCount ?? 12, 4, 32);
        int ang = Clamp(spec.IndustryRotorAngle ?? 0, 0, 359);
        string type = (spec.IndustryWheelType ?? "overshot").Trim().ToLowerInvariant();
        bool over = type != "undershot";

        double R = wd / 2.0;
        int bottom = over ? 2 : 1;              // 下掛けは水輪の下端を水中へ入れる
        double cy = bottom + R;
        int z0 = 0, z1 = ww - 1;

        // 水路。川床 y=0、両側に護岸、水面は下掛けで2マス・他は1マス。
        int xLo = -(int)Math.Ceiling(R) - 12, xHi = (int)Math.Ceiling(R) + 4;
        int depth = over ? 1 : 2;
        Fill(cells, xLo, xHi, 0, 0, z0 - 1, z1 + 1, p.Base);
        for (int x = xLo; x <= xHi; x++)
            for (int y = 1; y <= depth; y++)
            {
                cells[(x, y, z0 - 1)] = p.Base;
                cells[(x, y, z1 + 1)] = p.Base;
                for (int z = z0; z <= z1; z++) cells[(x, y, z)] = WaterId;
            }

        // 水車小屋。水輪の +z 側に建て、軸が壁を貫いて中へ入る。
        int hz0 = z1 + 3, hz1 = hz0 + 4;
        if (spec.IndustryMillHouse) MillHouse(cells, -4, 4, hz0, hz1, 4, p);

        // 導水路。上流（-x 側）から水を運び、上掛けは頂部・胸掛けは軸の高さで落とす。
        if (spec.IndustryFlume && over)
        {
            int fy = type == "breast" ? (int)Math.Round(cy) : (int)Math.Round(cy + R);
            for (int x = xLo; x <= -1; x++)
            {
                for (int y = fy; y <= fy + 1; y++)
                {
                    cells[(x, y, z0 - 1)] = p.Base;
                    cells[(x, y, z1 + 1)] = p.Base;
                }
                for (int z = z0; z <= z1; z++)
                {
                    cells[(x, fy, z)] = p.Base;          // 底板
                    cells[(x, fy + 1, z)] = WaterId;     // 流水
                }
                // 支柱。水輪に当たらない位置だけ、4マスおきに川床まで落とす。
                if (x <= -(int)Math.Ceiling(R) - 1 && (x - xLo) % 4 == 0)
                    for (int y = 1; y < fy; y++) cells[(x, y, z0)] = p.Base;
            }
        }

        // 水輪。外リム・内リム・スポーク・羽根。
        RimXY(cells, 0, cy, R, z0, z1, p.Blade);
        if (R >= 4.0) RimXY(cells, 0, cy, R - 2.0, z0, z1, p.Blade);

        int spokes = Clamp(paddles / 2, 4, 16);
        for (int i = 0; i < spokes; i++)
            BladeXY(cells, 0, cy, ang + 360.0 * i / spokes, 1.5, R - 1.0,
                1.0, 1.0, z0, z1, p.Blade, false);

        for (int i = 0; i < paddles; i++)
        {
            double a = ang + 360.0 * i / paddles;
            double inner = Math.Max(1.5, R - 2.0);
            BladeXY(cells, 0, cy, a, inner, R - 0.5, 1.0, 1.0, z0, z1, p.Deck, false);

            if (over)
            {
                // 上掛け・胸掛けは水を溜めるバケットの底板を付ける。
                double rad = a * Math.PI / 180.0;
                int bx = (int)Math.Round(Math.Cos(rad) * inner - Math.Sin(rad));
                int by = (int)Math.Round(cy + Math.Sin(rad) * inner + Math.Cos(rad));
                Put(cells, bx, by, z0, z1, p.Deck);
            }
        }

        // 車軸と軸受け。軸は小屋の中まで伸ばす。
        int azEnd = spec.IndustryMillHouse ? hz0 + 1 : z1 + 3;
        AxleZ(cells, 0, cy, 1.0, z0 - 3, azEnd, p.Accent);
        int postTop = (int)Math.Round(cy) - 2;
        Fill(cells, -1, 1, 0, postTop, z0 - 3, z0 - 3, p.Base);
        if (!spec.IndustryMillHouse) Fill(cells, -1, 1, 0, postTop, z1 + 3, z1 + 3, p.Base);
    }

    // 水車小屋。五間×二間半（約9m×4.5m）級の切妻。棟は x 方向に走る。
    private static void MillHouse(Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int wallH, Palette p)
    {
        Fill(cells, x0, x1, 0, 0, z0, z1, p.Base);                  // 土間

        for (int y = 1; y <= wallH; y++)
        {
            Fill(cells, x0, x1, y, y, z0, z0, p.Shell);
            Fill(cells, x0, x1, y, y, z1, z1, p.Shell);
            Fill(cells, x0, x0, y, y, z0, z1, p.Shell);
            Fill(cells, x1, x1, y, y, z0, z1, p.Shell);
        }

        int cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        cells.Remove((cx, 1, z1));                                  // 入口
        cells.Remove((cx, 2, z1));
        cells[(x0, 2, cz)] = p.Glaze;                               // 採光窓
        cells[(x1, 2, cz)] = p.Glaze;

        int half = (z1 - z0) / 2;
        for (int k = 0; k <= half; k++)
        {
            int y = wallH + 1 + k;
            int a = z0 + k, b = z1 - k;
            for (int z = a; z <= b; z++)                            // 妻壁
            {
                cells[(x0, y, z)] = p.Shell;
                cells[(x1, y, z)] = p.Shell;
            }
            int ex0 = k == 0 ? x0 - 1 : x0;                         // 軒の出は最下段だけ
            int ex1 = k == 0 ? x1 + 1 : x1;
            for (int x = ex0; x <= ex1; x++)
            {
                cells[(x, y, a)] = p.Roof;
                cells[(x, y, b)] = p.Roof;
            }
        }
    }
}
