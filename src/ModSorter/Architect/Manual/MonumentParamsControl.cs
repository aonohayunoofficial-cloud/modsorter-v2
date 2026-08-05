using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 記念建築。人が住む箱ではなく「見せるための塊」なので、再現度の軸は
// 台座・本体・頂部の3段構成と、余計な入口を開けないこと（no_entrance）。
//   オベリスク     … 細い角柱＋尖塔。台座に載せる。
//   凱旋門         … 厚い箱を半円アーチで前後に貫く。上端のアティックはパラペットで作る。
//   記念柱         … 円形平面の柱身。頂部は像台（縁のみ）／ドーム／円錐。
//   石碑・記念壁   … 厚み2〜5の板。笠石（パラペット）と基壇だけで輪郭を作る。
//   霊廟           … ドーム＋アーチ。平面は方形と円形から選ぶ。
//   階段ピラミッド … 段ごとに縮む箱を volumes で積み、頂上に社を載せる。
//
// 台座付き（オベリスク・記念柱）と階段ピラミッドは volumes で複数の箱を積む。
// volumes は部品ごとに座標が 0 起点へ正規化されるため、負座標を作る要素（縁側・軒）を
// 部品に付けると位置がずれる。この2系統では縁側・軒を一切使わない。
public sealed class MonumentParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 使用ブロックをまとめて持ち回る。
    private sealed class Blocks
    {
        public string Body = "";
        public string Accent = "";
        public string Cap = "";
        public string Base = "";
        public string Plinth = "";
    }

    public MonumentParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("種類")
           .Choice("kind", "記念建築の種類", new[]
           {
               ("オベリスク", "obelisk"),
               ("凱旋門", "arch"),
               ("記念柱（円形）", "column"),
               ("石碑・記念壁", "stele"),
               ("霊廟", "mausoleum"),
               ("階段ピラミッド", "ziggurat"),
           }, "obelisk");

        _ui.Heading("向き")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south");

        // ===== オベリスク =====
        _ui.BeginChoiceGroup("kind", "obelisk")
           .Heading("オベリスク")
           .Note("細い角柱の上に尖塔を載せる。台座は下に別の箱として積む。")
           .IntSlider("obW", "柱身の幅", 3, 9, 5, "偶数は自動で+1し、中心を1マスに保つ")
           .IntSlider("obH", "柱身の高さ", 12, 48, 24)
           .IntSlider("obPitch", "尖り", 1, 4, 3, "1=四角錐(45°) / 4=細く鋭い")
           .IntSlider("obPed", "台座の高さ", 0, 8, 3, "0で台座なし")
           .IntSlider("obPedOut", "台座の張り出し", 1, 4, 2)
           .EndGroup();

        // ===== 凱旋門 =====
        _ui.BeginChoiceGroup("kind", "arch")
           .Heading("凱旋門")
           .Note("正面と背面に同じアーチを開け、内部を通路として貫く。")
           .IntSlider("arW", "幅", 11, 31, 15)
           .IntSlider("arD", "奥行", 5, 15, 7, "通路の長さ")
           .IntSlider("arH", "高さ", 11, 31, 17)
           .IntSlider("arMW", "主アーチの幅", 3, 15, 7)
           .IntSlider("arMH", "主アーチの高さ", 5, 21, 11)
           .Toggle("arSide", "側面アーチあり", "側面アーチなし", false)
           .BeginGroup("arSide")
           .IntSlider("arSW", "側面アーチの幅", 3, 9, 5)
           .IntSlider("arSH", "側面アーチの高さ", 4, 13, 7)
           .EndGroup()
           .IntSlider("arAttic", "アティックの高さ", 0, 4, 3, "上端の立ち上がり。0で無し")
           .Toggle("arPil", "方柱あり", "方柱なし", true)
           .BeginGroup("arPil")
           .IntSlider("arPilStep", "方柱の間隔", 4, 12, 5)
           .EndGroup()
           .IntSlider("arPlinth", "基壇の張り出し", 0, 3, 1)
           .EndGroup();

        // ===== 記念柱 =====
        _ui.BeginChoiceGroup("kind", "column")
           .Heading("記念柱")
           .Note("円形平面の柱身。方形の台座に載せる。")
           .IntSlider("coDiam", "柱身の直径", 5, 13, 7, "偶数は自動で+1し、中心を1マスに保つ")
           .IntSlider("coH", "柱身の高さ", 12, 48, 28)
           .Choice("coCap", "頂部", new[]
           {
               ("像台（縁のみ）", "flat"),
               ("ドーム", "dome"),
               ("円錐", "cone"),
           }, "flat")
           .BeginChoiceGroup("coCap", "cone")
           .IntSlider("coPitch", "円錐の尖り", 1, 4, 2)
           .EndGroup()
           .BeginChoiceGroup("coCap", "dome")
           .IntSlider("coDomeH", "ドームの高さ", 3, 12, 5)
           .EndGroup()
           .IntSlider("coPed", "台座の高さ", 0, 8, 4, "0で台座なし")
           .IntSlider("coPedOut", "台座の張り出し", 1, 4, 2)
           .EndGroup();

        // ===== 石碑・記念壁 =====
        _ui.BeginChoiceGroup("kind", "stele")
           .Heading("石碑・記念壁")
           .Note("厚みが薄いので外周だけで中身が埋まり、実質中実の板になる。")
           .IntSlider("stW", "幅", 5, 21, 9)
           .IntSlider("stD", "厚み", 2, 5, 2)
           .IntSlider("stH", "高さ", 5, 21, 11)
           .IntSlider("stCop", "笠石の高さ", 0, 4, 1, "上端の立ち上がり。0で無し")
           .IntSlider("stPlinth", "基壇の張り出し", 0, 3, 1)
           .Toggle("stRib", "縦リブあり", "縦リブなし", false)
           .BeginGroup("stRib")
           .IntSlider("stRibStep", "リブの間隔", 4, 8, 4)
           .EndGroup()
           .EndGroup();

        // ===== 霊廟 =====
        _ui.BeginChoiceGroup("kind", "mausoleum")
           .Heading("霊廟")
           .Choice("maPlan", "平面", new[] { ("方形", "square"), ("円形", "circle") }, "square")
           .IntSlider("maW", "幅", 11, 31, 17, "円形ではこれが直径になる")
           .BeginChoiceGroup("maPlan", "square")
           .IntSlider("maD", "奥行", 11, 31, 17)
           .Choice("maFaces", "アーチの面", new[] { ("四面", "four"), ("正面のみ", "front") }, "four")
           .EndGroup()
           .IntSlider("maH", "壁の高さ", 6, 20, 10)
           .Choice("maCap", "頂部", new[]
           {
               ("ドーム", "dome"),
               ("四角錐", "pyramid"),
               ("陸屋根", "flat"),
           }, "dome")
           .BeginChoiceGroup("maCap", "dome")
           .IntSlider("maDomeH", "ドームの高さ", 3, 16, 8)
           .EndGroup()
           .IntSlider("maArchW", "アーチの幅", 3, 9, 5, "円形平面では5までに抑えられる")
           .IntSlider("maArchH", "アーチの高さ", 4, 13, 7)
           .Toggle("maBase", "土台段あり", "土台段なし", true)
           .IntSlider("maPlinth", "基壇の張り出し", 0, 3, 1)
           .EndGroup();

        // ===== 階段ピラミッド =====
        _ui.BeginChoiceGroup("kind", "ziggurat")
           .Heading("階段ピラミッド")
           .Note("段ごとに縮む箱を積む。段数は寸法から自動で抑えられる。")
           .IntSlider("ziW", "基部の幅", 15, 41, 25)
           .IntSlider("ziD", "基部の奥行", 15, 41, 25)
           .IntSlider("ziTiers", "段数", 2, 6, 4)
           .IntSlider("ziTierH", "1段の高さ", 2, 6, 4)
           .IntSlider("ziInset", "段ごとの後退", 2, 6, 3)
           .Toggle("ziShrine", "頂上の社あり", "頂上の社なし", true)
           .BeginGroup("ziShrine")
           .IntSlider("ziShrineH", "社の高さ", 4, 12, 7)
           .Choice("ziShrineRoof", "社の屋根",
               new[] { ("四角錐", "pyramid"), ("陸屋根", "flat") }, "pyramid")
           .EndGroup()
           .EndGroup();

        _ui.Heading("使用ブロック")
           .BlockPick("body", "本体", "minecraft:smooth_quartz")
           .BlockPick("accent", "装飾", "minecraft:chiseled_quartz_block")
           .BlockPick("cap", "頂部", "minecraft:quartz_block")
           .BlockPick("baseBlock", "台座・土台", "minecraft:polished_diorite")
           .BlockPick("plinthBlock", "基壇", "minecraft:polished_diorite");

        Content = _ui.Root;
    }

    private static string Opposite(string face) => face switch
    {
        "north" => "south",
        "south" => "north",
        "east" => "west",
        _ => "east"
    };

    // 正面に直交する2面。正面が north/south なら east/west、east/west なら north/south。
    private static string[] SideFaces(string face)
        => (face == "north" || face == "south")
            ? new[] { "east", "west" }
            : new[] { "north", "south" };

    // 台座（方形の箱）の上に本体を載せる。台座の高さが0なら本体だけを返す。
    // 台座は本体より outw マス外へ張り出し、本体はその中央に載る。
    // 本体の床(y=0)が台座の天面と同じ高さに来るので、後勝ちで継ぎ目なく繋がる。
    private static StructureSpec Pedestal(
        StructureSpec body, int bodyW, int bodyD, int bodyH,
        int pedH, int outw, string block, string top)
    {
        if (pedH <= 0) return body;

        var ped = new StructureSpec
        {
            Width = bodyW + outw * 2,
            Depth = bodyD + outw * 2,
            Height = pedH + 1,
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = "flat",
            WallBlock = block,
            FloorBlock = block,
            RoofBlock = top,
            NoEntrance = true,
            Openings = new List<Opening>()
        };

        return new StructureSpec
        {
            Width = ped.Width,
            Depth = ped.Depth,
            Height = pedH + bodyH,
            Volumes = new List<VolumePart>
            {
                new() { OffsetX = 0, OffsetY = 0, OffsetZ = 0, Part = ped },
                new() { OffsetX = outw, OffsetY = pedH, OffsetZ = outw, Part = body },
            }
        };
    }

    private (StructureSpec Spec, string Summary) BuildObelisk(Blocks b, string front)
    {
        int w = _ui.GetInt("obW");
        if (w % 2 == 0) w++;
        int h = _ui.GetInt("obH");
        int pitch = _ui.GetInt("obPitch");

        var shaft = new StructureSpec
        {
            Width = w,
            Depth = w,
            Height = h,
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = "spire",
            RoofPitch = pitch,
            FacadeFace = front,
            WallBlock = b.Body,
            FloorBlock = b.Body,
            RoofBlock = b.Cap,
            AccentBlock = b.Accent,
            PilasterStep = 0,
            NoEntrance = true,
            Openings = new List<Opening>(),
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        int pedH = _ui.GetInt("obPed");
        int outw = _ui.GetInt("obPedOut");
        var spec = Pedestal(shaft, w, w, h, pedH, outw, b.Base, b.Cap);

        string ped = pedH > 0 ? $" / 台座{pedH}(張り出し{outw})" : "";
        return (spec, $"オベリスク 柱身{w}×{w}×{h} / 尖り{pitch}{ped}");
    }

    private (StructureSpec Spec, string Summary) BuildTriumphalArch(Blocks b, string front)
    {
        int w = _ui.GetInt("arW");
        int d = _ui.GetInt("arD");
        int h = _ui.GetInt("arH");

        bool frontAlongX = front == "north" || front == "south";
        int frontSpan = frontAlongX ? w : d;
        int sideSpan = frontAlongX ? d : w;

        int mainW = Math.Clamp(_ui.GetInt("arMW"), 3, Math.Max(3, frontSpan - 4));
        int mainH = Math.Clamp(_ui.GetInt("arMH"), 3, Math.Max(3, h - 3));

        var ops = new List<Opening>
        {
            new() { Face = front, Kind = "arch", Offset = frontSpan / 2, Width = mainW, Height = mainH },
            new() { Face = Opposite(front), Kind = "arch", Offset = frontSpan / 2, Width = mainW, Height = mainH },
        };

        bool side = _ui.GetBool("arSide");
        if (side)
        {
            int sw = Math.Clamp(_ui.GetInt("arSW"), 3, Math.Max(3, sideSpan - 4));
            int sh = Math.Clamp(_ui.GetInt("arSH"), 3, Math.Max(3, mainH - 2));
            foreach (string f in SideFaces(front))
                ops.Add(new Opening
                {
                    Face = f,
                    Kind = "arch",
                    Offset = sideSpan / 2,
                    Width = sw,
                    Height = sh
                });
        }

        int attic = _ui.GetInt("arAttic");
        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = "flat",
            FacadeFace = front,
            WallBlock = b.Body,
            FloorBlock = b.Body,
            RoofBlock = b.Cap,
            AccentBlock = b.Accent,
            PilasterStep = _ui.GetBool("arPil") ? Math.Max(4, _ui.GetInt("arPilStep")) : 0,
            HasBase = true,
            BaseBlock = b.Base,
            ParapetHeight = attic,
            ParapetBlock = b.Accent,
            VerandaWidth = _ui.GetInt("arPlinth"),
            VerandaBlock = b.Plinth,
            NoEntrance = true,
            Openings = ops,
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        string sideNote = side ? " / 側面アーチあり" : "";
        string atticNote = attic > 0 ? $" / アティック{attic}" : "";
        return (spec,
            $"凱旋門 {w}×{d}×{h} / 主アーチ幅{mainW}×高{mainH}{sideNote}{atticNote} / 正面{front}");
    }

    private (StructureSpec Spec, string Summary) BuildMemorialColumn(Blocks b, string front)
    {
        int diam = _ui.GetInt("coDiam");
        if (diam % 2 == 0) diam++;
        int h = _ui.GetInt("coH");
        string cap = _ui.GetChoice("coCap", "flat");

        var shaft = new StructureSpec
        {
            Width = diam,
            Depth = diam,
            Height = h,
            FootprintShape = "circle",
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = cap == "cone" ? "spire" : cap == "dome" ? "dome" : "flat",
            RoofPitch = cap == "cone" ? _ui.GetInt("coPitch") : 1,
            DomeHeight = cap == "dome" ? _ui.GetInt("coDomeH") : (int?)null,
            FacadeFace = front,
            WallBlock = b.Body,
            FloorBlock = b.Body,
            RoofBlock = b.Cap,
            AccentBlock = b.Accent,
            PilasterStep = 0,
            // 像台は屋根の外周を1マス立ち上げた縁。頂部が陸屋根のときだけ有効。
            ParapetHeight = cap == "flat" ? 1 : 0,
            ParapetBlock = b.Accent,
            NoEntrance = true,
            Openings = new List<Opening>(),
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        int pedH = _ui.GetInt("coPed");
        int outw = _ui.GetInt("coPedOut");
        var spec = Pedestal(shaft, diam, diam, h, pedH, outw, b.Base, b.Cap);

        string capName = cap switch
        {
            "dome" => $"ドーム{_ui.GetInt("coDomeH")}",
            "cone" => $"円錐(尖り{_ui.GetInt("coPitch")})",
            _ => "像台"
        };
        string ped = pedH > 0 ? $" / 台座{pedH}(張り出し{outw})" : "";
        return (spec, $"記念柱 円形径{diam}×高{h} / {capName}{ped}");
    }

    private (StructureSpec Spec, string Summary) BuildStele(Blocks b, string front)
    {
        int w = _ui.GetInt("stW");
        int d = _ui.GetInt("stD");
        int h = _ui.GetInt("stH");
        int coping = _ui.GetInt("stCop");
        int plinth = _ui.GetInt("stPlinth");
        bool rib = _ui.GetBool("stRib");

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = "flat",
            FacadeFace = front,
            WallBlock = b.Body,
            FloorBlock = b.Body,
            RoofBlock = b.Cap,
            AccentBlock = b.Accent,
            PilasterStep = rib ? Math.Max(4, _ui.GetInt("stRibStep")) : 0,
            HasBase = true,
            BaseBlock = b.Base,
            ParapetHeight = coping,
            ParapetBlock = b.Cap,
            VerandaWidth = plinth,
            VerandaBlock = b.Plinth,
            NoEntrance = true,
            Openings = new List<Opening>(),
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        string cop = coping > 0 ? $" / 笠石{coping}" : "";
        string pl = plinth > 0 ? $" / 基壇{plinth}" : "";
        return (spec, $"石碑 {w}×{d}×{h}{cop}{pl} / 正面{front}");
    }

    private (StructureSpec Spec, string Summary) BuildMausoleum(Blocks b, string front)
    {
        bool round = _ui.GetChoice("maPlan", "square") == "circle";
        int w = _ui.GetInt("maW");
        int d = round ? w : _ui.GetInt("maD");
        int h = _ui.GetInt("maH");
        string cap = _ui.GetChoice("maCap", "dome");

        bool frontAlongX = front == "north" || front == "south";
        int frontSpan = frontAlongX ? w : d;
        int sideSpan = frontAlongX ? d : w;

        // 円形の壁は1マス厚の曲線なので、面の1列に当たるセルが少ない。
        // 広いアーチを頼んでも数マスしか抜けないため、円形では幅を5までに抑える。
        int aw = Math.Clamp(_ui.GetInt("maArchW"), 3, round ? 5 : Math.Max(3, frontSpan - 4));
        int ah = Math.Clamp(_ui.GetInt("maArchH"), 3, Math.Max(3, h - 3));

        var ops = new List<Opening>
        {
            new() { Face = front, Kind = "arch", Offset = frontSpan / 2, Width = aw, Height = ah }
        };

        // 四面アーチは方形平面のときだけ。円形では反対面・側面の抜きが列に揃わない。
        bool four = !round && _ui.GetChoice("maFaces", "four") == "four";
        if (four)
        {
            int sw = Math.Clamp(aw, 3, Math.Max(3, sideSpan - 4));
            ops.Add(new Opening
            {
                Face = Opposite(front),
                Kind = "arch",
                Offset = frontSpan / 2,
                Width = aw,
                Height = ah
            });
            foreach (string f in SideFaces(front))
                ops.Add(new Opening
                {
                    Face = f,
                    Kind = "arch",
                    Offset = sideSpan / 2,
                    Width = sw,
                    Height = ah
                });
        }

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            FootprintShape = round ? "circle" : "rect",
            StructureType = "building",
            BuildingStyle = "walled",
            RoofType = cap,
            DomeHeight = cap == "dome" ? _ui.GetInt("maDomeH") : (int?)null,
            FacadeFace = front,
            WallBlock = b.Body,
            FloorBlock = b.Body,
            RoofBlock = b.Cap,
            AccentBlock = b.Accent,
            PilasterStep = 0,
            HasBase = _ui.GetBool("maBase"),
            BaseBlock = b.Base,
            VerandaWidth = _ui.GetInt("maPlinth"),
            VerandaBlock = b.Plinth,
            NoEntrance = true,
            Openings = ops,
            ChimneyCount = 0,
            EaveOverhang = 0
        };

        string planName = round ? $"円形径{w}" : $"方形{w}×{d}";
        string capName = cap switch
        {
            "dome" => $"ドーム{_ui.GetInt("maDomeH")}",
            "pyramid" => "四角錐",
            _ => "陸屋根"
        };
        string faces = four ? "四面アーチ" : "正面アーチ";
        return (spec, $"霊廟 {planName}×高{h} / {capName} / {faces}(幅{aw}×高{ah})");
    }

    private (StructureSpec Spec, string Summary) BuildZiggurat(Blocks b, string front)
    {
        int bw = _ui.GetInt("ziW");
        int bd = _ui.GetInt("ziD");
        int tiers = _ui.GetInt("ziTiers");
        int tierH = _ui.GetInt("ziTierH");
        int inset = _ui.GetInt("ziInset");

        var vols = new List<VolumePart>();
        for (int i = 0; i < tiers; i++)
        {
            int tw = bw - inset * 2 * i;
            int td = bd - inset * 2 * i;
            if (tw < 5 || td < 5) break; // これ以上縮むと段にならない

            vols.Add(new VolumePart
            {
                OffsetX = inset * i,
                OffsetY = tierH * i,
                OffsetZ = inset * i,
                Part = new StructureSpec
                {
                    Width = tw,
                    Depth = td,
                    Height = tierH + 1, // 天面がテラスになる
                    StructureType = "building",
                    BuildingStyle = "walled",
                    RoofType = "flat",
                    WallBlock = b.Body,
                    FloorBlock = b.Body,
                    RoofBlock = b.Cap,
                    AccentBlock = b.Accent,
                    HasBase = true,
                    BaseBlock = b.Base,
                    NoEntrance = true,
                    Openings = new List<Opening>()
                }
            });
        }

        int builtTiers = vols.Count;
        int topY = tierH * builtTiers;
        int shrineH = 0;

        if (_ui.GetBool("ziShrine") && builtTiers > 0)
        {
            int topW = bw - inset * 2 * (builtTiers - 1);
            int topD = bd - inset * 2 * (builtTiers - 1);
            int sw = topW - inset * 2;
            int sd = topD - inset * 2;
            if (sw >= 5 && sd >= 5)
            {
                shrineH = _ui.GetInt("ziShrineH");
                string shrineRoof = _ui.GetChoice("ziShrineRoof", "pyramid");
                bool frontAlongX = front == "north" || front == "south";
                int span = frontAlongX ? sw : sd;
                int archW = Math.Clamp(3, 3, Math.Max(3, span - 4));

                vols.Add(new VolumePart
                {
                    OffsetX = (bw - sw) / 2,
                    OffsetY = topY,
                    OffsetZ = (bd - sd) / 2,
                    Part = new StructureSpec
                    {
                        Width = sw,
                        Depth = sd,
                        Height = shrineH,
                        StructureType = "building",
                        BuildingStyle = "walled",
                        RoofType = shrineRoof,
                        FacadeFace = front,
                        WallBlock = b.Accent,
                        FloorBlock = b.Body,
                        RoofBlock = b.Cap,
                        AccentBlock = b.Accent,
                        HasBase = false,
                        BaseBlock = b.Base,
                        NoEntrance = true,
                        Openings = new List<Opening>
                        {
                            new()
                            {
                                Face = front,
                                Kind = "arch",
                                Offset = span / 2,
                                Width = archW,
                                Height = Math.Max(3, Math.Min(5, shrineH - 3))
                            }
                        }
                    }
                });
            }
        }

        var spec = new StructureSpec
        {
            Width = bw,
            Depth = bd,
            Height = topY + Math.Max(1, shrineH),
            Volumes = vols
        };

        string shrine = shrineH > 0 ? $" / 頂上の社 高{shrineH}" : "";
        return (spec,
            $"階段ピラミッド 基部{bw}×{bd} / {builtTiers}段(段高{tierH}・後退{inset}){shrine}");
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        var b = new Blocks
        {
            Body = _ui.GetBlock("body", "minecraft:smooth_quartz"),
            Accent = _ui.GetBlock("accent", "minecraft:chiseled_quartz_block"),
            Cap = _ui.GetBlock("cap", "minecraft:quartz_block"),
            Base = _ui.GetBlock("baseBlock", "minecraft:polished_diorite"),
            Plinth = _ui.GetBlock("plinthBlock", "minecraft:polished_diorite"),
        };

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(b.Body);

        string kind = _ui.GetChoice("kind", "obelisk");
        string front = _ui.GetChoice("front", "south");

        var result = kind switch
        {
            "arch" => BuildTriumphalArch(b, front),
            "column" => BuildMemorialColumn(b, front),
            "stele" => BuildStele(b, front),
            "mausoleum" => BuildMausoleum(b, front),
            "ziggurat" => BuildZiggurat(b, front),
            _ => BuildObelisk(b, front),
        };

        summary = result.Summary;
        return result.Spec;
    }
}
