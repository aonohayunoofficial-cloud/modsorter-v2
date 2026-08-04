using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 塔。単体で立つ細長い箱なので、再現度の軸は「頂部の形」と「上端の作り」。
//   陸屋根＋胸壁 … 城の塔・櫓。最上段に狭間（クレネル）を抜いて凹凸を作る。
//   尖塔         … 時計塔・鐘塔。roof_pitch が鋭さ（何段ごとに1マス絞るか）になる。
//   ドーム／四角錐／切妻 … 既存の屋根ロジックをそのまま使う。
// 内部は階高ごとに床を入れ、各階の壁に縦長の窓（矢狭間）を並べる。
// 最上階の大開口（鐘楼・望楼の抜き）は窓より後に足して、重なった位置で抜きを勝たせる。
// 入口は正面に必ず1つ出すので、展開側の「ドアが無ければ自動で足す」処理は働かない。
public sealed class TowerParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public TowerParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("規模")
           .IntSlider("w", "幅", 5, 21, 9)
           .IntSlider("d", "奥行", 5, 21, 9)
           .IntSlider("h", "壁の高さ", 8, 64, 24, "頂部はこの上に載る")
           .IntSlider("fh", "階高", 3, 6, 4, "この間隔で内部に床が入り、窓もこの間隔で並ぶ")
           .Toggle("floorsOn", "内部に床あり", "内部に床なし", true);

        _ui.Heading("頂部")
           .Choice("cap", "頂部の形", new[]
           {
               ("陸屋根＋胸壁 (flat)", "flat"),
               ("尖塔 (spire)", "spire"),
               ("ドーム (dome)", "dome"),
               ("四角錐 (pyramid)", "pyramid"),
               ("切妻 (gable)", "gable"),
           }, "flat");

        _ui.BeginChoiceGroup("cap", "flat")
           .IntSlider("parapet", "胸壁の高さ", 0, 4, 2, "屋根の外周を上へ立ち上げる。0で無し")
           .Toggle("crenel", "狭間あり", "狭間なし", true)
           .BeginGroup("crenel")
           .IntSlider("crenelStep", "狭間の周期", 2, 6, 3, "3で矢壁2マス＋狭間1マス")
           .EndGroup()
           .EndGroup();

        _ui.BeginChoiceGroup("cap", "spire")
           .IntSlider("pitchS", "尖り", 1, 4, 2, "1=四角錐(45°) / 4=細く鋭い")
           .EndGroup();

        _ui.BeginChoiceGroup("cap", "dome")
           .IntSlider("domeH", "ドームの高さ", 2, 16, 5)
           .EndGroup();

        _ui.BeginChoiceGroup("cap", "gable")
           .Choice("ridge", "棟の向き", new[] { ("X軸", "x"), ("Z軸", "z") }, "x")
           .IntSlider("pitchG", "勾配", 1, 4, 1, "1=急(45°) / 4=緩やか")
           .EndGroup();

        _ui.Heading("窓")
           .Toggle("win", "窓あり", "窓なし", true)
           .BeginGroup("win")
           .IntSlider("winCount", "窓の数(各面・各階)", 1, 6, 2)
           .IntSlider("winRows", "窓の段数", 1, 3, 2, "2以上で縦長の矢狭間になる")
           .EndGroup();

        _ui.Heading("最上階の開口")
           .Note("鐘楼・望楼の抜き。壁の上端の近くを大きく抜く。")
           .Toggle("belfry", "開口あり", "開口なし", true)
           .BeginGroup("belfry")
           .IntSlider("belfryW", "開口の幅", 1, 7, 3)
           .IntSlider("belfryH", "開口の高さ", 2, 4, 3)
           .Choice("belfryFaces", "取り付け面",
               new[] { ("四面", "four"), ("正面のみ", "front") }, "four")
           .EndGroup();

        _ui.Heading("入口")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south")
           .IntSlider("entrance", "入口の幅", 1, 5, 1, "2以上で大開口。1なら片開きのドア");

        _ui.Heading("外装")
           .Toggle("pilaster", "方柱あり", "方柱なし", false)
           .BeginGroup("pilaster")
           .IntSlider("pilasterStep", "方柱の間隔", 4, 12, 4, "外周の柱型リズム")
           .EndGroup()
           .Toggle("baseCourse", "土台段あり", "土台段なし", true)
           .IntSlider("plinth", "基壇の張り出し", 0, 3, 1, "足元の外側へ床を敷き足す。0で無し");

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:stone_bricks")
           .BlockPick("accent", "方柱", "minecraft:polished_andesite")
           .BlockPick("floor", "床", "minecraft:spruce_planks")
           .BlockPick("roofBlock", "頂部", "minecraft:deepslate_tiles")
           .BlockPick("parapetBlock", "胸壁", "minecraft:stone_bricks")
           .BlockPick("baseBlock", "土台", "minecraft:cobblestone")
           .BlockPick("plinthBlock", "基壇", "minecraft:stone_bricks")
           .BlockPick("window", "窓ガラス", "minecraft:glass");

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

    private static Opening Gate(string face, int offset, int level, int width, int height)
        => new()
        {
            Face = face,
            Kind = "gate",
            Offset = offset,
            Level = level,
            Width = width,
            Height = height
        };

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = _ui.GetInt("w");
        int d = _ui.GetInt("d");
        int h = Math.Clamp(_ui.GetInt("h"), 8, 64);
        int fh = Math.Clamp(_ui.GetInt("fh"), 3, 6);

        string wall = _ui.GetBlock("wall", "minecraft:stone_bricks");
        string accent = _ui.GetBlock("accent", "minecraft:polished_andesite");
        string floor = _ui.GetBlock("floor", "minecraft:spruce_planks");
        string roof = _ui.GetBlock("roofBlock", "minecraft:deepslate_tiles");
        string parapetBlock = _ui.GetBlock("parapetBlock", "minecraft:stone_bricks");
        string baseBlock = _ui.GetBlock("baseBlock", "minecraft:cobblestone");
        string plinthBlock = _ui.GetBlock("plinthBlock", "minecraft:stone_bricks");
        string glass = _ui.GetBlock("window", "minecraft:glass");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        string cap = _ui.GetChoice("cap", "flat");
        string front = _ui.GetChoice("front", "south");
        int frontSpan = (front == "north" || front == "south") ? w : d;
        int center = frontSpan / 2;

        // 内部の床。階高ごとに入れる。1階の床(0)と屋根の領域(h-1)は別管理なので除く。
        var levels = new List<int>();
        if (_ui.GetBool("floorsOn"))
            for (int y = fh; y < h - 1; y += fh)
                levels.Add(y);

        int floors = Math.Max(1, (h - 1) / fh);
        var ops = new List<Opening>();

        // 窓。各階の床から2段目以上に、その階の範囲内で並べる。
        if (_ui.GetBool("win"))
        {
            int n = _ui.GetInt("winCount");
            int rows = Math.Clamp(_ui.GetInt("winRows"), 1, 3);
            for (int i = 0; i < floors; i++)
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

        // 最上階の開口。上端から開口の高さぶん下げた位置に置き、壁の範囲内へ収める。
        if (_ui.GetBool("belfry"))
        {
            int bh = Math.Clamp(_ui.GetInt("belfryH"), 2, 4);
            int level = Math.Max(2, h - 1 - bh);
            bh = Math.Min(bh, h - 1 - level);
            if (bh >= 2)
            {
                int bwX = Math.Min(_ui.GetInt("belfryW"), Math.Max(1, w - 2));
                int bwZ = Math.Min(_ui.GetInt("belfryW"), Math.Max(1, d - 2));
                if (_ui.GetChoice("belfryFaces", "four") == "four")
                {
                    ops.Add(Gate("north", w / 2, level, bwX, bh));
                    ops.Add(Gate("south", w / 2, level, bwX, bh));
                    ops.Add(Gate("east", d / 2, level, bwZ, bh));
                    ops.Add(Gate("west", d / 2, level, bwZ, bh));
                }
                else
                {
                    int bw = (front == "north" || front == "south") ? bwX : bwZ;
                    ops.Add(Gate(front, center, level, bw, bh));
                }
            }
        }

        // 入口。幅2以上は大開口で抜き、中心にドアを1つ置く。
        int entrance = Math.Clamp(_ui.GetInt("entrance"), 1, 5);
        if (entrance >= 2)
            ops.Add(Gate(front, center, 1,
                Math.Min(entrance, Math.Max(1, frontSpan - 2)), Math.Min(3, h - 2)));
        ops.Add(new Opening { Face = front, Kind = "door", Offset = center, Level = 1 });

        bool flatCap = cap == "flat";
        int parapet = flatCap ? _ui.GetInt("parapet") : 0;
        bool crenel = flatCap && parapet > 0 && _ui.GetBool("crenel");
        int plinth = _ui.GetInt("plinth");
        bool pilaster = _ui.GetBool("pilaster");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = cap,
            FacadeFace = front,
            RidgeAxis = _ui.GetChoice("ridge", "x"),
            RoofPitch = cap == "spire" ? _ui.GetInt("pitchS")
                      : cap == "gable" ? _ui.GetInt("pitchG")
                      : 1,
            DomeHeight = cap == "dome" ? _ui.GetInt("domeH") : (int?)null,
            FloorLevels = levels,
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roof,
            AccentBlock = accent,
            PilasterStep = pilaster ? Math.Max(4, _ui.GetInt("pilasterStep")) : 0,
            HasBase = _ui.GetBool("baseCourse"),
            BaseBlock = baseBlock,
            ParapetHeight = parapet,
            ParapetBlock = parapetBlock,
            ParapetCrenel = crenel,
            ParapetCrenelStep = _ui.GetInt("crenelStep"),
            VerandaWidth = plinth,
            VerandaBlock = plinthBlock,
            Openings = ops,
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        string capName = cap switch
        {
            "spire" => $"尖塔(尖り{spec.RoofPitch})",
            "dome" => $"ドーム{spec.DomeHeight}",
            "pyramid" => "四角錐",
            "gable" => $"切妻(勾配{spec.RoofPitch}/棟{spec.RidgeAxis})",
            _ => parapet > 0 ? $"陸屋根＋胸壁{parapet}{(crenel ? "(狭間)" : "")}" : "陸屋根"
        };
        string plinthNote = plinth > 0 ? $" / 基壇{plinth}" : "";
        summary = $"{w}×{d}×{h} / {floors}層(階高{fh}) / {capName}{plinthNote} / 正面{front}";
        return spec;
    }
}
