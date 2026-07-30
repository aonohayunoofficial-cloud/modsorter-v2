using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 倉庫。長い単一大空間、緩勾配の切妻または陸屋根＋パラペット、
// 長辺に並ぶ荷役シャッターと、その上の高窓が再現度の軸。
// 中間床は入れない（内部は天井まで吹き抜けの一室）。煙突も持たない。
public sealed class WarehouseParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public WarehouseParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .IntSlider("w", "幅", 9, 64, 31)
           .IntSlider("d", "奥行", 9, 64, 41)
           .IntSlider("h", "軒高", 5, 32, 10, "壁の高さ。屋根はこの上に載る");

        _ui.Heading("屋根")
           .Choice("roof", "屋根形式", new[]
           {
               ("切妻 (gable)", "gable"),
               ("片流れ (shed)", "shed"),
               ("陸屋根 (flat)", "flat"),
           }, "gable")
           .Choice("ridge", "棟の向き", new[] { ("X軸", "x"), ("Z軸", "z") }, "x");

        _ui.BeginChoiceGroup("roof", "gable", "shed")
           .IntSlider("pitch", "勾配", 1, 4, 3, "1=急(45°) / 4=緩やか。倉庫は緩勾配が普通")
           .IntSlider("eave", "軒の出", 0, 3, 1)
           .EndGroup();

        _ui.BeginChoiceGroup("roof", "flat")
           .IntSlider("parapet", "パラペット", 0, 4, 2, "屋根の外周を上へ立ち上げる高さ。0で無し")
           .EndGroup();

        _ui.Heading("荷役シャッター")
           .Choice("gateFace", "取り付け面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south")
           .IntSlider("gates", "本数", 0, 8, 4)
           .IntSlider("gateW", "開口幅", 3, 16, 4)
           .IntSlider("gateH", "開口高", 3, 12, 5);

        _ui.Heading("開口")
           .Toggle("door", "通用口あり", "通用口なし", true)
           .Toggle("clerestory", "高窓あり", "高窓なし", true)
           .BeginGroup("clerestory")
           .IntSlider("clerestoryCount", "高窓の数(各面)", 1, 16, 8)
           .EndGroup();

        _ui.Heading("外装")
           .Toggle("pilaster", "柱型あり", "柱型なし", true)
           .BeginGroup("pilaster")
           .IntSlider("pilasterStep", "柱型の間隔", 4, 12, 6, "無柱スパンの割付。倉庫は大きめ")
           .EndGroup()
           .Toggle("baseCourse", "土台段あり", "土台段なし", true);

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:light_gray_concrete")
           .BlockPick("accent", "柱型", "minecraft:gray_concrete")
           .BlockPick("floor", "床", "minecraft:smooth_stone")
           .BlockPick("roofBlock", "屋根", "minecraft:gray_concrete")
           .BlockPick("parapetBlock", "パラペット", "minecraft:gray_concrete")
           .BlockPick("baseBlock", "土台", "minecraft:stone_bricks")
           .BlockPick("window", "高窓ガラス", "minecraft:glass");

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
        int usable = span - 2;                        // 両端1マスは壁として残す
        int slot = usable / count;
        if (slot < 2) return;                         // 1本ぶんの持ち幅が無い
        gw = Math.Clamp(Math.Min(gw, slot - 1), 1, usable);

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

    // 面の両端1マスを避けて等間隔に開口を並べる。
    private static void AddEven(List<Opening> ops, string face, string kind,
        int count, int span, int level, string? block)
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
                Block = block
            });
        }
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = _ui.GetInt("w");
        int d = _ui.GetInt("d");
        int h = _ui.GetInt("h");
        string roofType = _ui.GetChoice("roof", "gable");
        string ridge = _ui.GetChoice("ridge", "x");
        bool flatRoof = roofType == "flat";

        string wall = _ui.GetBlock("wall", "minecraft:light_gray_concrete");
        string accent = _ui.GetBlock("accent", "minecraft:gray_concrete");
        string floor = _ui.GetBlock("floor", "minecraft:smooth_stone");
        string roof = _ui.GetBlock("roofBlock", "minecraft:gray_concrete");
        string parapetBlock = _ui.GetBlock("parapetBlock", "minecraft:gray_concrete");
        string baseBlock = _ui.GetBlock("baseBlock", "minecraft:stone_bricks");
        string glass = _ui.GetBlock("window", "minecraft:glass");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        var ops = new List<Opening>();

        string gateFace = _ui.GetChoice("gateFace", "south");
        bool gateOnZ = gateFace == "north" || gateFace == "south";
        int gateSpan = gateOnZ ? w : d;
        int gates = _ui.GetInt("gates");
        AddGates(ops, gateFace, gates, gateSpan, _ui.GetInt("gateW"), _ui.GetInt("gateH"));

        // 通用口はシャッターと当たらないよう反対面に置く。
        if (_ui.GetBool("door"))
        {
            string doorFace = Opposite(gateFace);
            int doorSpan = (doorFace == "north" || doorFace == "south") ? w : d;
            AddEven(ops, doorFace, "door", 1, doorSpan, 1, null);
        }

        // 高窓。シャッター面にも入れるが、シャッターの上端より高い段に置いて衝突を避ける。
        if (_ui.GetBool("clerestory"))
        {
            int gateTop = gates > 0 ? _ui.GetInt("gateH") + 1 : 2;
            int level = Math.Max(gateTop, h - 3);
            if (level <= h - 2)
            {
                int n = _ui.GetInt("clerestoryCount");
                AddEven(ops, "north", "window", n, w, level, glass);
                AddEven(ops, "south", "window", n, w, level, glass);
                AddEven(ops, "east", "window", n, d, level, glass);
                AddEven(ops, "west", "window", n, d, level, glass);
            }
        }

        bool pilaster = _ui.GetBool("pilaster");
        int eave = flatRoof ? 0 : _ui.GetInt("eave");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roofType,
            RidgeAxis = ridge,
            RoofPitch = flatRoof ? 1 : Math.Clamp(_ui.GetInt("pitch"), 1, 4),
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roof,
            AccentBlock = accent,
            PilasterStep = pilaster ? Math.Max(4, _ui.GetInt("pilasterStep")) : 0,
            HasBase = _ui.GetBool("baseCourse"),
            BaseBlock = baseBlock,
            // パラペットは陸屋根のときだけ。勾配屋根では展開側が無視するので 0 を渡す。
            ParapetHeight = flatRoof ? _ui.GetInt("parapet") : 0,
            ParapetBlock = parapetBlock,
            Openings = ops,
            // 倉庫は内部を吹き抜けの一室にする。中間床も煙突も持たない。
            ChimneyCount = 0,
            EaveOverhang = eave,
            EaveNorth = true,
            EaveSouth = true,
            EaveEast = true,
            EaveWest = true
        };

        string roofNote = flatRoof
            ? $"陸屋根(パラペット{spec.ParapetHeight})"
            : $"{roofType}(勾配{spec.RoofPitch})";
        summary = $"{w}×{d}×{h} / {roofNote} / シャッター{gates}";
        return spec;
    }
}
