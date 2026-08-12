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
//   進入灯 CAT I     … 進入端から 900m のセンターライン（間隔 30m）＋
//                      クロスバー 150/300/450/600/750m の5本。300m のものは長さ 30m、
//                      他は外縁を結ぶ線が進入端の 300m 先で収束するよう調整する。
//                      0〜300m は1灯、300〜600m は2灯、600〜900m は3灯。
//   進入灯 CAT II/III… CAT I に加えて 270m まで伸びる赤の側方列（間隔 30m）。
//   進入灯 簡易式    … 420m 以上のセンターライン（間隔 60m・30m まで詰めてよい）＋
//                      300m の位置に長さ 18m か 30m のクロスバー1本。
//   バレット         … 簡易式で 3m 以上、他で 4m 以上。使うときクロスバーは
//                      CAT I で 300m の1本、CAT II/III で 150m と 300m の2本だけになる。
//   ヘリポート       … すべて D 値（設計ヘリの全長）から決まる。FATO は 1D
//                      （限定用途なら 0.83D）、TLOF は 0.83D、セーフティエリアは
//                      FATO の外へ 3m か 0.25D の大きい方。TLOF 縁灯は緑・間隔 5m 以下。
//                      進入方向指示の H は D<16m のとき高さ 3m。
//                      TD/PM 円は内径 0.5D。

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
//
// 管制塔だけは平面土木ではないので寸法の扱いが別で、縮尺を持たず 1マス=1m で組む。
//   height=管制室の床の高さ / width・depth=庁舎の平面寸法
//   airport_cab_width・airport_cab_height・airport_cab_shape・airport_cab_tilt … 管制室
//   airport_shaft_width・airport_floor_step … シャフト
//   airport_catwalk … 外周通路 / airport_base_height … 庁舎 / airport_mast … アンテナ柱
//   airport_edge_light … 0 以外で航空障害灯を点ける
//   tower_block=塔身 / glazing_block=窓 / accent_block=窓枠・方立・腰壁
//   floor_block=床・キャットウォーク / roof_block=屋根 / parapet_block=手すり
//   base_block=庁舎 / seat_block=灯火
//
// 旅客ターミナルも 1マス=1m で組む。桁行きは width ではなくゲート数×間隔の従属値。
//   depth=建物の奥行き（エプロン側が z=0）
//   airport_gates・airport_gate_spacing … ゲート数と1ゲートあたりの桁行き
//   airport_levels・airport_level_height … 階数と階高
//   airport_bridge … 搭乗橋の伸長 / airport_canopy … 車寄せの庇
//   airport_terminal_roof … "flat" | "vault"
//   tower_block=躯体 / glazing_block=カーテンウォール / accent_block=方立・腰壁
//   floor_block=床・搭乗橋の床 / roof_block=屋根 / parapet_block=パラペット
//   seat_block=天井の照明
//
// 貨物ターミナルも 1マス=1m。桁行きはドック数×間隔の従属値。
//   depth=建物の奥行き（エプロン側が z=0）/ height=庫内の有効高さ
//   airport_docks・airport_dock_pitch … トラックドックの数と間隔
//   airport_airside_doors・airport_door_width … エアサイドの大型扉
//   airport_office … 事務所棟の桁行き / airport_canopy … ドック上屋
//   tower_block=躯体 / glazing_block=高窓・トップライト / accent_block=まぐさ・帯
//   floor_block=床・エプロン取付け / roof_block=屋根・上屋
//   parapet_block=シャッター・パラペット / seat_block=庫内の照明
//
// 格納庫も 1マス=1m。width=扉の開口幅 / depth=奥行き / height=庫内の有効高さ。
//   airport_door_height・airport_door_type … 扉の高さと形式
//   airport_bays … 収める機体の数 / airport_hangar_roof … 屋根の形
//   airport_annex … 側面の附属棟の奥行き
//   tower_block=躯体・トラス / glazing_block=扉の窓・高窓 / accent_block=まぐさ・柱
//   floor_block=床 / roof_block=屋根 / parapet_block=扉 / seat_block=庫内の照明
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
            case "control_tower":
            case "tower": return "control_tower";
            case "terminal":
            case "passenger_terminal": return "terminal";
            case "cargo_terminal":
            case "cargo": return "cargo_terminal";
            case "hangar": return "hangar";
            case "approach_light":
            case "als": return "approach_light";
            case "helipad":
            case "heliport": return "helipad";
            default: return "runway";
        }
    }

    private sealed class Palette
    {
        public readonly string Pave, Mark, Shoulder, Light, Line, Body, Glass, Roof, Rail;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Pave = Pick(spec.FloorBlock, allowed, fallback);
            Mark = Pick(spec.AccentBlock, allowed, Pave);
            Shoulder = Pick(spec.BaseBlock, allowed, Pave);
            Light = Pick(spec.SeatBlock, allowed, Mark);
            Line = Pick(spec.WallBlock, allowed, Mark);

            // 管制塔・旅客ターミナルで使う。平面土木の3種は参照しない。
            Body = Pick(spec.TowerBlock ?? spec.WallBlock, allowed, Pave);
            Glass = Pick(spec.GlazingBlock, allowed, Mark);
            Roof = Pick(spec.RoofBlock, allowed, Shoulder);
            Rail = Pick(spec.ParapetBlock, allowed, Mark);
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
            case "control_tower": BuildControlTower(cells, spec, p); break;
            case "terminal": BuildTerminal(cells, spec, p); break;
            case "cargo_terminal": BuildCargoTerminal(cells, spec, p); break;
            case "hangar": BuildHangar(cells, spec, p); break;
            case "approach_light": BuildApproachLight(cells, spec, p); break;
            case "helipad": BuildHelipad(cells, spec, p); break;
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
        if (hasBase)
        {
            int bx = (baseW - 1) / 2;
            int bz = (baseD - 1) / 2;

            Fill(cells, -bx, bx, 0, 0, -bz, bz, p.Pave);

            for (int y = 1; y <= baseH; y++)
            {
                Fill(cells, -bx, bx, y, y, -bz, -bz, p.Shoulder);
                Fill(cells, -bx, bx, y, y, bz, bz, p.Shoulder);
                Fill(cells, -bx, -bx, y, y, -bz, bz, p.Shoulder);
                Fill(cells, bx, bx, y, y, -bz, bz, p.Shoulder);
            }

            // 窓。1マスおきに抜く。
            int wy = Math.Max(2, baseH - 2);
            for (int x = -bx + 1; x <= bx - 1; x++)
                if (((x + bx) & 1) == 0)
                {
                    cells[(x, wy, -bz)] = p.Glass;
                    cells[(x, wy, bz)] = p.Glass;
                }
            for (int z = -bz + 1; z <= bz - 1; z++)
                if (((z + bz) & 1) == 0)
                {
                    cells[(-bx, wy, z)] = p.Glass;
                    cells[(bx, wy, z)] = p.Glass;
                }

            // 屋根。シャフトの通る中央は空ける。
            for (int x = -bx; x <= bx; x++)
                for (int z = -bz; z <= bz; z++)
                    if (!InPlan(shape, x, z, shaftR)) cells[(x, baseH + 1, z)] = p.Roof;

            // 正面の出入口。
            int doorH = Math.Min(3, baseH);
            for (int x = -1; x <= 1; x++)
                for (int y = 1; y <= doorH; y++)
                    cells.Remove((x, y, -bz));
        }

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

        // ===== 管制室 =====
        PlanFill(cells, shape, cabR, floorY, p.Pave, false);

        // キャットウォークと手すり。
        if (walk > 0)
        {
            int rw = cabR + walk;
            for (int dx = -rw; dx <= rw; dx++)
                for (int dz = -rw; dz <= rw; dz++)
                    if (InPlan(shape, dx, dz, rw) && !InPlan(shape, dx, dz, cabR))
                        cells[(dx, floorY, dz)] = p.Pave;
            PlanFill(cells, shape, rw, floorY + 1, p.Rail, true);
        }

        // 窓。tilt 段ごとに 1 マス外へ出して 15 度の外傾を近似する。
        // 最下段はコンソールが並ぶ腰壁なので窓にしない。
        int rTop = cabR;
        for (int j = 0; j < cabH; j++)
        {
            int y = floorY + 1 + j;
            rTop = cabR + (tilt > 0 ? j / tilt : 0);
            PlanFill(cells, shape, rTop, y, j == 0 ? p.Mark : p.Glass, true);

            // 方立。平面の角に立てる。
            if (j > 0)
                for (int dx = -rTop; dx <= rTop; dx++)
                    for (int dz = -rTop; dz <= rTop; dz++)
                        if (IsCorner(shape, dx, dz, rTop)) cells[(dx, y, dz)] = p.Mark;
        }

        // ===== 屋根・アンテナ柱・航空障害灯 =====
        int roofY = floorY + 1 + cabH;
        PlanFill(cells, shape, rTop + 1, roofY, p.Roof, false);

        for (int k = 1; k <= mast; k++) cells[(0, roofY + k, 0)] = p.Mark;

        if (light)
        {
            cells[(0, roofY + mast + 1, 0)] = p.Light;
            cells[(rTop, roofY + 1, 0)] = p.Light;
            cells[(-rTop, roofY + 1, 0)] = p.Light;
            cells[(0, roofY + 1, rTop)] = p.Light;
            cells[(0, roofY + 1, -rTop)] = p.Light;
        }
    }

    // 管制室・シャフトの平面。"square" | "octagon"（既定） | "round"。
    private static string ShapeOf(string? s)
    {
        string v = (s ?? "octagon").Trim().ToLowerInvariant();
        return (v == "square" || v == "round") ? v : "octagon";
    }

    // 中央 1 マスを確保するため偶数を奇数へ丸める。
    private static int Odd(int v) => (v % 2 == 0) ? v + 1 : v;

    // 中心 (0,0) から見て (dx,dz) が半径 r の平面の内側か。
    // 八角形は正八角形の一辺 ＝ 対辺幅/(1+√2) に合わせて ax+az の上限を r×1.45 とする。
    private static bool InPlan(string shape, int dx, int dz, int r)
    {
        int ax = Math.Abs(dx), az = Math.Abs(dz);
        if (shape == "round") return dx * dx + dz * dz <= (r + 0.35) * (r + 0.35);
        if (ax > r || az > r) return false;
        if (shape == "octagon") return ax + az <= (int)Math.Round(r * 1.45);
        return true;
    }

    // 平面を 1 段ぶん塗る。ringOnly なら外周 1 マスだけ（4近傍が全部内側なら塗らない）。
    private static void PlanFill(
        Dictionary<(int x, int y, int z), string> cells,
        string shape, int r, int y, string block, bool ringOnly)
    {
        if (r < 0) return;
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                if (!InPlan(shape, dx, dz, r)) continue;
                if (ringOnly
                    && InPlan(shape, dx + 1, dz, r) && InPlan(shape, dx - 1, dz, r)
                    && InPlan(shape, dx, dz + 1, r) && InPlan(shape, dx, dz - 1, r)) continue;
                cells[(dx, y, dz)] = block;
            }
    }

    // 平面の角。方立を立てる位置。
    private static bool IsCorner(string shape, int dx, int dz, int r)
        => InPlan(shape, dx, dz, r)
           && (!InPlan(shape, dx + 1, dz, r) || !InPlan(shape, dx - 1, dz, r))
           && (!InPlan(shape, dx, dz + 1, r) || !InPlan(shape, dx, dz - 1, r));



    // ===== 旅客ターミナル =====
    // 平面土木ではないが "airport:" 配下なのでここで作る。縮尺は持たず 1マス=1m。
    //
    // 実寸の出典。
    //   ゲート1つあたりの桁行き … 平均 33〜40m（FAA AC 150/5360-13）。ピア全長は 210〜300m。
    //                            エプロンのスポット幅と同じ寸法系なので同じ値を受け取る。
    //   建物の奥行き   … ダブルローデッドの動線幅 30ft（約9m・動く歩道なし）＋
    //                    ラウンジ奥行き 25〜30ft（8〜9m）。片側ピアで 26〜30m。
    //   階構成         … 出発が上階・到着が下階の2層が基本。
    //   搭乗橋         … アプロンドライブ式。伸長 15〜45m、ロタンダ高さ 5m 級（最大8m）、
    //                    トンネルの勾配は 10% 以下。
    //
    // 桁行きは「ゲート数 × ゲート間隔」の従属値。頭打ちすると端のゲートだけ切れて
    // 左右非対称になるので、収まらないときは幅を切らずにゲート数を減らす。
    // ゲート中心は i*pitch + pitch/2 で、エプロンのスポット中心と同じ式にしてある。
    // 断面は「エプロン側が z=0」で組み、最後に Rotate で向きを回す。
    private const int TerminalMaxLen = 256; // 桁行きの上限（マス）。超える分はゲート数を減らす

    private static void BuildTerminal(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int pitch = Odd(Clamp(spec.AirportGateSpacing ?? 45, 9, 96));
        int gates = Clamp(spec.AirportGates ?? 3, 1, 8);
        while (gates > 1 && gates * pitch > TerminalMaxLen) gates--;

        int len = gates * pitch;                              // 桁行き（x）
        int depth = Clamp(spec.Depth, 10, 48);                // 奥行き（z）。z=0 がエプロン側
        int levels = Clamp(spec.AirportLevels ?? 2, 1, 4);
        int lh = Clamp(spec.AirportLevelHeight ?? 6, 4, 8);   // 階高。搭乗橋の高さもこれに従う
        int bridge = Clamp(spec.AirportBridge ?? 15, 0, 48);
        int canopy = Clamp(spec.AirportCanopy ?? 6, 0, 16);
        bool vault = string.Equals(
            (spec.AirportTerminalRoof ?? "flat").Trim(), "vault", StringComparison.OrdinalIgnoreCase);

        int top = levels * lh;   // 最上階の天井＝屋根の高さ

        // ===== 床 =====
        Fill(cells, 0, len - 1, 0, 0, 0, depth - 1, p.Pave);
        for (int i = 1; i < levels; i++)
            Fill(cells, 1, len - 2, i * lh, i * lh, 1, depth - 2, p.Pave);

        // ===== カーテンウォール（エプロン側と道路側）=====
        // 各階の床レベルは腰壁、3マスごとに方立、それ以外はガラス。
        for (int y = 1; y < top; y++)
        {
            bool band = (y % lh == 0) || y == 1;
            for (int x = 0; x < len; x++)
            {
                string b = band ? p.Mark : ((x % 3 == 0) ? p.Body : p.Glass);
                cells[(x, y, 0)] = b;
                cells[(x, y, depth - 1)] = b;
            }
        }

        // ===== 妻側の壁 =====
        for (int y = 1; y < top; y++)
            for (int z = 0; z < depth; z++)
            {
                cells[(0, y, z)] = p.Body;
                cells[(len - 1, y, z)] = p.Body;
            }

        // ===== 内部の柱 =====
        int colZ = (depth - 1) / 2;
        for (int x = 8; x < len - 1; x += 9)
            for (int y = 1; y < top; y++)
                cells[(x, y, colZ)] = p.Body;

        // ===== 天井の照明 =====
        for (int i = 0; i < levels; i++)
        {
            int y = (i + 1) * lh - 1;
            for (int x = 4; x < len - 1; x += 8)
                for (int z = 4; z < depth - 1; z += 8)
                    cells[(x, y, z)] = p.Light;
        }

        // ===== 屋根 =====
        if (!vault)
        {
            Fill(cells, 0, len - 1, top, top, 0, depth - 1, p.Roof);
            for (int x = 0; x < len; x++)
            {
                cells[(x, top + 1, 0)] = p.Rail;
                cells[(x, top + 1, depth - 1)] = p.Rail;
            }
            for (int z = 0; z < depth; z++)
            {
                cells[(0, top + 1, z)] = p.Rail;
                cells[(len - 1, top + 1, z)] = p.Rail;
            }
        }
        else
        {
            // かまぼこ屋根。奥行き方向に半円弧を張る。
            double rise = Math.Max(2.0, depth / 4.0);
            var h = new int[depth];
            for (int z = 0; z < depth; z++)
                h[z] = (int)Math.Round(rise * Math.Sin(Math.PI * (z + 0.5) / depth));

            // 妻壁を弧の下まで立ち上げる。
            for (int z = 0; z < depth; z++)
            {
                Fill(cells, 0, 0, top, top + h[z], z, z, p.Body);
                Fill(cells, len - 1, len - 1, top, top + h[z], z, z, p.Body);
            }

            // 弧の面。隣との段差ぶんだけ下へ伸ばして穴を塞ぐ。
            for (int z = 0; z < depth; z++)
            {
                int prev = z > 0 ? h[z - 1] : 0;
                int next = z < depth - 1 ? h[z + 1] : 0;
                int lo = top + Math.Min(h[z], Math.Min(prev, next));
                Fill(cells, 0, len - 1, lo, top + h[z], z, z, p.Roof);
            }
        }

        // ===== 車寄せの庇 =====
        if (canopy > 0)
        {
            int cy = lh;
            Fill(cells, 0, len - 1, cy, cy, depth, depth + canopy - 1, p.Roof);
            for (int x = 4; x < len; x += 8)
                Fill(cells, x, x, 1, cy - 1, depth + canopy - 1, depth + canopy - 1, p.Body);
        }

        // ===== 出入口・搭乗橋 =====
        // 搭乗橋の床はロタンダの高さ。実物は 5m 級なので出発階の床に合わせる。
        int fy = levels >= 2 ? lh : 4;

        for (int i = 0; i < gates; i++)
        {
            int cx = i * pitch + pitch / 2;   // エプロンのスポット中心と同じ式

            // 道路側の出入口。
            for (int x = cx - 2; x <= cx + 2; x++)
                for (int y = 1; y <= 3; y++)
                    cells.Remove((x, y, depth - 1));

            if (bridge <= 0) continue;

            // ロタンダの開口。
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = fy; y <= fy + 2; y++)
                    cells.Remove((x, y, 0));

            // トンネル。勾配は 10% が上限なので 10 マスにつき 1 マス下げる。
            for (int k = 1; k <= bridge; k++)
            {
                int y = Math.Max(2, fy - k / 10);
                Fill(cells, cx - 1, cx + 1, y, y, -k, -k, p.Pave);
                Fill(cells, cx - 1, cx - 1, y + 1, y + 2, -k, -k, p.Glass);
                Fill(cells, cx + 1, cx + 1, y + 1, y + 2, -k, -k, p.Glass);
                Fill(cells, cx - 1, cx + 1, y + 3, y + 3, -k, -k, p.Roof);
            }

            // 走行装置の支柱。ロタンダから 2/3 の位置に立てる。
            int sk = Math.Max(2, bridge * 2 / 3);
            int sy = Math.Max(2, fy - sk / 10);
            Fill(cells, cx, cx, 0, sy - 1, -sk, -sk, p.Body);

            // 先端のキャブ。幅を広げて機体側の口にする。
            if (bridge >= 4)
            {
                int ey = Math.Max(2, fy - bridge / 10);
                Fill(cells, cx - 2, cx + 2, ey, ey, -bridge, -bridge + 1, p.Pave);
                Fill(cells, cx - 2, cx - 2, ey + 1, ey + 2, -bridge, -bridge + 1, p.Glass);
                Fill(cells, cx + 2, cx + 2, ey + 1, ey + 2, -bridge, -bridge + 1, p.Glass);
                Fill(cells, cx - 1, cx + 1, ey + 1, ey + 2, -bridge, -bridge, p.Glass);
                Fill(cells, cx - 2, cx + 2, ey + 3, ey + 3, -bridge, -bridge + 1, p.Roof);
            }
        }
    }

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
    // 桁行きは「ドック数 × 間隔」の従属値。頭打ちすると端のドックだけ切れるので、
    // 収まらないときは幅を切らずにドック数を減らす。
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

        // ===== トラックドック（ランドサイド）=====
        for (int i = 0; i < docks; i++)
        {
            int cx = i * pitch + pitch / 2;
            int x0 = Math.Max(1, cx - 1);
            int x1 = Math.Min(len - 2, cx + 1);

            Fill(cells, x0, x1, 2, 4, lastZ, lastZ, p.Rail);   // シャッター
            Fill(cells, Math.Max(0, cx - 2), Math.Min(len - 1, cx + 2),
                 5, 5, lastZ, lastZ, p.Mark);                  // まぐさ

            // ドックバンパー。床と同じ高さに出す。
            cells[(x0, 1, depth)] = p.Mark;
            cells[(x1, 1, depth)] = p.Mark;
        }

        // ===== ドック上屋 =====
        if (canopy > 0)
        {
            int cy = Math.Min(6, roofY - 1);
            Fill(cells, 0, len - 1, cy, cy, depth, depth + canopy - 1, p.Roof);
            for (int x = 4; x < len; x += 8)
                Fill(cells, x, x, 1, cy - 1, depth + canopy - 1, depth + canopy - 1, p.Body);
        }

        // ===== エアサイドの大型扉 =====
        for (int j = 0; j < doors; j++)
        {
            int cx = (2 * j + 1) * len / (2 * doors);
            int x0 = Math.Max(1, cx - doorW / 2);
            int x1 = Math.Min(len - 2, cx + doorW / 2);

            Fill(cells, x0, x1, 2, doorH + 1, 0, 0, p.Rail);
            Fill(cells, Math.Max(0, x0 - 1), Math.Min(len - 1, x1 + 1),
                 doorH + 2, doorH + 2, 0, 0, p.Mark);

            // エプロン側の取付け。床との段差 1m をここで摺り付ける。
            Fill(cells, x0, x1, 0, 0, -4, -1, p.Pave);
            Fill(cells, x0, x1, 1, 1, -1, -1, p.Pave);
        }

        // ===== 事務所棟 =====
        // 倉庫の妻側に付く2層の別棟。階高4、奥行きは倉庫に合わせて最大16。
        if (office >= 6)
        {
            int ow = office;
            int od = Math.Min(depth, 16);
            int oh = 9;

            Fill(cells, -ow, -1, 0, 1, 0, od - 1, p.Pave);
            Fill(cells, -ow, -1, 5, 5, 0, od - 1, p.Pave);

            for (int y = 2; y < oh; y++)
            {
                bool band = (y == 5);
                for (int x = -ow; x <= -1; x++)
                {
                    string b = band ? p.Mark : ((x % 2 == 0) ? p.Body : p.Glass);
                    cells[(x, y, 0)] = b;
                    cells[(x, y, od - 1)] = b;
                }
                for (int z = 0; z < od; z++)
                    cells[(-ow, y, z)] = band ? p.Mark : ((z % 2 == 0) ? p.Body : p.Glass);
            }

            Fill(cells, -ow, -1, oh, oh, 0, od - 1, p.Roof);
            for (int z = 0; z < od; z++) cells[(-ow, oh + 1, z)] = p.Rail;
            for (int x = -ow; x <= -1; x++)
            {
                cells[(x, oh + 1, 0)] = p.Rail;
                cells[(x, oh + 1, od - 1)] = p.Rail;
            }

            // 道路側の出入口。
            for (int x = -ow / 2 - 1; x <= -ow / 2 + 1; x++)
                for (int y = 2; y <= 4; y++)
                    cells.Remove((x, y, od - 1));

            // 倉庫との連絡口。
            for (int y = 2; y <= 4; y++)
                for (int z = 2; z <= 4; z++)
                    cells.Remove((0, y, z));
        }
    }

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

        // ===== 扉 =====
        // まぐさ。扉の上に 1 マスの帯を通し、その上を躯体で塞ぐ。
        Fill(cells, 0, len - 1, doorH + 1, doorH + 1, 0, 0, p.Mark);
        Fill(cells, 0, len - 1, doorH + 2, wallTop, 0, 0, p.Body);

        // 開口の両脇の柱型。
        Fill(cells, 0, 0, 1, doorH, 0, 0, p.Mark);
        Fill(cells, len - 1, len - 1, 1, doorH, 0, 0, p.Mark);

        if (door != "open")
        {
            int mid = len / 2;
            for (int x = 1; x < len - 1; x++)
            {
                // 引き分け戸は中央から左右へ、折り戸は等間隔で建具の縦框が入る。
                bool stile = (door == "fold")
                    ? (x % 5 == 0)
                    : (x == mid || Math.Abs(x - mid) % 8 == 0);

                for (int y = 1; y <= doorH; y++)
                {
                    // 上から 2 段目を扉の窓にする。実物も同じ位置に窓が並ぶ。
                    bool win = (y == doorH - 1) && !stile && (x % 2 == 0);
                    cells[(x, y, 0)] = stile ? p.Mark : (win ? p.Glass : p.Rail);
                }
            }

            // 通用口。扉の端に人の出入りする戸を空ける。
            for (int y = 1; y <= 3; y++)
                cells.Remove((2, y, 0));
        }

        // ===== 附属棟 =====
        // 側面に張り出す 2 層の別棟。工場・部品庫・事務所が入る。
        if (annex >= 4)
        {
            int ah = 9;
            int az0 = 2;
            int az1 = Math.Min(lastZ, az0 + Math.Max(8, depth - 6) - 1);

            Fill(cells, len, len + annex - 1, 0, 0, az0, az1, p.Pave);
            Fill(cells, len, len + annex - 1, 5, 5, az0, az1, p.Pave);

            for (int y = 1; y < ah; y++)
            {
                bool band = (y == 5);
                for (int z = az0; z <= az1; z++)
                    cells[(len + annex - 1, y, z)] = band ? p.Mark
                        : ((z % 2 == 0) ? p.Body : p.Glass);
                for (int x = len; x < len + annex; x++)
                {
                    cells[(x, y, az0)] = band ? p.Mark : p.Body;
                    cells[(x, y, az1)] = band ? p.Mark : p.Body;
                }
            }

            Fill(cells, len, len + annex - 1, ah, ah, az0, az1, p.Roof);
            for (int x = len; x < len + annex; x++)
            {
                cells[(x, ah + 1, az0)] = p.Rail;
                cells[(x, ah + 1, az1)] = p.Rail;
            }
            for (int z = az0; z <= az1; z++)
                cells[(len + annex - 1, ah + 1, z)] = p.Rail;

            // 格納庫との連絡口。
            for (int y = 1; y <= 3; y++)
                for (int z = az0 + 2; z <= Math.Min(az0 + 4, az1 - 1); z++)
                    cells.Remove((len - 1, y, z));
        }
    }

    // 格納庫の屋根。"arch"（既定） | "flat" | "shed"。
    private static string RoofOf(string? s)
    {
        string v = (s ?? "arch").Trim().ToLowerInvariant();
        return (v == "flat" || v == "shed") ? v : "arch";
    }

    // 格納庫の扉。"slide"（既定） | "fold" | "open"。
    private static string DoorOf(string? s)
    {
        string v = (s ?? "slide").Trim().ToLowerInvariant();
        return (v == "fold" || v == "open") ? v : "slide";
    }


    // ===== 進入灯 =====
    // 平面土木なので滑走路と同じく実寸(m)を持ち、Scale で割ってマスへ落とす。
    // 進入端が z=0 で、そこから手前（z の増加方向）へ伸びる。最後に Rotate で向きを回す。
    //
    // 実寸（ICAO Annex 14 Vol.I 第5章）。
    //   CAT I      … センターライン 900m（間隔30m）＋クロスバー 150/300/450/600/750m。
    //                300m のクロスバーは長さ30m、他は外縁を結ぶ線が進入端の300m先で
    //                収束するよう調整する。0〜300m は1灯、300〜600m は2灯、
    //                600〜900m は3灯（灯数で距離が読めるようにするため）。
    //   CAT II/III … CAT I に加えて 270m まで伸びる赤の側方列（間隔30m）。
    //   簡易式     … 420m 以上（間隔60m・30mまで詰めてよい）＋300m に長さ18mか30mの
    //                クロスバー1本。
    //   バレット   … 簡易式で3m以上、他で4m以上。使うときクロスバーは CAT I で 300m の
    //                1本、CAT II/III で 150m と 300m の2本だけ。
    //
    // 900m は Scale=1 だと 900 マスになる。縮尺 5〜10 を選ぶ前提の小分類。
    private const double AlsCat1LenM = 900.0;   // CAT I・CAT II/III のセンターライン長
    private const double AlsSimpleLenM = 420.0; // 簡易式のセンターライン長
    private const double AlsSpacingM = 30.0;    // センターラインの間隔
    private const double AlsSimpleSpacingM = 60.0; // 簡易式の間隔
    private const double AlsCrossbar300M = 30.0; // 300m のクロスバーの長さ
    private const double AlsConvergeM = 300.0;   // 外縁を結ぶ線が収束する位置（進入端の先）
    private const double AlsSideRowM = 270.0;    // CAT II/III の側方列の長さ
    private const double AlsBarretteM = 4.0;     // バレットの長さ（簡易式は 3m）
    private const double PapiOffsetM = 300.0;    // PAPI の位置（進入端から）
    private const double PapiSideM = 15.0;       // PAPI の横距離（滑走路縁から）

    private static void BuildApproachLight(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        string type = AlsTypeOf(spec.AirportAlsType);
        bool barrette = spec.AirportAlsBarrette;
        int trestle = Clamp(spec.AirportAlsTrestle ?? 0, 0, 8);
        bool simple = (type == "simple");

        double lenM = simple ? AlsSimpleLenM : AlsCat1LenM;
        int len = Clamp(spec.Depth, 8, M((int)lenM, scale));   // 実際に描く長さ（マス）
        int rw = Odd(Clamp(spec.Width, 5, 63));                // 滑走路の幅（マス）
        int cx = rw / 2;                                        // 中心線の x

        double spacingM = simple ? AlsSimpleSpacingM : AlsSpacingM;
        int step = Math.Max(1, M(spacingM, scale));
        double barM = simple ? 3.0 : AlsBarretteM;
        int barHalf = barrette ? Math.Max(0, M(barM, scale) / 2) : 0;

        int ty = trestle;   // 灯火を載せる高さ

        // ===== 進入端の帯 =====
        // 滑走路の側を示す基準。ここから手前へ灯列が伸びる。
        Fill(cells, 0, rw - 1, 0, 0, 0, 0, p.Pave);
        Fill(cells, 0, rw - 1, ty + 1, ty + 1, 0, 0, p.Mark);

        // ===== センターライン =====
        for (int k = 1; k * step <= len; k++)
        {
            int z = k * step;
            double distM = z * scale;   // 進入端からの実寸

            // 灯数。0〜300m は1灯、300〜600m は2灯、600〜900m は3灯。
            // 簡易式は距離によらず1灯。
            int lamps = simple ? 1 : (distM <= 300.0 ? 1 : (distM <= 600.0 ? 2 : 3));
            int half = barrette ? barHalf : (lamps - 1);

            Trestle(cells, cx, z, ty, p);
            Fill(cells, cx - half, cx + half, ty + 1, ty + 1, z, z, p.Light);
        }

        // ===== クロスバー =====
        // バレットを使うときは本数が減る（CAT I は 300m のみ、CAT II/III は 150m と 300m）。
        double[] bars = simple
            ? new[] { 300.0 }
            : (barrette
                ? (type == "cat2" ? new[] { 150.0, 300.0 } : new[] { 300.0 })
                : new[] { 150.0, 300.0, 450.0, 600.0, 750.0 });

        foreach (double bm in bars)
        {
            int z = M(bm, scale);
            if (z > len) continue;

            // 300m のクロスバーは長さ 30m（簡易式は 18m か 30m のうち 30m を採る）。
            // 他は外縁を結ぶ線が進入端の 300m 先で収束するように広げる。
            double barLenM = (Math.Abs(bm - 300.0) < 0.5)
                ? AlsCrossbar300M
                : AlsCrossbar300M * (bm + AlsConvergeM) / (300.0 + AlsConvergeM);

            int half = Math.Max(1, M(barLenM, scale) / 2);
            for (int x = cx - half; x <= cx + half; x++)
            {
                if (Math.Abs(x - cx) <= barHalf) continue;   // 中心はセンターラインが占める
                Trestle(cells, x, z, ty, p);
                cells[(x, ty + 1, z)] = p.Light;
            }
        }

        // ===== 側方列（CAT II/III のみ）=====
        if (type == "cat2")
        {
            int rowLen = Math.Min(len, M(AlsSideRowM, scale));
            int off = Math.Max(2, rw / 2);
            for (int k = 1; k * step <= rowLen; k++)
            {
                int z = k * step;
                foreach (int x in new[] { cx - off, cx + off })
                {
                    Trestle(cells, x, z, ty, p);
                    cells[(x, ty + 1, z)] = p.Mark;   // 側方列は赤
                }
            }
        }

        // ===== PAPI =====
        // 滑走路の左側、進入端から 300m の位置に 4 灯を横に並べる。
        if (spec.AirportPapi)
        {
            int z = M(PapiOffsetM, scale);
            if (z <= len)
            {
                int x0 = cx - Math.Max(2, rw / 2 + M(PapiSideM, scale));
                for (int i = 0; i < 4; i++)
                {
                    int x = x0 - i * Math.Max(1, M(9.0, scale));
                    Fill(cells, x, x, 0, 0, z, z, p.Pave);
                    cells[(x, 1, z)] = p.Light;
                }
            }
        }
    }

    // 進入灯橋の脚。trestle=0 なら地面に舗装だけ置く。
    private static void Trestle(
        Dictionary<(int x, int y, int z), string> cells, int x, int z, int ty, Palette p)
    {
        if (ty <= 0) { cells[(x, 0, z)] = p.Pave; return; }
        Fill(cells, x, x, 0, ty, z, z, p.Shoulder);
    }

    private static string AlsTypeOf(string? s)
    {
        string v = (s ?? "cat1").Trim().ToLowerInvariant();
        return (v == "cat2" || v == "simple") ? v : "cat1";
    }

    // ===== ヘリポート =====
    // 平面土木なので Scale で m からマスへ落とす。断面は「進入方向が z=0 側」で組む。
    //
    // 実寸（ICAO Annex 14 Vol.II）。すべて D 値（設計ヘリの全長）から決まる。
    //   FATO   … 1D。限定用途の地上式に限り 0.83D まで縮められる。
    //   TLOF   … 0.83D。FATO の中に置く。
    //   セーフティエリア … FATO の外へ 3m か 0.25D の大きい方。
    //   TLOF 縁灯 … 緑。間隔 5m 以下（円形）。
    //   TD/PM 円  … 内径 0.5D。
    //   H マーキング … D<16m のとき高さ 3m。
    private static void BuildHelipad(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        double dM = Clamp(spec.AirportHeliD ?? 15, 6, 40);
        bool marking = spec.AirportMarking;
        int lift = Clamp(spec.AirportHeliElevated ?? 0, 0, 24);
        bool fullFato = spec.AirportHeliFullFato;

        double fatoM = dM * (fullFato ? 1.0 : 0.83);
        double tlofM = dM * 0.83;
        double safeM = Math.Max(3.0, dM * 0.25);

        int fato = Odd(Math.Max(5, M(fatoM, scale)));
        int tlof = Odd(Math.Max(3, Math.Min(fato, M(tlofM, scale))));
        int safe = Math.Max(1, M(safeM, scale));

        int total = fato + safe * 2;      // セーフティエリア込みの一辺
        int c = total / 2;                // 中心
        int y = lift;                     // 舗装面の高さ

        // ===== 高架式の脚 =====
        if (lift > 0)
        {
            for (int i = 0; i < 4; i++)
            {
                int px = (i % 2 == 0) ? c - fato / 3 : c + fato / 3;
                int pz = (i < 2) ? c - fato / 3 : c + fato / 3;
                Fill(cells, px, px, 0, lift - 1, pz, pz, p.Shoulder);
            }
        }

        // ===== セーフティエリアと FATO =====
        Fill(cells, 0, total - 1, y, y, 0, total - 1, p.Shoulder);

        int f0 = safe, f1 = safe + fato - 1;
        Fill(cells, f0, f1, y, y, f0, f1, p.Pave);

        if (!marking) return;

        // FATO の外周（TLOF ではないので細い線でよい）。
        Fill(cells, f0, f1, y, y, f0, f0, p.Line);
        Fill(cells, f0, f1, y, y, f1, f1, p.Line);
        Fill(cells, f0, f0, y, y, f0, f1, p.Line);
        Fill(cells, f1, f1, y, y, f0, f1, p.Line);

        // ===== TLOF の外周 =====
        int t0 = c - tlof / 2, t1 = c + tlof / 2;
        Fill(cells, t0, t1, y, y, t0, t0, p.Mark);
        Fill(cells, t0, t1, y, y, t1, t1, p.Mark);
        Fill(cells, t0, t0, y, y, t0, t1, p.Mark);
        Fill(cells, t1, t1, y, y, t0, t1, p.Mark);

        // ===== TD/PM 円（内径 0.5D）=====
        int r = Math.Max(2, M(dM * 0.5, scale) / 2);
        for (int dx = -r - 1; dx <= r + 1; dx++)
            for (int dz = -r - 1; dz <= r + 1; dz++)
            {
                double d2 = dx * dx + dz * dz;
                if (d2 <= (r + 0.5) * (r + 0.5) && d2 >= (r - 0.5) * (r - 0.5))
                    cells[(c + dx, y, c + dz)] = p.Mark;
            }

        // ===== H マーキング =====
        // D<16m のとき高さ 3m。円の内側に収まる大きさにする。
        double hHeightM = (dM < 16.0) ? 3.0 : dM * 0.2;
        int hh = Math.Max(3, M(hHeightM, scale));
        if (hh % 2 == 0) hh++;
        int hw = Math.Max(2, hh * 2 / 3);
        if (hw % 2 == 0) hw++;

        int hz0 = c - hh / 2, hz1 = c + hh / 2;
        int hx0 = c - hw / 2, hx1 = c + hw / 2;

        Fill(cells, hx0, hx0, y, y, hz0, hz1, p.Line);   // 縦棒（左）
        Fill(cells, hx1, hx1, y, y, hz0, hz1, p.Line);   // 縦棒（右）
        Fill(cells, hx0, hx1, y, y, c, c, p.Line);       // 横棒

        // ===== TLOF 縁灯（緑・間隔 5m 以下）=====
        int ls = Math.Max(1, M(5.0, scale));
        for (int x = t0; x <= t1; x += ls)
        {
            cells[(x, y + 1, t0)] = p.Light;
            cells[(x, y + 1, t1)] = p.Light;
        }
        for (int z = t0; z <= t1; z += ls)
        {
            cells[(t0, y + 1, z)] = p.Light;
            cells[(t1, y + 1, z)] = p.Light;
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
