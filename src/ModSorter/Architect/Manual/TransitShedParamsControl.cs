using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 上屋（港湾の荷捌き施設）。倉庫と同じ箱ベースだが、再現度の軸が違う。
// 倉庫が「保管のための閉じた大空間」なのに対し、上屋は「岸壁とヤードの間で
// 貨物を通過させる施設」なので、海側と陸側の両長辺にシャッターが対称に並び、
// 庇（キャノピー）が長辺に張り出し、その上に高窓が回るのが特徴。
// 内部は柱スパンだけの吹き抜けで、中間床も煙突も持たない。
//
// 既定値の根拠（実寸）:
//   平屋上屋は桁行 50〜100m・梁間 20〜30m・有効高 5〜7m、柱スパン 10m 前後。
//   荷役シャッターは間口 4〜5m で、フォークリフトの通行を見込んで柱間ごとに 1つ。
//   庇の出は 3〜5m でトラックの荷台を覆う。桁行は 64 マス上限のため、
//   実物の 50m 級がそのまま入り、100m 級は 2 棟並べる粒度になる。
//
// 庇は展開側の軒（EaveOverhang）で作る。軒は屋根ブロックで生成され専用の素材指定を
// 持たないので、庇のブロック選択は設けず屋根と同じ材になる。
public sealed class TransitShedParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public TransitShedParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .Note("平屋上屋は桁行50〜100m・梁間20〜30m・有効高5〜7mが実物の目安。")
           .IntSlider("w", "桁行(長辺)", 16, 64, 50)
           .IntSlider("d", "梁間(短辺)", 10, 40, 24)
           .IntSlider("h", "軒高", 5, 20, 8, "壁の高さ。屋根はこの上に載る")
           .IntSlider("span", "柱スパン", 4, 12, 10, "外周の柱型リズム");

        _ui.Heading("屋根")
           .Choice("roof", "屋根形式", new[]
           {
               ("切妻 (gable)", "gable"),
               ("鋸屋根 (sawtooth)", "sawtooth"),
               ("陸屋根 (flat)", "flat"),
           }, "gable")
           .Choice("ridge", "棟の向き", new[] { ("X軸(長辺方向)", "x"), ("Z軸", "z") }, "x");

        _ui.BeginChoiceGroup("roof", "gable")
           .IntSlider("pitch", "勾配", 1, 4, 3, "1=急(45°) / 4=緩やか。上屋は緩勾配")
           .EndGroup();

        _ui.BeginChoiceGroup("roof", "sawtooth")
           .IntSlider("bays", "山の数", 1, 12, 6, "採光面がこの数だけ並ぶ")
           .EndGroup();

        _ui.BeginChoiceGroup("roof", "flat")
           .IntSlider("parapet", "パラペット", 0, 4, 2)
           .EndGroup();

        _ui.Heading("荷役シャッター")
           .Note("海側と陸側の両長辺に対称に並ぶのが上屋の要点。間口4〜5mが実物。")
           .IntSlider("gateW", "シャッターの間口", 3, 6, 4)
           .IntSlider("gateH", "シャッターの高さ", 3, 6, 4)
           .Toggle("gateBoth", "両長辺に配置", "海側のみ", true)
           .Toggle("gateEnd", "妻面にも配置", "妻面なし", false);

        _ui.Heading("庇")
           .Note("トラックの荷台を覆う張り出し。実物は3〜5m。素材は屋根と同じ。")
           .Toggle("canopy", "庇あり", "庇なし", true)
           .BeginGroup("canopy")
           .IntSlider("canopyW", "庇の出", 1, 6, 4)
           .EndGroup();

        _ui.Heading("高窓")
           .Toggle("clerestory", "高窓あり", "高窓なし", true)
           .BeginGroup("clerestory")
           .IntSlider("winCount", "窓の数(長辺あたり)", 2, 12, 8)
           .EndGroup();

        _ui.Heading("向き")
           .Choice("front", "海側", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south");

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:light_gray_concrete")
           .BlockPick("accent", "柱型", "minecraft:gray_concrete")
           .BlockPick("floor", "床(土間)", "minecraft:smooth_stone")
           .BlockPick("roofBlock", "屋根・庇", "minecraft:gray_concrete")
           .BlockPick("parapetBlock", "パラペット", "minecraft:light_gray_concrete")
           .BlockPick("glazing", "採光面・高窓", "minecraft:glass");

        Content = _ui.Root;
    }

    // 面の両端1マスを避けて等間隔に開口を並べる。
    private static void AddEven(List<Opening> ops, string face, string kind,
        int count, int span, int level, int w, int h, string? block)
    {
        if (count <= 0) return;
        int lo = 1, hi = span - 2;
        if (hi < lo) return;
        int n = Math.Min(count, hi - lo + 1);
        for (int i = 0; i < n; i++)
        {
            int offset = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)Math.Round((double)(hi - lo) * i / (n - 1));
            ops.Add(new Opening
            {
                Face = face,
                Kind = kind,
                Offset = offset,
                Level = level,
                Width = w,
                Height = h,
                Block = block
            });
        }
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = _ui.GetInt("w");
        int d = _ui.GetInt("d");
        int h = _ui.GetInt("h");
        int span = _ui.GetInt("span");

        string wall = _ui.GetBlock("wall", "minecraft:light_gray_concrete");
        string accent = _ui.GetBlock("accent", "minecraft:gray_concrete");
        string floor = _ui.GetBlock("floor", "minecraft:smooth_stone");
        string roofBlock = _ui.GetBlock("roofBlock", "minecraft:gray_concrete");
        string parapetBlock = _ui.GetBlock("parapetBlock", "minecraft:light_gray_concrete");
        string glazing = _ui.GetBlock("glazing", "minecraft:glass");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        string roof = _ui.GetChoice("roof", "gable");
        string front = _ui.GetChoice("front", "south");
        bool flatRoof = roof == "flat";
        bool sawtooth = roof == "sawtooth";

        // 海側と、その反対の陸側。長辺・妻面の対応も向きで決まる。
        string back = front switch
        {
            "north" => "south",
            "south" => "north",
            "east" => "west",
            _ => "east"
        };
        bool frontIsLongSide = front == "north" || front == "south";
        int longSpan = frontIsLongSide ? w : d;
        int endSpan = frontIsLongSide ? d : w;
        string end1 = frontIsLongSide ? "east" : "north";
        string end2 = frontIsLongSide ? "west" : "south";

        var ops = new List<Opening>();

        // 荷役シャッター。柱スパンごとに 1つ入る数を上限にする。
        int gateW = _ui.GetInt("gateW");
        int gateH = Math.Min(_ui.GetInt("gateH"), h - 1);
        int gates = Math.Max(1, longSpan / Math.Max(4, span));
        AddEven(ops, front, "gate", gates, longSpan, 1, gateW, gateH, null);
        if (_ui.GetBool("gateBoth"))
            AddEven(ops, back, "gate", gates, longSpan, 1, gateW, gateH, null);
        if (_ui.GetBool("gateEnd"))
        {
            AddEven(ops, end1, "gate", 1, endSpan, 1, gateW, gateH, null);
            AddEven(ops, end2, "gate", 1, endSpan, 1, gateW, gateH, null);
        }

        // 高窓。シャッターの上端より高い段に置いて衝突を避ける。
        if (_ui.GetBool("clerestory"))
        {
            int level = Math.Max(gateH + 2, h - 2);
            if (level <= h - 1)
            {
                int n = _ui.GetInt("winCount");
                AddEven(ops, front, "window", n, longSpan, level, 1, 1, glazing);
                AddEven(ops, back, "window", n, longSpan, level, 1, 1, glazing);
            }
        }

        // 庇は軒として作る。鋸屋根は展開側の軒が非対応なので、そのときは出さない。
        bool canopy = _ui.GetBool("canopy") && !sawtooth;

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roof,
            RidgeAxis = _ui.GetChoice("ridge", "x"),
            RoofPitch = roof == "gable" ? Math.Clamp(_ui.GetInt("pitch"), 1, 4) : 1,
            // 鋸屋根の山の数。他の屋根形では展開側が参照しない。
            SawtoothBays = sawtooth ? _ui.GetInt("bays") : 0,
            FacadeFace = front,
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roofBlock,
            AccentBlock = accent,
            GlazingBlock = glazing,
            PilasterStep = span,
            // パラペットは陸屋根のときだけ。勾配屋根では展開側が無視するので 0 を渡す。
            ParapetHeight = flatRoof ? _ui.GetInt("parapet") : 0,
            ParapetBlock = parapetBlock,
            Openings = ops,
            // 上屋は通過施設。中間床も煙突も持たない吹き抜けの一室。
            ChimneyCount = 0,
            // 庇は海側と陸側の長辺だけに出す。妻面には出さない。
            EaveOverhang = canopy ? _ui.GetInt("canopyW") : 0,
            EaveNorth = canopy && (front == "north" || back == "north"),
            EaveSouth = canopy && (front == "south" || back == "south"),
            EaveEast = canopy && (front == "east" || back == "east"),
            EaveWest = canopy && (front == "west" || back == "west")
        };

        string roofNote = sawtooth
            ? $"鋸屋根(山{spec.SawtoothBays})"
            : flatRoof
                ? $"陸屋根(パラペット{spec.ParapetHeight})"
                : $"切妻(勾配{spec.RoofPitch})";
        string canopyNote = canopy
            ? $"庇{spec.EaveOverhang}"
            : (sawtooth ? "庇なし(鋸屋根)" : "庇なし");
        string gateNote = _ui.GetBool("gateBoth") ? $"シャッター{gates}×両側" : $"シャッター{gates}";
        summary = $"上屋 {w}×{d}×{h} / {roofNote} / スパン{span} / {gateNote} / {canopyNote} / 海側{front}";
        return spec;
    }
}
