using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// オフィスビル。基準階の反復、水平連続窓（カーテンウォール）、
// 陸屋根＋パラペット、屋上の塔屋が再現度の軸。
// 全体高さは 階数×階高＋1（最上段が屋根面）。
public sealed class OfficeParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public OfficeParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .IntSlider("w", "幅", 9, 64, 21)
           .IntSlider("d", "奥行", 9, 64, 21)
           .IntSlider("floors", "階数", 2, 16, 6)
           .IntSlider("fh", "階高", 3, 6, 4, "1階ぶんの高さ。全体高さ = 階数×階高＋1");

        _ui.Heading("屋根")
           .Note("オフィスは陸屋根。屋上端のパラペットと塔屋で輪郭を作る。")
           .IntSlider("parapet", "パラペット", 0, 4, 2, "屋根の外周を上へ立ち上げる高さ。0で無し")
           .Toggle("penthouse", "塔屋あり", "塔屋なし", true)
           .BeginGroup("penthouse")
           .IntSlider("phW", "塔屋の幅", 3, 32, 7)
           .IntSlider("phD", "塔屋の奥行", 3, 32, 7)
           .IntSlider("phH", "塔屋の高さ", 2, 12, 4)
           .Choice("phAlign", "寄せ方向", new[]
           {
               ("中央", "center"),
               ("北寄せ", "north"), ("南寄せ", "south"),
               ("東寄せ", "east"), ("西寄せ", "west"),
               ("北東の角", "northeast"), ("北西の角", "northwest"),
               ("南東の角", "southeast"), ("南西の角", "southwest"),
           }, "center")
           .EndGroup();

        _ui.Heading("窓")
           .Toggle("curtain", "連続窓あり", "連続窓なし", true)
           .BeginGroup("curtain")
           .IntSlider("winCount", "窓の数(各面・各階)", 1, 24, 12)
           .IntSlider("winRows", "窓の段数", 1, 2, 2, "2段で水平連続窓に近くなる")
           .EndGroup();

        _ui.Heading("エントランス")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south")
           .IntSlider("entrance", "入口の幅", 1, 9, 5, "2以上で大開口。1なら片開きのドア");

        _ui.Heading("外装")
           .Toggle("pilaster", "方柱あり", "方柱なし", true)
           .BeginGroup("pilaster")
           .IntSlider("pilasterStep", "方柱の間隔", 4, 12, 4, "外周の柱型リズム")
           .EndGroup()
           .Toggle("baseCourse", "土台段あり", "土台段なし", true);

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:light_gray_concrete")
           .BlockPick("accent", "方柱", "minecraft:smooth_stone")
           .BlockPick("floor", "床", "minecraft:smooth_stone")
           .BlockPick("roofBlock", "屋根", "minecraft:gray_concrete")
           .BlockPick("parapetBlock", "パラペット", "minecraft:smooth_stone")
           .BlockPick("phBlock", "塔屋", "minecraft:light_gray_concrete")
           .BlockPick("baseBlock", "土台", "minecraft:polished_andesite")
           .BlockPick("window", "窓ガラス", "minecraft:light_blue_stained_glass");

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

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = _ui.GetInt("w");
        int d = _ui.GetInt("d");
        int floors = Math.Max(1, _ui.GetInt("floors"));
        int fh = Math.Clamp(_ui.GetInt("fh"), 3, 6);
        while (floors > 1 && floors * fh + 1 > 64) floors--;
        int h = Math.Min(64, floors * fh + 1);

        string wall = _ui.GetBlock("wall", "minecraft:light_gray_concrete");
        string accent = _ui.GetBlock("accent", "minecraft:smooth_stone");
        string floor = _ui.GetBlock("floor", "minecraft:smooth_stone");
        string roof = _ui.GetBlock("roofBlock", "minecraft:gray_concrete");
        string parapetBlock = _ui.GetBlock("parapetBlock", "minecraft:smooth_stone");
        string phBlock = _ui.GetBlock("phBlock", "minecraft:light_gray_concrete");
        string baseBlock = _ui.GetBlock("baseBlock", "minecraft:polished_andesite");
        string glass = _ui.GetBlock("window", "minecraft:light_blue_stained_glass");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        var levels = new List<int>();
        for (int i = 1; i < floors; i++)
        {
            int y = i * fh;
            if (y > 0 && y < h - 1) levels.Add(y);
        }

        var ops = new List<Opening>();

        // 連続窓。各階の床から2段目（必要なら3段目も）を窓にする。
        // 階の天井（次の床）に窓が食い込まないよう、その階の範囲内に収める。
        if (_ui.GetBool("curtain"))
        {
            int n = _ui.GetInt("winCount");
            int rows = Math.Clamp(_ui.GetInt("winRows"), 1, 2);
            for (int i = 0; i < floors; i++)
            {
                for (int r = 0; r < rows; r++)
                {
                    int level = i * fh + 2 + r;
                    if (level > h - 2) break;
                    if (i < floors - 1 && level >= (i + 1) * fh) break;
                    AddEven(ops, "north", "window", n, w, level, glass);
                    AddEven(ops, "south", "window", n, w, level, glass);
                    AddEven(ops, "east", "window", n, d, level, glass);
                    AddEven(ops, "west", "window", n, d, level, glass);
                }
            }
        }

        // エントランス。幅2以上は大開口で抜き、中心にドアを1つ置いて
        // 展開側の「ドアが無ければ自動で足す」処理が別の面に穴を開けるのを防ぐ。
        string front = _ui.GetChoice("front", "south");
        int frontSpan = (front == "north" || front == "south") ? w : d;
        int center = frontSpan / 2;
        int entrance = Math.Clamp(_ui.GetInt("entrance"), 1, 9);
        if (entrance >= 2)
        {
            ops.Add(new Opening
            {
                Face = front,
                Kind = "gate",
                Offset = center,
                Level = 1,
                Width = Math.Min(entrance, frontSpan - 2),
                Height = Math.Min(3, fh - 1)
            });
        }
        ops.Add(new Opening
        {
            Face = front,
            Kind = "door",
            Offset = center,
            Level = 1
        });

        bool penthouse = _ui.GetBool("penthouse");
        bool pilaster = _ui.GetBool("pilaster");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = "flat",
            FacadeFace = front,
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
            // 塔屋は建物より小さくなるよう、幅・奥行から2マスぶん内側までに抑える。
            PenthouseWidth = penthouse ? Math.Min(_ui.GetInt("phW"), Math.Max(0, w - 4)) : 0,
            PenthouseDepth = penthouse ? Math.Min(_ui.GetInt("phD"), Math.Max(0, d - 4)) : 0,
            PenthouseHeight = penthouse ? _ui.GetInt("phH") : 0,
            PenthouseBlock = phBlock,
            PenthouseAlign = _ui.GetChoice("phAlign", "center"),
            Openings = ops,
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        string phNote = spec.PenthouseHeight > 0
            ? $"塔屋{spec.PenthouseWidth}×{spec.PenthouseDepth}×{spec.PenthouseHeight}"
              + $"({spec.PenthouseAlign})"
            : "塔屋なし";
        summary = $"{w}×{d} / {floors}階(階高{fh}) / パラペット{spec.ParapetHeight} / {phNote}";
        return spec;
    }
}
