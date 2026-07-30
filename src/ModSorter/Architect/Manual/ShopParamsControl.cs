using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 店舗・商業施設。1階の全面ショーウィンドウ、上階の規則的な窓、
// 柱型で割った間口、陸屋根＋パラペット（看板帯に見える屋上端）が再現度の軸。
// 全体高さは 階数×階高＋1（最上段が屋根面）。
public sealed class ShopParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public ShopParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .IntSlider("w", "幅", 7, 64, 17)
           .IntSlider("d", "奥行", 7, 64, 13)
           .IntSlider("floors", "階数", 1, 4, 2)
           .IntSlider("fh", "階高", 3, 6, 4, "1階ぶんの高さ。全体高さ = 階数×階高＋1");

        _ui.Heading("屋根")
           .Choice("roof", "屋根形式", new[]
           {
               ("陸屋根 (flat)", "flat"),
               ("切妻 (gable)", "gable"),
           }, "flat");

        _ui.BeginChoiceGroup("roof", "flat")
           .IntSlider("parapet", "パラペット", 0, 4, 2, "屋上端の立ち上がり。看板帯として見える")
           .EndGroup();

        _ui.BeginChoiceGroup("roof", "gable")
           .Choice("ridge", "棟の向き", new[] { ("X軸", "x"), ("Z軸", "z") }, "x")
           .IntSlider("pitch", "勾配", 1, 4, 2, "1=急(45°) / 4=緩やか")
           .IntSlider("eave", "軒の出", 0, 3, 1)
           .EndGroup();

        _ui.Heading("店構え")
           .Choice("front", "正面（間口）", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south")
           .Toggle("showcase", "ショーウィンドウあり", "ショーウィンドウなし", true)
           .BeginGroup("showcase")
           .Toggle("showcaseSide", "側面も張る", "正面だけ", false)
           .EndGroup()
           .IntSlider("entrance", "入口の幅", 1, 5, 3, "2以上で大開口。1なら片開きのドア");

        _ui.Heading("上階の窓")
           .Toggle("upperWin", "上階の窓あり", "上階の窓なし", true)
           .BeginGroup("upperWin")
           .IntSlider("upperCount", "窓の数(各面・各階)", 1, 16, 5)
           .EndGroup();

        _ui.Heading("外装")
           .Toggle("pilaster", "柱型あり", "柱型なし", true)
           .BeginGroup("pilaster")
           .IntSlider("pilasterStep", "柱型の間隔", 4, 12, 4, "間口の割付。店舗は小さめ")
           .EndGroup()
           .Toggle("baseCourse", "土台段あり", "土台段なし", true);

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:white_terracotta")
           .BlockPick("accent", "柱型", "minecraft:polished_andesite")
           .BlockPick("floor", "床", "minecraft:polished_andesite")
           .BlockPick("roofBlock", "屋根", "minecraft:deepslate_tiles")
           .BlockPick("parapetBlock", "パラペット・看板帯", "minecraft:dark_oak_planks")
           .BlockPick("baseBlock", "土台", "minecraft:polished_blackstone")
           .BlockPick("showcaseGlass", "ショーウィンドウ", "minecraft:glass")
           .BlockPick("windowGlass", "上階の窓", "minecraft:glass_pane");

        Content = _ui.Root;
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

    // 1面ぶんのショーウィンドウ。腰壁(y=1)を残し、その上を階高いっぱいまでガラスで埋める。
    private static void AddShowcase(List<Opening> ops, string face,
        int span, int fh, string glass)
    {
        for (int y = 2; y <= fh - 1; y++)
            AddEven(ops, face, "window", span - 2, span, y, glass);
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = _ui.GetInt("w");
        int d = _ui.GetInt("d");
        int floors = Math.Max(1, _ui.GetInt("floors"));
        int fh = Math.Clamp(_ui.GetInt("fh"), 3, 6);
        while (floors > 1 && floors * fh + 1 > 64) floors--;
        int h = Math.Min(64, floors * fh + 1);

        string roofType = _ui.GetChoice("roof", "flat");
        bool flatRoof = roofType == "flat";

        string wall = _ui.GetBlock("wall", "minecraft:white_terracotta");
        string accent = _ui.GetBlock("accent", "minecraft:polished_andesite");
        string floor = _ui.GetBlock("floor", "minecraft:polished_andesite");
        string roof = _ui.GetBlock("roofBlock", "minecraft:deepslate_tiles");
        string parapetBlock = _ui.GetBlock("parapetBlock", "minecraft:dark_oak_planks");
        string baseBlock = _ui.GetBlock("baseBlock", "minecraft:polished_blackstone");
        string showcaseGlass = _ui.GetBlock("showcaseGlass", "minecraft:glass");
        string windowGlass = _ui.GetBlock("windowGlass", "minecraft:glass_pane");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        var levels = new List<int>();
        for (int i = 1; i < floors; i++)
        {
            int y = i * fh;
            if (y > 0 && y < h - 1) levels.Add(y);
        }

        string front = _ui.GetChoice("front", "south");
        bool frontOnZ = front == "north" || front == "south";
        int frontSpan = frontOnZ ? w : d;
        int sideSpan = frontOnZ ? d : w;
        string sideA = frontOnZ ? "east" : "north";
        string sideB = frontOnZ ? "west" : "south";

        var ops = new List<Opening>();

        // ショーウィンドウ → 上階の窓 → 入口の順に積む。開口の適用は後勝ちなので、
        // 最後に置いた入口がショーウィンドウのガラスを必ず貫く。
        if (_ui.GetBool("showcase"))
        {
            AddShowcase(ops, front, frontSpan, fh, showcaseGlass);
            if (_ui.GetBool("showcaseSide"))
            {
                AddShowcase(ops, sideA, sideSpan, fh, showcaseGlass);
                AddShowcase(ops, sideB, sideSpan, fh, showcaseGlass);
            }
        }

        if (_ui.GetBool("upperWin"))
        {
            int n = _ui.GetInt("upperCount");
            for (int i = 1; i < floors; i++)
            {
                int level = i * fh + 2;
                if (level > h - 2) break;
                AddEven(ops, "north", "window", n, w, level, windowGlass);
                AddEven(ops, "south", "window", n, w, level, windowGlass);
                AddEven(ops, "east", "window", n, d, level, windowGlass);
                AddEven(ops, "west", "window", n, d, level, windowGlass);
            }
        }

        // 入口。幅2以上は大開口(gate)で抜き、その中心にドアを1つ置いて
        // 展開側の「ドアが無ければ自動で足す」処理が別の面に穴を開けるのを防ぐ。
        int entrance = Math.Clamp(_ui.GetInt("entrance"), 1, 5);
        int entranceCenter = frontSpan / 2;
        if (entrance >= 2)
        {
            ops.Add(new Opening
            {
                Face = front,
                Kind = "gate",
                Offset = entranceCenter,
                Level = 1,
                Width = Math.Min(entrance, frontSpan - 2),
                Height = Math.Min(3, fh - 1)
            });
        }
        ops.Add(new Opening
        {
            Face = front,
            Kind = "door",
            Offset = entranceCenter,
            Level = 1
        });

        bool pilaster = _ui.GetBool("pilaster");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roofType,
            RidgeAxis = _ui.GetChoice("ridge", "x"),
            RoofPitch = flatRoof ? 1 : Math.Clamp(_ui.GetInt("pitch"), 1, 4),
            FacadeFace = front,
            FloorLevels = levels,
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roof,
            AccentBlock = accent,
            PilasterStep = pilaster ? Math.Max(4, _ui.GetInt("pilasterStep")) : 0,
            HasBase = _ui.GetBool("baseCourse"),
            BaseBlock = baseBlock,
            ParapetHeight = flatRoof ? _ui.GetInt("parapet") : 0,
            ParapetBlock = parapetBlock,
            Openings = ops,
            ChimneyCount = 0,
            EaveOverhang = flatRoof ? 0 : _ui.GetInt("eave"),
            EaveNorth = true,
            EaveSouth = true,
            EaveEast = true,
            EaveWest = true
        };

        string roofNote = flatRoof
            ? $"陸屋根(パラペット{spec.ParapetHeight})"
            : $"切妻(勾配{spec.RoofPitch})";
        summary = $"{w}×{d} / {floors}階(階高{fh}) / {roofNote} / 入口幅{entrance}";
        return spec;
    }
}
