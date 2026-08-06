using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 屋外イベント会場。屋根の架かった箱ではなく「囲いと段と平場」の組み合わせ。
// 再現度の軸は、観客席の段・フィールドの平場・ステージの立ち上がりの3つ。
//   円形闘技場   … 楕円の外壁の内側に、全周のスタンドを段状に積む。
//   競技場       … 対向する2面にスタンドを置き、間をフィールドにする。
//   野外音楽堂   … 片側にシェル（背の高い半囲い）、対面に扇形のスタンド。
//   ステージ     … 櫓状の舞台と背面の幕。スタンドは無し。
//   テント広場   … 切妻のテントを列に並べ、間を通路として空ける。
//   観覧席       … スタンド1面だけ。既存会場に足す用途。
//
// スタンドは volumes で「片側だけ後退させた薄い箱」を段数ぶん積んで作る。
// 階段ピラミッドと同じ経路なので新しい展開ロジックを増やさない。
// volumes は部品ごとに座標が 0 起点へ正規化されるため、負座標を作る縁側・軒は
// 部品側では使わない（記念建築と同じ制約）。
public sealed class EventVenueParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private sealed class Blocks
    {
        public string Structure = "";
        public string Seat = "";
        public string Field = "";
        public string Roof = "";
        public string Accent = "";
    }

    public EventVenueParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("種類")
           .Choice("kind", "会場の種類", new[]
           {
               ("円形闘技場", "arena"),
               ("競技場（対向スタンド）", "stadium"),
               ("野外音楽堂", "bandshell"),
               ("ステージ＋やぐら", "stage"),
               ("テント広場", "tents"),
               ("観覧席スタンド単体", "grandstand"),
           }, "arena");

        _ui.Heading("向き")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south");

        // ===== 円形闘技場 =====
        _ui.BeginChoiceGroup("kind", "arena")
           .Heading("円形闘技場")
           .Note("楕円の外壁の内側に全周のスタンドを積む。中央が競技面。")
           .IntSlider("arW", "長径", 21, 61, 41)
           .IntSlider("arD", "短径", 21, 61, 31)
           .IntSlider("arTiers", "段数", 2, 10, 5)
           .IntSlider("arStep", "1段の高さ", 1, 3, 1, "段差。1が座りやすい")
           .IntSlider("arRun", "1段の奥行", 1, 4, 2, "段の踏面の広さ")
           .IntSlider("arWall", "外壁の高さ", 0, 8, 3, "スタンド上端からさらに上へ。0で無し")
           .Toggle("arGate", "入場門あり", "入場門なし", true)
           .EndGroup();

        // ===== 競技場 =====
        _ui.BeginChoiceGroup("kind", "stadium")
           .Heading("競技場")
           .Note("対向する2面にスタンドを置き、間をフィールドにする。")
           .IntSlider("stW", "全体の幅", 21, 61, 41)
           .IntSlider("stD", "全体の奥行", 21, 61, 35)
           .IntSlider("stTiers", "段数", 2, 10, 6)
           .IntSlider("stStep", "1段の高さ", 1, 3, 1)
           .IntSlider("stRun", "1段の奥行", 1, 4, 2)
           .Toggle("stFour", "四面スタンド", "対向2面スタンド", false)
           .Toggle("stRoof", "スタンド屋根あり", "スタンド屋根なし", true)
           .BeginGroup("stRoof")
           .IntSlider("stRoofH", "屋根の高さ", 3, 10, 5, "最上段からの立ち上がり")
           .EndGroup()
           .EndGroup();

        // ===== 野外音楽堂 =====
        _ui.BeginChoiceGroup("kind", "bandshell")
           .Heading("野外音楽堂")
           .Note("片側にシェル（背の高い半囲い）、対面に扇形のスタンド。")
           .IntSlider("bsW", "全体の幅", 21, 51, 31)
           .IntSlider("bsD", "全体の奥行", 17, 51, 27)
           .IntSlider("bsShellW", "シェルの幅", 9, 31, 17)
           .IntSlider("bsShellH", "シェルの高さ", 6, 20, 11)
           .IntSlider("bsStageH", "ステージの高さ", 1, 4, 2)
           .IntSlider("bsTiers", "客席の段数", 0, 10, 5, "0で平場のみ")
           .IntSlider("bsStep", "1段の高さ", 1, 3, 1)
           .IntSlider("bsRun", "1段の奥行", 1, 4, 2)
           .EndGroup();

        // ===== ステージ＋やぐら =====
        _ui.BeginChoiceGroup("kind", "stage")
           .Heading("ステージ")
           .Note("櫓状の舞台と背面の幕。観客席は作らない。")
           .IntSlider("sgW", "舞台の幅", 7, 31, 15)
           .IntSlider("sgD", "舞台の奥行", 5, 21, 9)
           .IntSlider("sgH", "舞台の高さ", 1, 8, 3, "床面までの立ち上がり")
           .Toggle("sgBack", "背面の幕あり", "背面の幕なし", true)
           .BeginGroup("sgBack")
           .IntSlider("sgBackH", "幕の高さ", 4, 16, 8)
           .EndGroup()
           .Toggle("sgRoof", "屋根あり", "屋根なし", true)
           .BeginGroup("sgRoof")
           .IntSlider("sgRoofH", "屋根までの高さ", 4, 14, 7)
           .Choice("sgRoofType", "屋根の形",
               new[] { ("切妻", "gable"), ("陸屋根", "flat"), ("四角錐", "pyramid") }, "gable")
           .EndGroup()
           .EndGroup();

        // ===== テント広場 =====
        _ui.BeginChoiceGroup("kind", "tents")
           .Heading("テント広場")
           .Note("切妻のテントを列に並べる。間は通路として空ける。")
           .IntSlider("tnCount", "テントの数", 2, 10, 4)
           .IntSlider("tnW", "1張の幅", 5, 15, 7)
           .IntSlider("tnD", "1張の奥行", 5, 21, 9)
           .IntSlider("tnH", "軒の高さ", 3, 8, 4)
           .IntSlider("tnGap", "間隔", 1, 8, 3, "テント同士の通路幅")
           .Toggle("tnTwoRow", "2列に並べる", "1列に並べる", false)
           .BeginGroup("tnTwoRow")
           .IntSlider("tnAisle", "列間の通路幅", 3, 12, 6)
           .EndGroup()
           .Toggle("tnOpen", "側面を開ける", "側面を閉じる", true)
           .EndGroup();

        // ===== 観覧席スタンド単体 =====
        _ui.BeginChoiceGroup("kind", "grandstand")
           .Heading("観覧席スタンド")
           .Note("1面だけのスタンド。既存の会場に足す用途。")
           .IntSlider("gsW", "幅", 11, 61, 31)
           .IntSlider("gsTiers", "段数", 2, 14, 8)
           .IntSlider("gsStep", "1段の高さ", 1, 3, 1)
           .IntSlider("gsRun", "1段の奥行", 1, 4, 2)
           .Toggle("gsRoof", "屋根あり", "屋根なし", true)
           .BeginGroup("gsRoof")
           .IntSlider("gsRoofH", "屋根の高さ", 3, 10, 5)
           .EndGroup()
           .Toggle("gsBack", "背面の壁あり", "背面の壁なし", true)
           .EndGroup();

        _ui.Heading("使用ブロック")
           .BlockPick("structure", "躯体", "minecraft:smooth_stone")
           .BlockPick("seat", "座席", "minecraft:polished_andesite")
           .BlockPick("field", "地面・床", "minecraft:sand")
           .BlockPick("roof", "屋根", "minecraft:white_concrete")
           .BlockPick("accent", "装飾", "minecraft:stone_bricks");

        Content = _ui.Root;
    }

    private static bool AlongX(string face) => face == "north" || face == "south";

    // 平らな板（床・地面・屋根）を1枚作る。高さ1の陸屋根箱として展開する。
    // 中実にせず天面だけが欲しいので、高さ1にして床＝天面にする。
    private static StructureSpec Slab(int w, int d, string block) => new()
    {
        Width = Math.Max(1, w),
        Depth = Math.Max(1, d),
        Height = 1,
        StructureType = "building",
        BuildingStyle = "walled",
        RoofType = "flat",
        WallBlock = block,
        FloorBlock = block,
        RoofBlock = block,
        NoEntrance = true,
        Openings = new List<Opening>()
    };

    // 中実の箱（ステージの台・櫓の脚など）。壁と床だけで中は空だが、
    // 高さが低ければ実質詰まって見える。天面は roof で塞ぐ。
    private static StructureSpec Box(int w, int d, int h, string wall, string top) => new()
    {
        Width = Math.Max(1, w),
        Depth = Math.Max(1, d),
        Height = Math.Max(1, h),
        StructureType = "building",
        BuildingStyle = "walled",
        RoofType = "flat",
        WallBlock = wall,
        FloorBlock = wall,
        RoofBlock = top,
        NoEntrance = true,
        Openings = new List<Opening>()
    };

    // スタンド1面ぶんの段を volumes へ足す。
    // dir は段が高くなっていく向き。"north" なら z が小さいほど高い（＝南を向く客席）。
    // ox,oz は最下段の手前角の位置。span は客席の横幅。
    // 各段は「高さ step の薄い箱」で、奥へ行くほど run マス後退しながら step マス上がる。
    private static int AddStand(
        List<VolumePart> vols, string dir,
        int ox, int oz, int span, int tiers, int step, int run,
        string wall, string seat)
    {
        int topY = 0;
        for (int i = 0; i < tiers; i++)
        {
            int y = i * step;
            int back = i * run;   // 手前からの後退量
            topY = y + step;

            int w, d, x, z;
            switch (dir)
            {
                case "north": // z が小さいほど高い
                    w = span; d = run;
                    x = ox; z = oz - back - run;
                    break;
                case "south": // z が大きいほど高い
                    w = span; d = run;
                    x = ox; z = oz + back;
                    break;
                case "west":  // x が小さいほど高い
                    w = run; d = span;
                    x = ox - back - run; z = oz;
                    break;
                default:      // east: x が大きいほど高い
                    w = run; d = span;
                    x = ox + back; z = oz;
                    break;
            }

            vols.Add(new VolumePart
            {
                OffsetX = x,
                OffsetY = 0,
                OffsetZ = z,
                Part = Box(w, d, y + step, wall, seat)
            });
        }
        return topY;
    }

    private (StructureSpec Spec, string Summary) BuildArena(Blocks b, string front)
    {
        int w = _ui.GetInt("arW");
        int d = _ui.GetInt("arD");
        int tiers = _ui.GetInt("arTiers");
        int step = _ui.GetInt("arStep");
        int run = _ui.GetInt("arRun");
        int wallH = _ui.GetInt("arWall");

        var vols = new List<VolumePart>();

        // 競技面。楕円の内側いっぱいに砂を敷く。
        vols.Add(new VolumePart
        {
            OffsetX = 0,
            OffsetY = 0,
            OffsetZ = 0,
            Part = new StructureSpec
            {
                Width = w,
                Depth = d,
                Height = 1,
                FootprintShape = "circle",
                StructureType = "building",
                BuildingStyle = "walled",
                RoofType = "flat",
                WallBlock = b.Field,
                FloorBlock = b.Field,
                RoofBlock = b.Field,
                NoEntrance = true,
                Openings = new List<Opening>()
            }
        });

        // スタンド。楕円リングを段数ぶん内側へ縮めながら積む。
        // 各段は「その段までの高さを持つ楕円の輪」で、内側が次の段に上書きされて
        // 結果として階段状の客席になる。
        int topY = 0;
        for (int i = 0; i < tiers; i++)
        {
            int inset = i * run;
            int tw = w - inset * 2;
            int td = d - inset * 2;
            if (tw < 7 || td < 7) break;

            topY = (i + 1) * step;
            vols.Add(new VolumePart
            {
                OffsetX = inset,
                OffsetY = 0,
                OffsetZ = inset,
                Part = new StructureSpec
                {
                    Width = tw,
                    Depth = td,
                    Height = topY,
                    FootprintShape = "circle",
                    StructureType = "building",
                    BuildingStyle = "walled",
                    RoofType = "flat",
                    WallBlock = b.Structure,
                    FloorBlock = b.Structure,
                    RoofBlock = b.Seat,
                    NoEntrance = true,
                    Openings = new List<Opening>()
                }
            });
        }

        // 外周壁。最下段の輪をさらに上へ伸ばす。
        if (wallH > 0)
        {
            var ops = new List<Opening>();
            if (_ui.GetBool("arGate"))
            {
                int span = AlongX(front) ? w : d;
                ops.Add(new Opening
                {
                    Face = front,
                    Kind = "arch",
                    Offset = span / 2,
                    Width = 5,
                    Height = Math.Min(7, wallH + 3)
                });
            }

            vols.Add(new VolumePart
            {
                OffsetX = 0,
                OffsetY = 0,
                OffsetZ = 0,
                Part = new StructureSpec
                {
                    Width = w,
                    Depth = d,
                    Height = topY + wallH,
                    FootprintShape = "circle",
                    StructureType = "building",
                    BuildingStyle = "walled",
                    RoofType = "flat",
                    WallBlock = b.Accent,
                    FloorBlock = b.Field,
                    RoofBlock = b.Accent,
                    FacadeFace = front,
                    NoEntrance = true,
                    Openings = ops
                }
            });
        }

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = topY + wallH + 1,
            Volumes = vols
        };
        return (spec, $"円形闘技場 {w}×{d} / {tiers}段(段高{step}・踏面{run}) / 外壁{wallH}");
    }

    private (StructureSpec Spec, string Summary) BuildStadium(Blocks b, string front)
    {
        int w = _ui.GetInt("stW");
        int d = _ui.GetInt("stD");
        int tiers = _ui.GetInt("stTiers");
        int step = _ui.GetInt("stStep");
        int run = _ui.GetInt("stRun");
        bool four = _ui.GetBool("stFour");
        bool roof = _ui.GetBool("stRoof");
        int roofH = _ui.GetInt("stRoofH");

        var vols = new List<VolumePart>();
        int depthUsed = tiers * run;

        // フィールド。全面に敷いてからスタンドを上書きする。
        vols.Add(new VolumePart
        {
            OffsetX = 0,
            OffsetY = 0,
            OffsetZ = 0,
            Part = Slab(w, d, b.Field)
        });

        // 南北のスタンド（z の両端）。段は内側（フィールド側）が低い。
        int topY = 0;
        topY = Math.Max(topY, AddStand(vols, "north", 0, depthUsed,
            w, tiers, step, run, b.Structure, b.Seat));
        topY = Math.Max(topY, AddStand(vols, "south", 0, d - depthUsed,
            w, tiers, step, run, b.Structure, b.Seat));

        // 四面指定なら東西にも足す。角は後勝ちで重なるが破綻はしない。
        if (four)
        {
            int sideSpan = Math.Max(1, d - depthUsed * 2);
            topY = Math.Max(topY, AddStand(vols, "west", depthUsed, depthUsed,
                sideSpan, tiers, step, run, b.Structure, b.Seat));
            topY = Math.Max(topY, AddStand(vols, "east", w - depthUsed, depthUsed,
                sideSpan, tiers, step, run, b.Structure, b.Seat));
        }

        // スタンド屋根。最上段の上に板を張り出す。
        if (roof)
        {
            int y = topY + roofH;
            vols.Add(new VolumePart
            {
                OffsetX = 0,
                OffsetY = y,
                OffsetZ = 0,
                Part = Slab(w, depthUsed, b.Roof)
            });
            vols.Add(new VolumePart
            {
                OffsetX = 0,
                OffsetY = y,
                OffsetZ = d - depthUsed,
                Part = Slab(w, depthUsed, b.Roof)
            });
            if (four)
            {
                int sideSpan = Math.Max(1, d - depthUsed * 2);
                vols.Add(new VolumePart
                {
                    OffsetX = 0,
                    OffsetY = y,
                    OffsetZ = depthUsed,
                    Part = Slab(depthUsed, sideSpan, b.Roof)
                });
                vols.Add(new VolumePart
                {
                    OffsetX = w - depthUsed,
                    OffsetY = y,
                    OffsetZ = depthUsed,
                    Part = Slab(depthUsed, sideSpan, b.Roof)
                });
            }
            topY = y;
        }

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = topY + 1,
            Volumes = vols
        };
        string faces = four ? "四面" : "対向2面";
        string rf = roof ? $" / 屋根{roofH}" : "";
        return (spec, $"競技場 {w}×{d} / {faces}{tiers}段(段高{step}・踏面{run}){rf}");
    }

    private (StructureSpec Spec, string Summary) BuildBandshell(Blocks b, string front)
    {
        int w = _ui.GetInt("bsW");
        int d = _ui.GetInt("bsD");
        int shellW = Math.Min(_ui.GetInt("bsShellW"), w);
        int shellH = _ui.GetInt("bsShellH");
        int stageH = _ui.GetInt("bsStageH");
        int tiers = _ui.GetInt("bsTiers");
        int step = _ui.GetInt("bsStep");
        int run = _ui.GetInt("bsRun");

        var vols = new List<VolumePart>();
        int shellD = Math.Max(5, shellW / 2);

        // 広場の地面。
        vols.Add(new VolumePart
        {
            OffsetX = 0,
            OffsetY = 0,
            OffsetZ = 0,
            Part = Slab(w, d, b.Field)
        });

        // ステージの台。奥（z=0 側）の中央に置く。
        int sx = (w - shellW) / 2;
        vols.Add(new VolumePart
        {
            OffsetX = sx,
            OffsetY = 0,
            OffsetZ = 0,
            Part = Box(shellW, shellD, stageH, b.Structure, b.Structure)
        });

        // シェル。半円の殻。円形平面のドーム屋根を使い、手前半分を客席側へ開く。
        // 円形の輪＋ドームなので、正面から見ると貝殻状の背景になる。
        vols.Add(new VolumePart
        {
            OffsetX = sx,
            OffsetY = stageH,
            OffsetZ = 0,
            Part = new StructureSpec
            {
                Width = shellW,
                Depth = shellD * 2 - 1,
                Height = shellH,
                FootprintShape = "circle",
                StructureType = "building",
                BuildingStyle = "walled",
                RoofType = "dome",
                DomeHeight = Math.Max(3, shellW / 3),
                FacadeFace = "south",
                WallBlock = b.Accent,
                FloorBlock = b.Structure,
                RoofBlock = b.Roof,
                NoEntrance = true,
                Openings = new List<Opening>
                {
                    new()
                    {
                        Face = "south", Kind = "gate",
                        Offset = shellW / 2,
                        Width = Math.Max(5, shellW - 4),
                        Height = Math.Max(4, shellH - 2)
                    }
                }
            }
        });

        // 客席。手前（z 大側）から奥へ向かって上がる＝ステージを見下ろさない配置。
        int topY = stageH + shellH;
        if (tiers > 0)
        {
            int seatZ = d - tiers * run;
            AddStand(vols, "south", 0, seatZ, w, tiers, step, run, b.Structure, b.Seat);
        }

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = topY + 1,
            Volumes = vols
        };
        string seats = tiers > 0 ? $" / 客席{tiers}段" : " / 平場";
        return (spec, $"野外音楽堂 {w}×{d} / シェル幅{shellW}×高{shellH}{seats}");
    }

    private (StructureSpec Spec, string Summary) BuildStage(Blocks b, string front)
    {
        int w = _ui.GetInt("sgW");
        int d = _ui.GetInt("sgD");
        int h = _ui.GetInt("sgH");
        bool back = _ui.GetBool("sgBack");
        int backH = _ui.GetInt("sgBackH");
        bool roof = _ui.GetBool("sgRoof");
        int roofH = _ui.GetInt("sgRoofH");
        string roofType = _ui.GetChoice("sgRoofType", "gable");

        var vols = new List<VolumePart>();

        // 舞台の台。
        vols.Add(new VolumePart
        {
            OffsetX = 0,
            OffsetY = 0,
            OffsetZ = 0,
            Part = Box(w, d, h, b.Structure, b.Roof)
        });

        int topY = h;

        // 背面の幕。正面の反対側に薄い壁を立てる。
        if (back)
        {
            bool alongX = AlongX(front);
            int bw = alongX ? w : 1;
            int bd = alongX ? 1 : d;
            int bx = (front == "east") ? 0 : (alongX ? 0 : w - 1);
            int bz = (front == "south") ? 0 : (alongX ? d - 1 : 0);

            vols.Add(new VolumePart
            {
                OffsetX = bx,
                OffsetY = h,
                OffsetZ = bz,
                Part = Box(bw, bd, backH, b.Accent, b.Accent)
            });
            topY = Math.Max(topY, h + backH);
        }

        // 屋根。舞台の上に架ける。柱は立てず、幕と一体の構えにする。
        if (roof)
        {
            vols.Add(new VolumePart
            {
                OffsetX = 0,
                OffsetY = h,
                OffsetZ = 0,
                Part = new StructureSpec
                {
                    Width = w,
                    Depth = d,
                    Height = roofH,
                    StructureType = "building",
                    BuildingStyle = "colonnade",
                    RoofType = roofType,
                    RidgeAxis = AlongX(front) ? "x" : "z",
                    FacadeFace = front,
                    WallBlock = b.Structure,
                    FloorBlock = b.Roof,
                    RoofBlock = b.Roof,
                    AccentBlock = b.Accent,
                    PilasterStep = 4,
                    NoEntrance = true,
                    Openings = new List<Opening>()
                }
            });
            topY = Math.Max(topY, h + roofH + w / 2);
        }

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = topY + 1,
            Volumes = vols
        };
        string bk = back ? $" / 幕{backH}" : "";
        string rf = roof ? $" / 屋根{roofType}" : "";
        return (spec, $"ステージ {w}×{d} / 台高{h}{bk}{rf} / 正面{front}");
    }

    private (StructureSpec Spec, string Summary) BuildTents(Blocks b, string front)
    {
        int count = _ui.GetInt("tnCount");
        int tw = _ui.GetInt("tnW");
        int td = _ui.GetInt("tnD");
        int th = _ui.GetInt("tnH");
        int gap = _ui.GetInt("tnGap");
        bool twoRow = _ui.GetBool("tnTwoRow");
        int aisle = _ui.GetInt("tnAisle");
        bool open = _ui.GetBool("tnOpen");

        int perRow = twoRow ? (count + 1) / 2 : count;
        int rows = twoRow ? 2 : 1;

        int totalW = perRow * tw + (perRow - 1) * gap;
        int totalD = twoRow ? td * 2 + aisle : td;

        var vols = new List<VolumePart>();

        // 広場の地面。
        vols.Add(new VolumePart
        {
            OffsetX = 0,
            OffsetY = 0,
            OffsetZ = 0,
            Part = Slab(totalW, totalD, b.Field)
        });

        int placed = 0;
        for (int r = 0; r < rows; r++)
        {
            int z = (r == 0) ? 0 : td + aisle;
            for (int i = 0; i < perRow && placed < count; i++, placed++)
            {
                int x = i * (tw + gap);

                var tent = new StructureSpec
                {
                    Width = tw,
                    Depth = td,
                    Height = th,
                    StructureType = "building",
                    // 側面を開けるなら列柱＝壁なし。閉じるなら通常の壁。
                    BuildingStyle = open ? "colonnade" : "walled",
                    RoofType = "gable",
                    RidgeAxis = "z",
                    RoofPitch = 1,
                    FacadeFace = front,
                    WallBlock = b.Structure,
                    FloorBlock = b.Field,
                    RoofBlock = b.Roof,
                    AccentBlock = b.Accent,
                    PilasterStep = 2,
                    NoEntrance = true,
                    Openings = new List<Opening>()
                };

                vols.Add(new VolumePart
                {
                    OffsetX = x,
                    OffsetY = 0,
                    OffsetZ = z,
                    Part = tent
                });
            }
        }

        int ridge = th + (tw + 1) / 2;
        var spec = new StructureSpec
        {
            Width = totalW,
            Depth = totalD,
            Height = ridge + 1,
            Volumes = vols
        };
        string row = twoRow ? $"2列(通路{aisle})" : "1列";
        string side = open ? "側面開放" : "側面閉鎖";
        return (spec, $"テント広場 {count}張 {tw}×{td}×{th} / {row} / 間隔{gap} / {side}");
    }

    private (StructureSpec Spec, string Summary) BuildGrandstand(Blocks b, string front)
    {
        int w = _ui.GetInt("gsW");
        int tiers = _ui.GetInt("gsTiers");
        int step = _ui.GetInt("gsStep");
        int run = _ui.GetInt("gsRun");
        bool roof = _ui.GetBool("gsRoof");
        int roofH = _ui.GetInt("gsRoofH");
        bool back = _ui.GetBool("gsBack");

        int d = tiers * run;
        var vols = new List<VolumePart>();

        // 正面を向くように段の向きを決める。正面側が低い。
        string dir = front switch
        {
            "north" => "north",
            "east" => "east",
            "west" => "west",
            _ => "south"
        };

        bool alongX = AlongX(front);
        int totalW = alongX ? w : d;
        int totalD = alongX ? d : w;

        int ox = 0, oz = 0;
        switch (dir)
        {
            case "north": oz = d; break;             // z 小へ向かって上がる
            case "south": oz = 0; break;             // z 大へ向かって上がる
            case "west": ox = d; break;              // x 小へ向かって上がる
            default: ox = 0; break;                  // east
        }

        int topY = AddStand(vols, dir, ox, oz, w, tiers, step, run, b.Structure, b.Seat);

        // 背面の壁。段の一番高い側を塞ぐ。
        if (back)
        {
            int bw = alongX ? w : 1;
            int bd = alongX ? 1 : w;
            int bx = (dir == "west") ? 0 : (alongX ? 0 : totalW - 1);
            int bz = (dir == "north") ? 0 : (alongX ? totalD - 1 : 0);

            vols.Add(new VolumePart
            {
                OffsetX = bx,
                OffsetY = topY,
                OffsetZ = bz,
                Part = Box(bw, bd, Math.Max(2, roofH - 1), b.Accent, b.Accent)
            });
        }

        // 屋根。段の上へ張り出す板。
        if (roof)
        {
            vols.Add(new VolumePart
            {
                OffsetX = 0,
                OffsetY = topY + roofH,
                OffsetZ = 0,
                Part = Slab(totalW, totalD, b.Roof)
            });
            topY += roofH;
        }

        var spec = new StructureSpec
        {
            Width = totalW,
            Depth = totalD,
            Height = topY + 1,
            Volumes = vols
        };
        string rf = roof ? $" / 屋根{roofH}" : "";
        string bk = back ? " / 背面壁あり" : "";
        return (spec, $"観覧席 幅{w} / {tiers}段(段高{step}・踏面{run}){rf}{bk} / 正面{front}");
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        var b = new Blocks
        {
            Structure = _ui.GetBlock("structure", "minecraft:smooth_stone"),
            Seat = _ui.GetBlock("seat", "minecraft:polished_andesite"),
            Field = _ui.GetBlock("field", "minecraft:sand"),
            Roof = _ui.GetBlock("roof", "minecraft:white_concrete"),
            Accent = _ui.GetBlock("accent", "minecraft:stone_bricks"),
        };

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(b.Structure);

        string kind = _ui.GetChoice("kind", "arena");
        string front = _ui.GetChoice("front", "south");

        var result = kind switch
        {
            "stadium" => BuildStadium(b, front),
            "bandshell" => BuildBandshell(b, front),
            "stage" => BuildStage(b, front),
            "tents" => BuildTents(b, front),
            "grandstand" => BuildGrandstand(b, front),
            _ => BuildArena(b, front),
        };

        summary = result.Summary;
        return result.Spec;
    }
}
