using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 空港の平面土木 3 種（滑走路・誘導路・エプロン）。どれも y=0 の舗装 1 層＋標識なので、
// コンストラクタで種類を受けて UI と既定値を切り替える 1 クラスにまとめる。
// 座標生成は AirportExpander が受け持つので、ここは structure_type="airport:<種類>" と
// 寸法・標識の指定・素材を渡すだけ。
//
// ===== 寸法の決め方 =====
// 実物の空港は「就航する最大機の翼幅と主脚外側間隔」からコードレター(A〜F)が決まり、
// 滑走路幅・誘導路幅・ショルダー幅・標識の本数がそこから芋づる式に決まる。
// この UI も同じ順序で、機体サイズ 1 つを選べば 3 種すべての寸法が揃うようにする。
// 幅を直接いじらせないので、値をずらして左右非対称になることがない。
//
// 出典（ICAO Annex 14 Vol.I / EASA CS-ADR-DSN）:
//   滑走路幅       … 2B:23m / 4C・4D・4E:45m / 4F:60m
//   滑走路ショルダー … 必須はコード D・E・F のみ。ショルダー込みの全幅は D/E:60m、F:75m
//   誘導路幅       … B:10.5m / C:15m（主脚外側間隔 6m 未満）または 18m（6〜9m）/
//                    E:23m / F:25m
//   誘導路ショルダー … ショルダー込みの全幅が C:25m / D:38m / E:44m / F:60m
//   進入端の縦縞   … 幅で本数が決まる。18m:4 / 23m:6 / 30m:8 / 45m:12 / 60m:16
//   エプロン       … スポット幅は翼幅＋両側のクリアランス。
//                    クリアランスはコード A/B:3.0m、C:4.5m、D/E/F:7.5m
public sealed class AirportPavementParamsControl : UserControl, IManualParamControl
{
    private readonly string _kind;   // "runway" / "taxiway" / "apron"
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 機体サイズ 1 区分の実寸(m)。3 種の寸法をここ 1 か所から配る。
    private readonly struct AircraftSize
    {
        public readonly string Label;      // UI 表示
        public readonly string Code;       // ICAO コード
        public readonly double RunwayW;    // 滑走路幅
        public readonly double RunwayShl;  // 滑走路ショルダー（片側）
        public readonly double TaxiW;      // 誘導路幅
        public readonly double TaxiShl;    // 誘導路ショルダー（片側）
        public readonly double SpotW;      // エプロン 1 スポットの幅（翼幅＋クリアランス）
        public readonly double SpotD;      // エプロン 1 スポットの奥行き（全長＋余裕）

        public AircraftSize(string label, string code, double rw, double rs,
                            double tw, double ts, double sw, double sd)
        {
            Label = label; Code = code;
            RunwayW = rw; RunwayShl = rs;
            TaxiW = tw; TaxiShl = ts;
            SpotW = sw; SpotD = sd;
        }
    }

    private static AircraftSize SizeOf(string key) => key switch
    {
        // CRJ200 翼幅 21m・全長 27m。コード 2B。クリアランス 3.0m
        "s" => new AircraftSize("S 小型機（CRJ200級）", "2B",
                                23, 0, 10.5, 0, 27, 33),
        // B777-300ER 翼幅 65m・全長 74m。コード 4E。クリアランス 7.5m
        "l" => new AircraftSize("L 大型機（B777/787級）", "4E",
                                45, 7.5, 23, 10.5, 81, 83),
        // A380 翼幅 80m・全長 73m。コード 4F。クリアランス 7.5m
        "ll" => new AircraftSize("LL 超大型機（A380級）", "4F",
                                 60, 7.5, 25, 17.5, 95, 83),
        // A320 翼幅 36m・全長 38m・主脚外側間隔 8.95m。コード 4C。クリアランス 4.5m
        _ => new AircraftSize("M 中型機（A320/737級）", "4C",
                              45, 0, 18, 3.5, 45, 47),
    };

    private static readonly (string Text, string Value)[] SizeItems =
    {
        ("S 小型機（CRJ200級・コード2B）", "s"),
        ("M 中型機（A320/737級・コード4C）", "m"),
        ("L 大型機（B777/787級・コード4E）", "l"),
        ("LL 超大型機（A380級・コード4F）", "ll"),
    };

    // 実寸(m) → マス数。四捨五入（.5 は切り上げ）で最低 1 マス。
    private static int Blocks(double meters, int scale)
        => Math.Max(1, (int)Math.Round(meters / scale, MidpointRounding.AwayFromZero));

    private static int Blocks0(double meters, int scale)
        => Math.Max(0, (int)Math.Round(meters / scale, MidpointRounding.AwayFromZero));

    // 舗装の幅は奇数に揃える。中心線が厳密な中央に載り、
    // 進入端標識や接地帯標識の左右対称が崩れなくなる。
    private static int Odd(int v) => v % 2 == 1 ? v : v + 1;

    public AirportPavementParamsControl(string kind)
    {
        _kind = (kind ?? "runway").Trim().ToLowerInvariant();
        if (_kind != "taxiway" && _kind != "apron") _kind = "runway";
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", _kind == "apron" ? "走行路側" : "進入端側", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("機体サイズ")
           .Choice("size", "想定する機体", SizeItems, "m");

        if (_kind == "runway")
        {
            _ui.Note("コードで幅が決まる。2B:23m / 4C・4E:45m / 4F:60m。"
                   + "ショルダーが要るのは D・E・F のみ。縦縞の本数も幅から決まる。");

            _ui.Heading("縮尺")
               .Choice("scale", "1マスの大きさ", new[]
               {
                   ("実寸（1マス=1m）", "1"),
                   ("1マス=2m", "2"),
                   ("1マス=5m", "5"),
                   ("1マス=10m", "10"),
               }, "1")
               .Note("実寸だと64マスで進入端まわりだけ。10mにすると2500m級の全体が入る。");

            _ui.Heading("寸法")
               .IntSlider("len", "延長", 16, 64, 64, "マス数。実寸は縮尺を掛けた値")
               .Toggle("shoulder", "ショルダーあり", "舗装のみ", true);

            _ui.Heading("標識・灯火")
               .Toggle("mark", "標識を描く", "舗装のみ", true)
               .Note("中心線は実線30m＋間隔20m、進入端の縦縞・接地帯標識・"
                   + "着陸目標点標識は実寸の位置に置かれる（範囲外なら描かれない）。")
               .Toggle("light", "縁灯あり", "灯火なし", true);
        }
        else if (_kind == "taxiway")
        {
            _ui.Note("コードで幅が決まる。B:10.5m / C:18m / E:23m / F:25m。"
                   + "ショルダー込みの全幅は C:25m / E:44m / F:60m。");

            _ui.Heading("縮尺")
               .Choice("scale", "1マスの大きさ", new[]
               {
                   ("実寸（1マス=1m）", "1"),
                   ("1マス=2m", "2"),
                   ("1マス=5m", "5"),
               }, "1");

            _ui.Heading("寸法")
               .IntSlider("len", "延長", 8, 64, 48)
               .Toggle("shoulder", "ショルダーあり", "舗装のみ", true);

            _ui.Heading("標識・灯火")
               .Toggle("mark", "標識を描く", "舗装のみ", true)
               .Note("中心線は黄の実線1本、両縁に誘導路縁標識の線が走る。")
               .Toggle("light", "縁灯あり", "灯火なし", true);
        }
        else
        {
            _ui.Note("スポット1つの大きさが決まる。全幅はスポット数ぶん横に伸びる。");

            _ui.Heading("縮尺")
               .Choice("scale", "1マスの大きさ", new[]
               {
                   ("実寸（1マス=1m）", "1"),
                   ("1マス=2m", "2"),
                   ("1マス=3m", "3"),
                   ("1マス=5m", "5"),
               }, "1")
               .Note("LLは実寸だと1スポット95マス。大きすぎるときは2〜5にする。");

            _ui.Heading("寸法")
               .IntSlider("spots", "スポット数", 1, 8, 3)
               .Toggle("lane", "走行路あり", "駐機区画のみ", true);

            _ui.Heading("標識")
               .Toggle("mark", "標識を描く", "舗装のみ", true)
               .Note("各スポットに区画線・リードインライン・ストップマークを引く。");
        }

        // 機体サイズから外れた寸法を作りたいとき用。ON の間だけ手入力が効く。
        if (_kind != "apron")
        {
            _ui.Heading("詳細")
               .Toggle("custom", "手動で寸法を指定", "機体サイズから自動", false)
               .BeginGroup("custom")
                   .Note("マス数で直接指定する。奇数に丸めてから使う。")
                   .IntSlider("cw", "幅", 4, 63, _kind == "runway" ? 45 : 23)
                   .IntSlider("csh", "ショルダー", 0, 20, _kind == "runway" ? 8 : 11)
               .EndGroup();
        }

        // エプロンの標識は実物では黄色。滑走路・誘導路の中心線標識は白と黄で分かれる。
        string markDefault = _kind == "runway"
            ? "minecraft:white_concrete"
            : "minecraft:yellow_concrete";

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "舗装", "minecraft:gray_concrete")
           .BlockPick("mark", "標識", markDefault)
           .BlockPick("line", "区画線・縁標識", "minecraft:yellow_concrete")
           .BlockPick("shoulder", "ショルダー", "minecraft:light_gray_concrete")
           .BlockPick("light", "灯火", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        bool mark = _ui.GetBool("mark");
        var size = SizeOf(_ui.GetChoice("size", "m"));
        int scale = int.TryParse(_ui.GetChoice("scale", "1"), out int s) && s > 0 ? s : 1;

        var spec = new StructureSpec
        {
            StructureType = "airport:" + _kind,
            FacadeFace = _ui.GetChoice("face", "south"),
            AirportMarking = mark,
            Height = 2,                            // 参考値。舗装は y=0 の 1 層
            FloorBlock = pave,
            AccentBlock = _ui.GetBlock("mark", "minecraft:white_concrete"),
            WallBlock = _ui.GetBlock("line", "minecraft:yellow_concrete"),
            BaseBlock = _ui.GetBlock("shoulder", "minecraft:light_gray_concrete"),
            SeatBlock = _ui.GetBlock("light", "minecraft:sea_lantern")
        };

        if (_kind == "apron")
        {
            int spots = _ui.GetInt("spots");
            int sw = Odd(Math.Max(5, Blocks(size.SpotW, scale)));
            int stand = Math.Max(5, Blocks(size.SpotD, scale));
            int lane = _ui.GetBool("lane") ? Math.Max(2, Blocks(size.TaxiW, scale)) : 0;

            spec.Width = spots * sw;               // 全幅はスポット数から決まる従属値
            spec.Depth = stand + lane;
            spec.AirportSpots = spots;
            spec.AirportSpotWidth = sw;
            spec.AirportShoulder = lane;

            summary = $"エプロン {size.Label} {spots}スポット / "
                    + $"全幅{spec.Width}×奥行き{spec.Depth}（{ScaleNote(scale)}） / "
                    + (mark ? "区画線・誘導線あり" : "舗装のみ");
            return spec;
        }

        bool custom = _ui.GetBool("custom");
        double widthM = _kind == "runway" ? size.RunwayW : size.TaxiW;
        double shlM = _kind == "runway" ? size.RunwayShl : size.TaxiShl;

        int w = custom ? Odd(_ui.GetInt("cw")) : Odd(Blocks(widthM, scale));
        int shoulder = custom
            ? _ui.GetInt("csh")
            : (_ui.GetBool("shoulder") ? Blocks0(shlM, scale) : 0);

        int len = _ui.GetInt("len");

        spec.Width = w;
        spec.Depth = len;
        spec.AirportScale = scale;
        spec.AirportShoulder = shoulder;
        spec.AirportEdgeLight = _ui.GetBool("light") ? 60 : 0;   // 縁灯の間隔は 60m 以下

        // 中心線の周期・進入端の縦縞・接地帯標識は null のままにして、
        // AirportExpander に幅の実寸から ICAO の表どおり決めさせる。
        int total = w + shoulder * 2;
        string over = total > 64 ? " ※全幅64超。縮尺を上げると収まる" : "";

        if (_kind == "runway")
        {
            summary = $"滑走路 コード{size.Code} 幅{w}×延長{len}マス"
                    + $"（実寸 約{w * scale}m×{len * scale}m・{ScaleNote(scale)}） / "
                    + (shoulder > 0 ? $"ショルダー片側{shoulder}（全幅{total}）" : "ショルダーなし")
                    + " / " + (mark ? "標識あり" : "舗装のみ") + over;
        }
        else
        {
            summary = $"誘導路 コード{size.Code} 幅{w}×延長{len}マス"
                    + $"（実寸 約{w * scale}m×{len * scale}m・{ScaleNote(scale)}） / "
                    + (shoulder > 0 ? $"ショルダー片側{shoulder}（全幅{total}）" : "ショルダーなし")
                    + " / " + (mark ? "中心線・縁標識あり" : "舗装のみ") + over;
        }

        return spec;
    }

    private static string ScaleNote(int scale)
        => scale == 1 ? "実寸" : $"1マス={scale}m";
}
