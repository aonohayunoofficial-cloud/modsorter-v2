using System;
using System.Collections.Generic;

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
// 実寸の一覧は AirportExpander.Dimensions.cs、種類ごとの寸法の根拠は各ファイルの冒頭に置く。
//
// 滑走路指示標識（進入端の数字）は文字なので、このクラスでは生成しない。
//
// 平面なので断面は「進入端側が z=0、逆側が z の増加方向」で組み、最後に Rotate で向きを回す。
// 舗装は y=0 の 1 層で、標識は同じ層の塗り分け、灯火だけ y=1 に載る。
// ショルダーが負座標へ張り出すぶんも Normalize で 0 起点へ寄る。
//
// StructureSpec との対応（平面土木）。
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
public static partial class AirportExpander
{
    public const string Prefix = "airport:";

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
}
