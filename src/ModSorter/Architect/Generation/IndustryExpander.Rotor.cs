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
    // 弦長を根元 chord0 から先端 chord1 へ細める。面は全ブロックで埋める。
    private static void BladeXY(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cy, double angDeg, double r0, double r1,
        double chord0, double chord1, int z0, int z1, string id)
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
                int x = (int)Math.Round(cx + ux * t + px * s);
                int y = (int)Math.Round(cy + uy * t + py * s);
                Put(cells, x, y, z0, z1, id);
            }
        }
    }

    // オランダ型の羽根1枚。縦框（whip）を全ブロックの1列で通し、その片側に帆桟と
    // 帆布を張る。帆布だけを透けるブロック（フェンス）にし、縁と3マスおきの桟は
    // 全ブロックにするので、遠目でも羽根の輪郭が分かる。
    //
    // 描く順は帆布→桟→框。丸めで座標が重なっても骨のほうが残る。
    private static void SailXY(Dictionary<(int x, int y, int z), string> cells,
        double cx, double cy, double angDeg, double r0, double r1, int chord, int z,
        string frame, string cloth)
    {
        if (r1 <= r0 || chord < 1) return;
        double a = angDeg * Math.PI / 180.0;
        double ux = Math.Cos(a), uy = Math.Sin(a);
        double px = -uy, py = ux;

        int steps = Math.Max(1, (int)Math.Ceiling((r1 - r0) * 2));
        for (int pass = 0; pass < 2; pass++)
            for (int s = 0; s <= steps; s++)
            {
                double t = r0 + (r1 - r0) * s / steps;
                bool bar = s % 6 == 0;                       // 帆桟。0.5刻みなので3マスおき
                for (int c = 0; c < chord; c++)
                {
                    bool frameCell = c == 0 || c == chord - 1 || bar;
                    if (pass == 0 && frameCell) continue;    // 1周目は帆布だけ
                    if (pass == 1 && !frameCell) continue;   // 2周目は骨だけ
                    int x = (int)Math.Round(cx + ux * t + px * c);
                    int y = (int)Math.Round(cy + uy * t + py * c);
                    Put(cells, x, y, z, z, frameCell ? frame : cloth);
                }
            }
    }

    // 3次元の直線。既にブロックがある座標は上書きしない（支線が構造体を食わない）。
    private static void Line3D(Dictionary<(int x, int y, int z), string> cells,
        int x0, int y0, int z0, int x1, int y1, int z1, string id)
    {
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Max(Math.Abs(y1 - y0), Math.Abs(z1 - z0)));
        if (steps <= 0) return;
        for (int s = 0; s <= steps; s++)
        {
            double f = s / (double)steps;
            int x = (int)Math.Round(x0 + (x1 - x0) * f);
            int y = (int)Math.Round(y0 + (y1 - y0) * f);
            int z = (int)Math.Round(z0 + (z1 - z0) * f);
            if (y < 0) continue;
            if (!cells.ContainsKey((x, y, z))) cells[(x, y, z)] = id;
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
    // 形式で分岐する。水平軸（近代・オランダ型）と垂直軸（ダリウス・直線翼・
    // ヘリカル・サボニウス）で組み立てがまるごと違う。
    private static void BuildWindTurbine(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        string type = (spec.IndustryMillType ?? "modern").Trim().ToLowerInvariant();
        switch (type)
        {
            case "dutch": BuildDutchMill(cells, spec, p); return;
            case "darrieus":
            case "gyromill":
            case "helical":
            case "savonius": BuildVawt(cells, spec, p, type); return;
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
                0, thick - 1, p.Blade);
    }

    // オランダ型。下太りの塔身＋回転キャップ＋帆を張った羽根。
    // 羽根は縦框（whip）を全ブロックで通し、その片側に帆桟（全ブロック）と
    // 帆布（フェンス等の透けるブロック）を張る。従来は縁と桟だけの格子だったため
    // 遠目に羽根が消えていた。
    private static void BuildDutchMill(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int th = Clamp(spec.IndustryTowerHeight ?? 20, 6, 48);
        int tb = Clamp(spec.IndustryTowerBase ?? 10, 5, 24);
        int sails = Clamp(spec.IndustryBladeCount ?? 4, 2, 8);
        int chord = Clamp(spec.IndustryRotorWidth ?? 4, 2, 8);
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

        // 十字の桁（cross tree）。框の根元同士を全ブロックで繋いで骨を見せる。
        for (int i = 0; i < sails; i++)
            BladeXY(cells, 0, hubY, ang + 360.0 * i / sails, 1.0, 3.0,
                1.0, 1.0, zFront, zFront, p.Blade);

        for (int i = 0; i < sails; i++)
            SailXY(cells, 0, hubY, ang + 360.0 * i / sails, 2.0, R, chord,
                zFront, p.Blade, p.Lattice);
    }

    // ===== 垂直軸型風車 =====
    // 主軸は canonical の原点に立て、翼は水平面（x-z）の角度で配る。
    // 風向に依存しない形式なので facade_face は翼の初期位置の基準にしかならない。
    //
    // 実寸の出典。
    //   ダリウス（φ型）… Cap-Chat の Éole は3.8MW・全高110m・ローター直径64m・
    //     高さ96m・2枚翼〔Möllerström, A historical review of VAWTs rated 100 kW or more〕。
    //   直線翼（H型・ジャイロミル）… 日本飛行機の運用試験機は直径4m・高さ4.5m・
    //     風速25m/sまで運転〔川崎重工 プレスリリース〕。別例でスパン2.4m・弦長0.30m・
    //     回転直径3.6m・3枚翼で約3kW。
    //   ヘリカル … H型の翼をらせんにして、枚数を増やさず起動性を上げる形式。
    //   サボニウス（S型ロータ）… 重なり比0.2〜0.3が最良、アスペクト比（高さ/直径）は
    //     2〜4。羽根2〜3枚で、3段に分けて60°ずつずらす〔大阪大学 サボニウス風車の
    //     数値解析による性能予測／浜島書店 サボニウス型風車風力発電機〕。
    //   浮体式 … SeaTwirl の30kW機はタービン直径10m・全高31m・水面上13m（水面下18m）、
    //     K-LINE の浮遊軸型 FAWT はロータ径9.3m・浮体径1.7m・20kW。
    private static void BuildVawt(Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, Palette p, string type)
    {
        int d = Clamp(spec.IndustryRotorDiameter ?? 64, 3, 120);
        int h = Clamp(spec.IndustryRotorHeight ?? 96, 3, 120);
        int blades = Clamp(spec.IndustryBladeCount ?? 2, 2, 6);
        int chord = Clamp(spec.IndustryRotorWidth ?? 2, 1, 8);
        int ang = Clamp(spec.IndustryRotorAngle ?? 0, 0, 359);
        int post = Clamp(spec.IndustryTowerHeight ?? 4, 0, 60);
        int shaft = Clamp(spec.IndustryTowerBase ?? 3, 1, 12);
        bool floating = spec.IndustryFloating;
        int draft = floating ? Clamp(spec.IndustryDraft ?? 18, 2, 60) : 0;

        double R = d / 2.0;
        int y0 = draft + post;      // ローター下端
        int top = y0 + h;           // 主軸の頂部

        if (floating)
        {
            // スパーブイ。海底の板と水を敷き、浮体を水面（y=draft）まで立てる。
            double sea = Math.Max(shaft / 2.0 + 4.0, R / 2.0);
            HDisc(cells, 0, 0, sea, 0, 0, p.Base);
            for (int y = 1; y <= draft; y++)
                HDisc(cells, 0, 0, sea, y, y, WaterId);
            // 浮体は主軸より太い。FAWT はロータ径9.3mに対し浮体径1.7m。
            HDisc(cells, 0, 0, Math.Max(1.5, shaft / 2.0 + 1.0), 1, draft, p.Accent);
        }
        else
        {
            HDisc(cells, 0, 0, Math.Max(2.0, shaft / 2.0 + 1.5), 0, 0, p.Base);
            // 発電機室。増速機と発電機を地上に置けるのが垂直軸型の利点。
            if (post >= 2) Fill(cells, -2, 2, 1, 2, -2, 2, p.Accent);
        }

        // 主軸。細いうちは中身まで詰め、太くなったら管にする。
        if (shaft <= 4) HDisc(cells, 0, 0, shaft / 2.0, draft + 1, top, p.Shell);
        else for (int y = draft + 1; y <= top; y++) HRing(cells, 0, 0, shaft / 2.0, y, p.Shell);

        int twist = Clamp(spec.IndustryRotorTwist ?? 180, 0, 720);

        switch (type)
        {
            case "darrieus":
                // φ型。翼は上下端で主軸に付き、中央で最大半径になる曲線（トロポスキーン）。
                for (int i = 0; i < blades; i++)
                    DarrieusBlade(cells, R, y0, h, ang + 360.0 * i / blades, chord, p.Blade);
                HDisc(cells, 0, 0, Math.Max(1.5, shaft / 2.0 + 1.0), y0, y0, p.Accent);
                HDisc(cells, 0, 0, Math.Max(1.5, shaft / 2.0 + 1.0), top, top, p.Accent);
                break;

            case "savonius":
                BuildSavonius(cells, d, h, y0, blades,
                    Clamp(spec.IndustryVawtStages ?? 3, 1, 8), ang, p);
                break;

            default:
                // H型（直線翼）とヘリカル。ヘリカルは高さに沿って翼の角度を回す。
                bool helical = type == "helical";
                for (int i = 0; i < blades; i++)
                {
                    double a0 = ang + 360.0 * i / blades;
                    StraightBlade(cells, R, y0, h, a0, helical ? twist : 0, chord, p.Blade);
                    // 支持アーム。翼の上下端を主軸へ繋ぐ。
                    ArmXZ(cells, R, y0, a0, chord, p.Accent);
                    ArmXZ(cells, R, y0 + h - 1, a0 + (helical ? twist : 0), chord, p.Accent);
                }
                break;
        }

        // 頂部の灯火。
        cells[(0, top + 1, 0)] = p.Light;

        // ガイワイヤ。主軸の頂部から3方向へ張る。浮体式では張らない。
        if (spec.IndustryGuy && !floating)
        {
            int anchor = (int)Math.Round(R) + 4;
            for (int i = 0; i < 3; i++)
            {
                double a = (ang + 120.0 * i) * Math.PI / 180.0;
                int ax = (int)Math.Round(Math.Cos(a) * anchor);
                int az = (int)Math.Round(Math.Sin(a) * anchor);
                cells[(ax, 0, az)] = p.Base;
                Line3D(cells, 0, top, 0, ax, 1, az, p.Lattice);
            }
        }
    }

    // ダリウス（φ型）の翼1枚。半径は r = R*sin(π*f)（f は下端からの高さの比）で、
    // 上下端が主軸に付き中央が最も張り出す。刻みを細かく取って線が切れないようにする。
    private static void DarrieusBlade(Dictionary<(int x, int y, int z), string> cells,
        double R, int y0, int h, double angDeg, int chord, string id)
    {
        double a = angDeg * Math.PI / 180.0;
        double ux = Math.Cos(a), uz = Math.Sin(a);
        double px = -uz, pz = ux;

        int steps = Math.Max(16, h * 8);
        for (int s = 0; s <= steps; s++)
        {
            double f = s / (double)steps;
            int y = y0 + (int)Math.Round(h * f);
            double r = R * Math.Sin(Math.PI * f);
            for (int c = 0; c < chord; c++)
            {
                double off = c - (chord - 1) / 2.0;
                int x = (int)Math.Round(ux * r + px * off);
                int z = (int)Math.Round(uz * r + pz * off);
                if (y >= 0) cells[(x, y, z)] = id;
            }
        }
    }

    // 直線翼。twist が 0 なら垂直（H型）、正ならその角度だけ上端までねじれる（ヘリカル）。
    private static void StraightBlade(Dictionary<(int x, int y, int z), string> cells,
        double R, int y0, int h, double angDeg, double twist, int chord, string id)
    {
        int steps = Math.Max(h, h * 4);
        for (int s = 0; s <= steps; s++)
        {
            double f = s / (double)steps;
            int y = y0 + (int)Math.Round((h - 1) * f);
            double a = (angDeg + twist * f) * Math.PI / 180.0;
            double ux = Math.Cos(a), uz = Math.Sin(a);
            double px = -uz, pz = ux;
            for (int c = 0; c < chord; c++)
            {
                double off = c - (chord - 1) / 2.0;
                int x = (int)Math.Round(ux * R + px * off);
                int z = (int)Math.Round(uz * R + pz * off);
                if (y >= 0) cells[(x, y, z)] = id;
            }
        }
    }

    // 支持アーム。主軸から半径 R の翼まで、水平面を1マス幅で繋ぐ。
    private static void ArmXZ(Dictionary<(int x, int y, int z), string> cells,
        double R, int y, double angDeg, int chord, string id)
    {
        if (y < 0) return;
        double a = angDeg * Math.PI / 180.0;
        double ux = Math.Cos(a), uz = Math.Sin(a);
        int steps = Math.Max(2, (int)Math.Ceiling(R * 2));
        for (int s = 0; s <= steps; s++)
        {
            double t = R * s / steps;
            int x = (int)Math.Round(ux * t);
            int z = (int)Math.Round(uz * t);
            if (!cells.ContainsKey((x, y, z))) cells[(x, y, z)] = id;
        }
    }

    // サボニウス（S型ロータ）。半円バケットを重なり比ぶん食い込ませて S 字にする。
    // 全体直径 D = 4*rb - e、バケット中心の偏心 q = rb - e/2（e は重なり）。
    // 段ごとに 360/(枚数×段数) 度ずらすと、どの向きの風でも起動できる。
    private static void BuildSavonius(Dictionary<(int x, int y, int z), string> cells,
        int d, int h, int y0, int blades, int stages, int ang, Palette p)
    {
        double e = Math.Max(1.0, d / 5.0);          // 重なり比0.2相当
        double rb = (d + e) / 4.0;
        double q = rb - e / 2.0;
        double R = d / 2.0;

        int hs = Math.Max(1, h / stages);
        for (int k = 0; k < stages; k++)
        {
            int ya = y0 + k * hs;
            int yb = k == stages - 1 ? y0 + h - 1 : ya + hs - 1;
            double off = ang + 360.0 * k / (blades * (double)stages);

            HDisc(cells, 0, 0, R, ya - 1, ya - 1, p.Deck);      // 段の仕切り板

            for (int i = 0; i < blades; i++)
            {
                double a = (off + 360.0 * i / blades) * Math.PI / 180.0;
                double ux = Math.Cos(a), uz = Math.Sin(a);
                double px = -uz, pz = ux;
                int steps = Math.Max(12, (int)Math.Ceiling(rb * Math.PI * 2));
                for (int s = 0; s <= steps; s++)
                {
                    double t = Math.PI * s / steps;             // 0..π の半円
                    double cp = q + rb * Math.Cos(t);
                    double cu = rb * Math.Sin(t);
                    int x = (int)Math.Round(px * cp + ux * cu);
                    int z = (int)Math.Round(pz * cp + uz * cu);
                    for (int y = ya; y <= yb; y++)
                        if (y >= 0) cells[(x, y, z)] = p.Blade;
                }
            }
        }
        HDisc(cells, 0, 0, R, y0 + h, y0 + h, p.Deck);          // 頂部の端板
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
                1.0, 1.0, z0, z1, p.Blade);

        for (int i = 0; i < paddles; i++)
        {
            double a = ang + 360.0 * i / paddles;
            double inner = Math.Max(1.5, R - 2.0);
            BladeXY(cells, 0, cy, a, inner, R - 0.5, 1.0, 1.0, z0, z1, p.Deck);

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
