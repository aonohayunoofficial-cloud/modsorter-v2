using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 空港の平面土木施設（structure_type="airport:<種類>"）の座標生成。
// harbor と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・開口部・
// 入口保証・フットプリントマスクは一切通らない。既存の小分類には影響しない。
//
// ===== 寸法の扱い（再現性の要）=====
// 標識・灯火の寸法は ICAO Annex 14 Vol.I 第5章と国交省の設計基準で m 単位に決まっている。
// そこでこのクラスは実寸(m)を定数で持ち、Scale（1マス=何m）で割ってマスへ落とす。
// マス数を直接書かないので、縮尺を変えても比率は実物のまま崩れない。
//   Scale=1  … 1マス=1m。滑走路の進入端まわりを切り出す見せ方。延長64マス=64mなので
//              150m 地点の接地帯標識は範囲外＝自動的に描かれない（実寸どおりの判定）。
//   Scale=10 … 1マス=10m。滑走路 2500m 級の全体像を 64 マスに収める見せ方。
//              このとき幅45mは5マスに落ちるが、標識の本数は実寸の幅から決まる。
//
// ===== 実寸の出典 =====
//   滑走路幅         … Code E で 45m（Code C 30m / Code F 60m）
//   滑走路ショルダー … Code D/E は舗装総幅 60m ＝ 片側 7.5m
//   進入端標識       … 進入端から 6m の位置から始まる縦縞。長さ 30m 以上、幅 1.8m、
//                      間隔 1.8m。本数は幅で決まる（18m:4 / 23m:6 / 30m:8 / 45m:12 / 60m:16）。
//                      横は舗装縁から 3m 以内、または中心線から 27m 以内の小さい方まで。
//   中心線標識       … 実線 30m ＋ 間隔 20m の破線（1周期 50m 以上）。幅は精密進入で 0.90m。
//   着陸目標点標識   … 進入端から 400m（滑走路長 2400m 以上のとき）、長さ 45〜60m。
//   接地帯標識       … 進入端から 150m ごとの対。帯の長さ 22.5m。
//   滑走路縁灯       … 間隔 60m 以下。
//   誘導路幅         … Code B:10.5m / C:15m（主脚外側間隔 6m 未満）・18m（6〜9m）/
//                      E:23m / F:25m。ショルダー込みの舗装総幅は C:25m / D:38m /
//                      E:44m / F:60m なので、Code E なら片側 10.5m。
//   誘導路中心線標識 … 黄の実線 1 本、幅 0.15m。縁標識は 2 本の連続線。
//   エプロン         … スポット幅は「翼幅＋両側のクリアランス」。クリアランスは
//                      Code A/B:3.0m、C:4.5m、D/E/F:7.5m。A320（翼幅 35.8m）で
//                      約 45m、B777（翼幅 64.8m）で約 80m。

// 滑走路指示標識（進入端の数字）は文字なので、このクラスでは生成しない。
//
// 平面なので断面は「進入端側が z=0、逆側が z の増加方向」で組み、最後に Rotate で向きを回す。
// 舗装は y=0 の 1 層で、標識は同じ層の塗り分け、灯火だけ y=1 に載る。
// ショルダーが負座標へ張り出すぶんも Normalize で 0 起点へ寄る。
//
// StructureSpec との対応。
//   width … 幅（x 方向・マス） / depth … 延長（z 方向・マス）
//   airport_scale       … 1マスあたりの実寸(m)。既定 1
//   airport_shoulder    … ショルダー幅（片側・マス）。0 でなし
//   airport_marking     … 標識の有無
//   airport_center_step … 中心線標識の周期(m)。0 で実線。既定 50m
//   airport_threshold   … 進入端標識の本数。null で幅から自動決定、0 で無し
//   airport_touchdown   … 接地帯標識の対の上限数。0 で無し
//   airport_edge_light  … 縁灯の間隔(m)。0 で灯火なし。既定 60m
//   airport_spots       … エプロンのスポット数 / airport_spot_width … スポットの幅（マス）
//   facade_face … 進入端・接続側の向き（既定 south）
//   floor_block=舗装 / accent_block=標識 / base_block=ショルダー
//   seat_block=縁灯 / wall_block=区画線・ストップマーク
public static class AirportExpander
{
    public const string Prefix = "airport:";

    // ===== 実寸（m）。マス数はすべてここから Scale で割って導く =====
    private const double ThresholdOffsetM = 6.0;     // 進入端から縦縞の始まりまで
    private const double ThresholdStripeLenM = 30.0; // 縦縞の長さ（最小30m）
    private const double StripeWidthM = 1.8;         // 縦縞の幅
    private const double EdgeClearM = 3.0;           // 舗装縁から縦縞までの空き
    private const double CenterOnM = 30.0;           // 中心線標識の実線長
    private const double CenterOffM = 20.0;          // 中心線標識の間隔
    private const double CenterWidthM = 0.90;        // 中心線標識の幅（精密進入）
    private const double AimPointM = 400.0;          // 着陸目標点標識の位置
    private const double AimLenM = 45.0;             // 着陸目標点標識の長さ
    private const double AimWidthM = 6.0;            // 着陸目標点標識の幅
    private const double TdzFirstM = 150.0;          // 接地帯標識の1組目
    private const double TdzStepM = 150.0;           // 接地帯標識の間隔
    private const double TdzLenM = 22.5;             // 接地帯標識の帯の長さ
    private const double TdzWidthM = 3.0;            // 接地帯標識の帯の幅
    private const double SideOffsetM = 18.0;         // 中心線から接地帯・目標点までの横距離
    private const double EdgeLightM = 60.0;          // 縁灯の間隔
    private const double TaxiCenterWidthM = 0.15;    // 誘導路中心線標識の幅
    private const double RunwayShoulderM = 7.5;      // 滑走路ショルダー（片側）
    private const double TaxiShoulderM = 10.5;       // 誘導路ショルダー（Code E・片側）

    // StructureExpander から呼ぶ判定。"airport:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "taxiway":
            case "taxi": return "taxiway";
            case "apron":
            case "ramp": return "apron";
            default: return "runway";
        }
    }

    private sealed class Palette
    {
        public readonly string Pave, Mark, Shoulder, Light, Line;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Pave = Pick(spec.FloorBlock, allowed, fallback);
            Mark = Pick(spec.AccentBlock, allowed, Pave);
            Shoulder = Pick(spec.BaseBlock, allowed, Pave);
            Light = Pick(spec.SeatBlock, allowed, Mark);
            Line = Pick(spec.WallBlock, allowed, Mark);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        switch (KindOf(spec.StructureType))
        {
            case "taxiway": BuildTaxiway(cells, spec, p); break;
            case "apron": BuildApron(cells, spec, p); break;
            default: BuildRunway(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== 滑走路 =====
    private static void BuildRunway(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        int w = Clamp(spec.Width, 6, 64);        // 幅（マス）
        int len = Clamp(spec.Depth, 8, 64);      // 延長（マス）
        double widthM = w * scale;               // 幅の実寸。標識の本数はこれで決まる

        int shoulder = Clamp(spec.AirportShoulder ?? M0(RunwayShoulderM, scale), 0, 16);

        // ショルダー。舗装の外側へ左右に張り出す（負座標は Normalize で寄る）。
        if (shoulder > 0)
        {
            Fill(cells, -shoulder, -1, 0, 0, 0, len - 1, p.Shoulder);
            Fill(cells, w, w + shoulder - 1, 0, 0, 0, len - 1, p.Shoulder);
        }

        // 舗装面。
        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        int cx = (w - 1) / 2;
        int cw = M(CenterWidthM, scale);         // 中心線標識の幅
        int cx0 = cx - (cw - 1) / 2;
        int cx1 = cx0 + cw - 1;

        if (spec.AirportMarking)
        {
            // ===== 中心線標識 =====
            // 実線30m＋間隔20mの破線。周期に 0 を指定すると実線になる。
            double periodM = spec.AirportCenterStep ?? (CenterOnM + CenterOffM);
            if (periodM <= 0)
            {
                Fill(cells, cx0, cx1, 0, 0, 0, len - 1, p.Mark);
            }
            else
            {
                int period = M(periodM, scale);
                int on = Math.Max(1, (int)Math.Round(period * CenterOnM / (CenterOnM + CenterOffM)));
                for (int z = 0; z < len; z++)
                    if (z % period < on) Fill(cells, cx0, cx1, 0, 0, z, z, p.Mark);
            }

            // ===== 進入端標識 =====
            // 本数は幅から決まる（45m なら 12 本）。指定があればそれを優先する。
            int stripes = Clamp(spec.AirportThreshold ?? ThresholdStripes(widthM), 0, 20);
            if (stripes >= 2)
            {
                int half = stripes / 2;
                int sw = M(StripeWidthM, scale);              // 縞の幅
                int z0 = M0(ThresholdOffsetM, scale);         // 進入端からの空き
                int z1 = Math.Min(len - 1, z0 + M(ThresholdStripeLenM, scale) - 1);

                // 片側に使える幅。中心線標識の外側から、舗装縁の 3m 手前まで。
                int inner = cx1 + 1;
                int outer = w - 1 - M0(EdgeClearM, scale);
                int span = outer - inner + 1;

                if (z1 >= z0 && span >= sw)
                {
                    // 最外の縞の外端がちょうど outer に来るよう等間隔に割る。
                    // 縞の幅は実寸を守り、間隔で辻褄を合わせる（縁からの空きが実物どおりになる）。
                    double pitch = (half <= 1) ? 0 : (double)(span - sw) / (half - 1);
                    for (int i = 0; i < half; i++)
                    {
                        int a = inner + (int)Math.Round(i * pitch);
                        int b = Math.Min(outer, a + sw - 1);
                        if (a > outer) break;

                        // 中心線を挟んで対称に置く。
                        Fill(cells, a, b, 0, 0, z0, z1, p.Mark);
                        Fill(cells, Math.Max(0, cx - (b - cx)), Math.Max(0, cx - (a - cx)),
                             0, 0, z0, z1, p.Mark);
                    }
                }
            }

            // ===== 着陸目標点標識 =====
            // 進入端から 400m。延長が足りなければ描かれない（実寸どおりの判定）。
            int aimZ = M0(AimPointM, scale);
            int aimLen = M(AimLenM, scale);
            bool hasAim = aimZ + aimLen <= len;
            if (hasAim)
                PairBand(cells, cx, w, SideOffsetM, AimWidthM, aimZ, aimZ + aimLen - 1, scale, p.Mark);

            // ===== 接地帯標識 =====
            // 進入端から 150m ごとの対。着陸目標点と重なる組は置かない。
            int tdzMax = Clamp(spec.AirportTouchdown ?? 6, 0, 8);
            int tdzLen = M(TdzLenM, scale);
            for (int i = 0; i < tdzMax; i++)
            {
                int tz = M0(TdzFirstM + i * TdzStepM, scale);
                if (tz + tdzLen > len) break;
                if (hasAim && tz < aimZ + aimLen && tz + tdzLen > aimZ) continue;
                PairBand(cells, cx, w, SideOffsetM, TdzWidthM, tz, tz + tdzLen - 1, scale, p.Mark);
            }
        }

        // ===== 滑走路縁灯 =====
        EdgeLights(cells, p, w, len, spec.AirportEdgeLight ?? (int)EdgeLightM, scale);
    }

    // ===== 誘導路 =====
    // 幅 23m 以上が基準。中心線は黄の実線 1 本、両縁に縁標識の 2 本線が走る。
    private static void BuildTaxiway(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        int w = Clamp(spec.Width, 4, 48);
        int len = Clamp(spec.Depth, 8, 64);
        int shoulder = Clamp(spec.AirportShoulder ?? M0(TaxiShoulderM, scale), 0, 16);

        if (shoulder > 0)
        {
            Fill(cells, -shoulder, -1, 0, 0, 0, len - 1, p.Shoulder);
            Fill(cells, w, w + shoulder - 1, 0, 0, 0, len - 1, p.Shoulder);
        }

        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        if (spec.AirportMarking)
        {
            // 中心線標識。誘導路は実線。
            int cx = (w - 1) / 2;
            int cw = M(TaxiCenterWidthM, scale);
            Fill(cells, cx, cx + cw - 1, 0, 0, 0, len - 1, p.Mark);

            // 誘導路縁標識。舗装の縁から 1 マス内側に走る連続線。
            foreach (int x in new[] { 1, w - 2 })
                if (x > 0 && x < w - 1 && (x < cx || x > cx + cw - 1))
                    Fill(cells, x, x, 0, 0, 0, len - 1, p.Line);
        }

        EdgeLights(cells, p, w, len, spec.AirportEdgeLight ?? (int)EdgeLightM, scale);
    }

    // ===== エプロン =====
    // スポット（駐機場）1 つの寸法は「翼幅＋両側のクリアランス」で決まる。
    // クリアランスは Annex 14 でコード A/B が 3.0m、C が 4.5m、D/E/F が 7.5m。
    // ここでは幅を入力として受けず、UI が機体サイズから決めた 1 スポットぶんの幅を
    // airport_spot_width で受け取り、スポット数ぶん横に並べる。全幅の頭打ちはしない。
    // 頭打ちすると端のスポットだけ切れて左右非対称になるため。
    // スポット幅は奇数に丸め、リードインラインを厳密な中央に載せる。
    // 区画線は各スポットが自分の枠の両端に引くので、並べても対称のまま。
    private static void BuildApron(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int spots = Clamp(spec.AirportSpots ?? 3, 1, 12);        // スポット数
        int sw = Clamp(spec.AirportSpotWidth ?? 45, 5, 96);      // 1 スポットの幅
        if (sw % 2 == 0) sw++;                                   // 中央 1 マスを確保
        int lane = Clamp(spec.AirportShoulder ?? 0, 0, 32);      // 走行路（タキシレーン）
        int len = Clamp(spec.Depth, 6, 192);                     // 駐機区画＋走行路
        bool marking = spec.AirportMarking;

        int w = spots * sw;                                      // 全幅は従属値
        int stand = Math.Max(4, len - lane);                     // 駐機区画の奥行き
        int total = stand + lane;

        // 舗装面。駐機区画と走行路をまとめて 1 面で敷く。
        Fill(cells, 0, w - 1, 0, 0, 0, total - 1, p.Pave);

        if (!marking) return;

        int barHalf = Math.Max(1, sw / 6);                       // ストップマークの半幅
        int stopZ = Math.Max(2, stand / 5);                      // 機首の停止位置

        for (int i = 0; i < spots; i++)
        {
            int x0 = i * sw;
            int x1 = x0 + sw - 1;
            int cx = x0 + sw / 2;                                // 奇数幅なので厳密な中央

            // 区画線。自分の枠の両端に引くので、隣り合うスポットの境界は 2 本線になる。
            Fill(cells, x0, x0, 0, 0, 0, stand - 1, p.Line);
            Fill(cells, x1, x1, 0, 0, 0, stand - 1, p.Line);

            // リードインライン。走行路側から停止位置まで。
            Fill(cells, cx, cx, 0, 0, stopZ, stand - 1, p.Mark);

            // ストップマーク。機首の停止位置を示す横棒。中心から左右等幅。
            Fill(cells, cx - barHalf, cx + barHalf, 0, 0, stopZ, stopZ, p.Mark);
        }

        // 走行路の中心線。駐機区画の奥を横切る。
        if (lane > 0)
        {
            int lz = stand + (lane - 1) / 2;
            Fill(cells, 0, w - 1, 0, 0, lz, lz, p.Mark);
        }
    }

    // ===== 共通ヘルパー =====

    // 実寸(m) → マス数。最低 1 マス（塗る対象が消えないように）。
    private static int M(double meters, double scale)
        => Math.Max(1, (int)Math.Round(meters / scale));

    // 実寸(m) → マス数。0 を許す（間隔・空きで「無し」を表せるように）。
    private static int M0(double meters, double scale)
        => Math.Max(0, (int)Math.Round(meters / scale));

    // 進入端標識の本数（ICAO Annex 14 Vol.I の表）。幅は実寸(m)。
    private static int ThresholdStripes(double widthM)
    {
        if (widthM < 20.5) return 4;    // 18m 級
        if (widthM < 26.5) return 6;    // 23m 級
        if (widthM < 37.5) return 8;    // 30m 級
        if (widthM < 52.5) return 12;   // 45m 級
        return 16;                       // 60m 級
    }

    // 中心線から左右対称に、指定の横距離・幅で帯を置く。接地帯標識と着陸目標点標識に使う。
    private static void PairBand(
        Dictionary<(int x, int y, int z), string> cells,
        int cx, int w, double offM, double bandM, int z0, int z1, double scale, string block)
    {
        int off = M(offM, scale);
        int bw = M(bandM, scale);

        int ra = cx + off, rb = ra + bw - 1;
        int lb = cx - off, la = lb - bw + 1;

        if (ra < w) Fill(cells, Math.Max(0, ra), Math.Min(w - 1, rb), 0, 0, z0, z1, block);
        if (lb >= 0) Fill(cells, Math.Max(0, la), Math.Min(w - 1, lb), 0, 0, z0, z1, block);
    }

    // 舗装の両縁に沿って一定間隔で灯火を置く。間隔は実寸(m)。
    private static void EdgeLights(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int w, int len, int intervalM, double scale)
    {
        if (intervalM <= 0) return;
        int step = M0(intervalM, scale);
        if (step <= 0) return;

        for (int z = step / 2; z < len; z += step)
        {
            cells[(0, 1, z)] = p.Light;
            cells[(w - 1, 1, z)] = p.Light;
        }
    }

    private static void Fill(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string block)
    {
        if (x1 < x0 || y1 < y0 || z1 < z0) return;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, y, z)] = block;
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 進入端側を z の小さい側（north）として組んだものを、指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string face)
    {
        if (face == "north" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = face switch
            {
                "south" => (w - 1 - x, d - 1 - z),
                "east" => (d - 1 - z, x),
                "west" => (z, w - 1 - x),
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
