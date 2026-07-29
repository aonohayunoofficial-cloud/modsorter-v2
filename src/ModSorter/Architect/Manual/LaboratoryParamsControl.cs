using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 研究所。陸屋根＋パラペット、階ごとの横連窓、屋上の排気筒が再現度の軸。
// 全体高さは 階数×階高＋1（最上段が屋根面）。中間床は階高ごとに自動で入る。
public sealed class LaboratoryParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public LaboratoryParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .IntSlider("w", "幅", 7, 64, 25)
           .IntSlider("d", "奥行", 7, 64, 15)
           .IntSlider("floors", "階数", 1, 8, 3)
           .IntSlider("fh", "階高", 3, 6, 4, "1階ぶんの高さ。全体高さ = 階数×階高＋1");

        _ui.Heading("屋根")
           .Note("研究所は陸屋根が基本。屋上端のパラペットで輪郭を作る。")
           .IntSlider("parapet", "パラペット", 0, 4, 1, "屋根の外周を上へ立ち上げる高さ。0で無し");

        _ui.Heading("開口")
           .Choice("entranceFace", "正面（入口）", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south")
           .Toggle("ribbon", "横連窓あり", "横連窓なし", true)
           .BeginGroup("ribbon")
           .IntSlider("ribbonCount", "窓の数(各面・各階)", 1, 16, 8)
           .EndGroup();

        _ui.Heading("排気筒")
           .Toggle("stack", "排気筒あり", "排気筒なし", true)
           .BeginGroup("stack")
           .IntSlider("stackCount", "本数", 1, 4, 2)
           .IntSlider("stackHeight", "高さ", 2, 24, 6)
           .Choice("stackAlign", "寄せ方向", new[]
           {
               ("中央", "center"), ("北寄せ", "north"), ("南寄せ", "south"),
               ("東寄せ", "east"), ("西寄せ", "west"),
           }, "center")
           .Choice("stackThick", "太さ", new[]
           {
               ("細（1マス）", "thin"),
               ("中（ひし形・中空）", "medium"),
               ("太（4×4・中空）", "thick"),
           }, "thin")
           .BlockPick("stackBlock", "排気筒", "minecraft:iron_block")
           .EndGroup();

        _ui.Heading("外装")
           .Toggle("pilaster", "柱型あり", "柱型なし", true)
           .BeginGroup("pilaster")
           .IntSlider("pilasterStep", "柱型の間隔", 4, 12, 6)
           .EndGroup()
           .Toggle("baseCourse", "土台段あり", "土台段なし", true);

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:smooth_quartz")
           .BlockPick("accent", "柱型", "minecraft:quartz_pillar")
           .BlockPick("floor", "床", "minecraft:light_gray_concrete")
           .BlockPick("roofBlock", "屋根", "minecraft:smooth_stone")
           .BlockPick("parapetBlock", "パラペット", "minecraft:light_gray_concrete")
           .BlockPick("baseBlock", "土台", "minecraft:stone_bricks")
           .BlockPick("window", "窓ガラス", "minecraft:light_blue_stained_glass");

        Content = _ui.Root;
    }

    // 面の両端1マスを避けて等間隔に窓を並べる。角に窓が乗って隅が抜けるのを防ぐ。
    private static void AddEven(List<Opening> ops, string face,
        int count, int span, int level, string block)
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
                Kind = "window",
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
        int floors = Math.Max(1, _ui.GetInt("floors"));
        int fh = Math.Clamp(_ui.GetInt("fh"), 3, 6);
        // 展開側の上限が64なので、そこに収まる範囲まで階数を落とす。
        while (floors > 1 && floors * fh + 1 > 64) floors--;
        int h = Math.Min(64, floors * fh + 1);

        string wall = _ui.GetBlock("wall", "minecraft:smooth_quartz");
        string accent = _ui.GetBlock("accent", "minecraft:quartz_pillar");
        string floor = _ui.GetBlock("floor", "minecraft:light_gray_concrete");
        string roof = _ui.GetBlock("roofBlock", "minecraft:smooth_stone");
        string parapetBlock = _ui.GetBlock("parapetBlock", "minecraft:light_gray_concrete");
        string baseBlock = _ui.GetBlock("baseBlock", "minecraft:stone_bricks");
        string glass = _ui.GetBlock("window", "minecraft:light_blue_stained_glass");
        string stackBlock = _ui.GetBlock("stackBlock", "minecraft:iron_block");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        // 中間床。1階の床(y=0)と屋根(y=h-1)は別管理なので、その間だけ入れる。
        var levels = new List<int>();
        for (int i = 1; i < floors; i++)
        {
            int y = i * fh;
            if (y > 0 && y < h - 1) levels.Add(y);
        }

        var ops = new List<Opening>();

        // 横連窓。各階の床から2段目を窓の高さにする。窓を先に置き、入口を後に置いて
        // 同じ位置に重なったときは入口が勝つようにする（開口の適用は後勝ち）。
        if (_ui.GetBool("ribbon"))
        {
            int n = _ui.GetInt("ribbonCount");
            for (int i = 0; i < floors; i++)
            {
                int level = i * fh + 2;
                if (level > h - 2) break;
                AddEven(ops, "north", n, w, level, glass);
                AddEven(ops, "south", n, w, level, glass);
                AddEven(ops, "east", n, d, level, glass);
                AddEven(ops, "west", n, d, level, glass);
            }
        }

        string face = _ui.GetChoice("entranceFace", "south");
        int entranceSpan = (face == "north" || face == "south") ? w : d;
        ops.Add(new Opening
        {
            Face = face,
            Kind = "door",
            Offset = entranceSpan / 2,
            Level = 1
        });

        bool stack = _ui.GetBool("stack");
        bool pilaster = _ui.GetBool("pilaster");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = "flat",
            FacadeFace = face,
            FloorLevels = levels,
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roof,
            AccentBlock = accent,
            PilasterStep = pilaster ? Math.Max(4, _ui.GetInt("pilasterStep")) : 0,
            HasBase = _ui.GetBool("baseCourse"),
            BaseBlock = baseBlock,
            ParapetHeight = _ui.GetInt("parapet"),
            ParapetBlock = parapetBlock,
            Openings = ops,
            ChimneyCount = stack ? _ui.GetInt("stackCount") : 0,
            ChimneyHeight = _ui.GetInt("stackHeight"),
            ChimneyAlign = _ui.GetChoice("stackAlign", "center"),
            ChimneyThickness = _ui.GetChoice("stackThick", "thin"),
            ChimneyPierce = false,
            ChimneyBlock = stackBlock,
            // 陸屋根なので軒は出さない。
            EaveOverhang = 0
        };

        summary = $"{w}×{d} / {floors}階(階高{fh}) / パラペット{_ui.GetInt("parapet")} / 排気筒{spec.ChimneyCount}";
        return spec;
    }
}
