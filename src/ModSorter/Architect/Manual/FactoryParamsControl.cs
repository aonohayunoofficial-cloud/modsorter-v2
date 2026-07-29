using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 工場。無柱スパンの繰り返しと、採光のための鋸屋根／越屋根が再現度の軸。
public sealed class FactoryParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public FactoryParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .IntSlider("w", "幅", 5, 64, 21)
           .IntSlider("d", "奥行", 5, 64, 31)
           .IntSlider("h", "軒高", 4, 32, 9, "壁の高さ。屋根はこの上に載る");

        _ui.Heading("屋根")
           .Choice("roof", "屋根形式", new[]
           {
               ("鋸屋根 (sawtooth)", "sawtooth"),
               ("越屋根 (monitor)", "monitor"),
               ("切妻 (gable)", "gable"),
               ("平屋根 (flat)", "flat"),
           }, "sawtooth")
           .Choice("ridge", "棟の向き", new[] { ("X軸", "x"), ("Z軸", "z") }, "x")
           .IntSlider("pitch", "勾配", 1, 4, 2, "1=急(45°) / 4=緩やか");

        _ui.BeginChoiceGroup("roof", "sawtooth")
           .IntSlider("bays", "山の数", 1, 12, 5, "無柱スパンの繰り返し数")
           .EndGroup();

        _ui.BeginChoiceGroup("roof", "monitor")
           .IntSlider("mw", "越屋根の幅", 3, 15, 5)
           .IntSlider("mh", "越屋根の高さ", 1, 8, 3)
           .EndGroup();

        _ui.Heading("大型シャッター")
           .Choice("gateFace", "取り付け面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south")
           .IntSlider("gates", "本数", 0, 6, 2)
           .IntSlider("gateW", "開口幅", 3, 16, 6)
           .IntSlider("gateH", "開口高", 3, 12, 5);

        _ui.Heading("開口")
           .Toggle("door", "通用口あり", "通用口なし", true)
           .Toggle("clerestory", "高窓あり", "高窓なし", true)
           .BeginGroup("clerestory")
           .IntSlider("clerestoryCount", "高窓の数(片面)", 1, 12, 6)
           .EndGroup();

        _ui.Heading("煙突")
           .Toggle("chimney", "煙突あり", "煙突なし", true)
           .BeginGroup("chimney")
           .IntSlider("chimneyCount", "本数", 1, 4, 1)
           .IntSlider("chimneyHeight", "高さ", 1, 24, 8)
           .Choice("chimneyAlign", "寄せ方向", new[]
           {
               ("中央", "center"), ("北寄せ", "north"), ("南寄せ", "south"),
               ("東寄せ", "east"), ("西寄せ", "west"),
           }, "north")
           .Choice("chimneyThick", "太さ", new[]
           {
               ("細（1マス）", "thin"),
               ("中（ひし形・中空）", "medium"),
               ("太（4×4・中空）", "thick"),
           }, "medium")
           .Toggle("chimneyPierce", "貫く（床から通す）", "貫かない（屋根上のみ）", false)
           .BlockPick("chimneyBlock", "煙突", "minecraft:bricks")
           .EndGroup();

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:bricks")
           .BlockPick("floor", "床", "minecraft:smooth_stone")
           .BlockPick("roofBlock", "屋根", "minecraft:deepslate_tiles")
           .BlockPick("glazing", "採光ガラス", "minecraft:glass");

        Content = _ui.Root;
    }

    private static string Opposite(string face) => face switch
    {
        "north" => "south",
        "south" => "north",
        "east" => "west",
        _ => "east"
    };

    // シャッターを角を避けて等間隔に置く。幅は面に収まるよう自動で詰める。
    private static void AddGates(List<Opening> ops, string face, int count, int span, int gw, int gh)
    {
        if (count <= 0 || span < 5) return;
        int usable = span - 2;                        // 両端1マスは残す
        gw = Math.Clamp(Math.Min(gw, usable / count - 1), 1, usable);
        if (gw < 1) return;

        int slot = usable / count;
        for (int i = 0; i < count; i++)
        {
            int center = 1 + slot / 2 + i * slot;
            center = Math.Clamp(center, 1 + gw / 2, span - 2 - gw / 2);
            ops.Add(new Opening
            {
                Face = face,
                Kind = "gate",
                Offset = center,
                Level = 1,
                Width = gw,
                Height = gh
            });
        }
    }

    private static void AddEven(List<Opening> ops, string face, string kind,
        int count, int span, int level)
    {
        if (count <= 0) return;
        int lo = 1, hi = span - 2;
        if (hi < lo) { lo = 0; hi = span - 1; }
        int n = Math.Min(count, hi - lo + 1);
        if (n <= 0) return;
        for (int i = 0; i < n; i++)
        {
            int offset = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)Math.Round((double)(hi - lo) * i / (n - 1));
            ops.Add(new Opening { Face = face, Kind = kind, Offset = offset, Level = level });
        }
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = _ui.GetInt("w");
        int d = _ui.GetInt("d");
        int h = _ui.GetInt("h");
        string roofType = _ui.GetChoice("roof", "sawtooth");
        string ridge = _ui.GetChoice("ridge", "x");
        int pitch = Math.Clamp(_ui.GetInt("pitch"), 1, 4);

        string wall = _ui.GetBlock("wall", "minecraft:bricks");
        string floor = _ui.GetBlock("floor", "minecraft:smooth_stone");
        string roof = _ui.GetBlock("roofBlock", "minecraft:deepslate_tiles");
        string glazing = _ui.GetBlock("glazing", "minecraft:glass");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        var ops = new List<Opening>();

        string gateFace = _ui.GetChoice("gateFace", "south");
        int gateSpan = (gateFace == "north" || gateFace == "south") ? w : d;
        int gates = _ui.GetInt("gates");
        AddGates(ops, gateFace, gates, gateSpan, _ui.GetInt("gateW"), _ui.GetInt("gateH"));

        if (_ui.GetBool("door"))
        {
            string doorFace = Opposite(gateFace);
            int doorSpan = (doorFace == "north" || doorFace == "south") ? w : d;
            AddEven(ops, doorFace, "door", 1, doorSpan, 1);
        }

        if (_ui.GetBool("clerestory"))
        {
            int level = Math.Max(2, h - 3);
            int n = _ui.GetInt("clerestoryCount");
            bool gateOnZ = gateFace == "north" || gateFace == "south";
            string f1 = gateOnZ ? "east" : "north";
            string f2 = gateOnZ ? "west" : "south";
            int span1 = gateOnZ ? d : w;
            AddEven(ops, f1, "window", n, span1, level);
            AddEven(ops, f2, "window", n, span1, level);
        }

        bool chimney = _ui.GetBool("chimney");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roofType,
            RidgeAxis = ridge,
            RoofPitch = pitch,
            SawtoothBays = _ui.GetInt("bays"),
            MonitorWidth = _ui.GetInt("mw"),
            MonitorHeight = _ui.GetInt("mh"),
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roof,
            GlazingBlock = glazing,
            Openings = ops,
            ChimneyCount = chimney ? _ui.GetInt("chimneyCount") : 0,
            ChimneyHeight = _ui.GetInt("chimneyHeight"),
            ChimneyAlign = _ui.GetChoice("chimneyAlign", "north"),
            ChimneyThickness = _ui.GetChoice("chimneyThick", "medium"),
            ChimneyPierce = _ui.GetBool("chimneyPierce"),
            ChimneyBlock = _ui.GetBlock("chimneyBlock", roof),
            // 工場に軒は不要。鋸屋根・越屋根は BuildEaves が非対応で、
            // 切妻・平屋根でも工場の外観としては軒を出さないため 0 固定。
            EaveOverhang = 0
        };

        summary = $"{w}×{d}×{h} / 屋根={roofType} / シャッター{gates} / 煙突{spec.ChimneyCount}";
        return spec;
    }
}
