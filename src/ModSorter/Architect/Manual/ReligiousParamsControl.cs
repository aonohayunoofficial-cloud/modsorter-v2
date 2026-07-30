using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 宗教建築。様式ごとに輪郭の作り方が正反対なので、パラメータも様式ごとに独立させる。
//   神殿   … 妻入り（正面に三角のペディメント）＋柱廊＋奥の部屋。浅い軒。窓なし。
//   教会   … 妻入り＋急勾配の切妻＋正面の鐘塔。縦長の窓を並べる。
//   寺社   … 平入り（正面に軒が向く）＋緩勾配＋深い軒＋回り縁。
//   モスク … ドーム屋根＋ミナレット。
// 棟の向きは「妻入り＝妻(三角)を正面へ」「平入り＝軒を正面へ」を正面の向きから
// ridge_axis へ変換する。ridge_axis="x" は棟がx軸に平行＝妻が東西面に出る。
//
// 柱廊＋奥の部屋(temple) のときは正面に開口を出さない。BuildColumn は半径 r の
// 塗りつぶし円を frontZ = d-1-r（正面が南のとき）に積むので、柱は外周面 z=d-1 に
// 1マス接する。そこへ開口を出すと ApplyOpening の壁探索が柱に当たり、柱の縁が縦に欠ける。
// 奥の部屋の入口は BuildTemple が柱廊側の壁中央に自前で開ける。
public sealed class ReligiousParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public ReligiousParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("様式")
           .Choice("style", "様式", new[]
           {
               ("神殿（妻入り＋柱廊）", "temple"),
               ("教会（急勾配＋鐘塔）", "church"),
               ("寺社（平入り＋深い軒）", "shrine"),
               ("モスク（ドーム＋ミナレット）", "mosque"),
           }, "temple");

        _ui.Heading("規模")
           .IntSlider("w", "幅", 7, 64, 21)
           .IntSlider("d", "奥行", 7, 64, 31)
           .IntSlider("h", "壁の高さ", 5, 48, 12, "屋根はこの上に載る。塔とドームはさらに上へ伸びる")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south");

        _ui.Heading("神殿の作り")
           .BeginChoiceGroup("style", "temple")
           .Note("正面に妻(ペディメント)を向け、柱廊の奥に部屋を置く。窓は付けない。")
           .Choice("planT", "構成", new[]
           {
               ("柱廊＋奥の部屋", "temple"),
               ("列柱のみ（壁なし）", "colonnade"),
               ("壁（方柱つき）", "walled"),
           }, "temple")
           .Choice("ridgeT", "棟の向き", new[]
           {
               ("妻入り（正面に妻）", "gable_front"),
               ("平入り（正面に軒）", "eaves_front"),
           }, "gable_front")
           .IntSlider("pitchT", "勾配", 1, 4, 2, "小さいほど急。2で標準的なペディメント")
           .IntSlider("eaveT", "軒の出", 0, 4, 1)
           .IntSlider("verandaT", "基壇の張り出し", 0, 3, 1, "外周へ床を敷き足す。0で無し")
           .EndGroup();

        _ui.Heading("教会の作り")
           .BeginChoiceGroup("style", "church")
           .Note("妻入り固定。正面の妻の上に鐘塔を立てる前提の勾配。")
           .IntSlider("pitchC", "勾配", 1, 4, 1, "1で45°の急勾配")
           .IntSlider("eaveC", "軒の出", 0, 4, 1)
           .EndGroup();

        _ui.Heading("寺社の作り")
           .BeginChoiceGroup("style", "shrine")
           .Note("平入り。正面に軒が向き、深い軒の下に回り縁が出る。")
           .Choice("planS", "構成", new[]
           {
               ("壁（方柱つき）", "walled"),
               ("列柱のみ（開放・拝殿風）", "colonnade"),
               ("柱廊＋奥の部屋", "temple"),
           }, "walled")
           .Choice("ridgeS", "棟の向き", new[]
           {
               ("平入り（正面に軒）", "eaves_front"),
               ("妻入り（正面に妻）", "gable_front"),
           }, "eaves_front")
           .IntSlider("pitchS", "勾配", 1, 4, 4, "4で最も緩い")
           .IntSlider("eaveS", "軒の出", 0, 4, 4)
           .IntSlider("verandaS", "縁の張り出し", 0, 3, 2, "軒の下に回る縁。0で無し")
           .EndGroup();

        _ui.Heading("モスクの作り")
           .BeginChoiceGroup("style", "mosque")
           .IntSlider("dome", "ドームの高さ", 3, 24, 8)
           .EndGroup();

        _ui.Heading("塔")
           .BeginChoiceGroup("style", "church", "mosque")
           .Toggle("tower", "塔あり", "塔なし", true)
           .BeginGroup("tower")
           .IntSlider("towerW", "塔の一辺", 3, 16, 5)
           .IntSlider("towerH", "塔の高さ", 2, 32, 12, "壁の上端から上へ。棟より低いと屋根に埋まる")
           .Choice("towerAlign", "位置", new[]
           {
               ("正面の中央", "front"),
               ("正面の両角", "front_corners"),
               ("四隅", "four_corners"),
               ("平面の中央", "center"),
               ("背面の中央", "rear"),
           }, "front")
           .Choice("towerRoof", "頂部", new[]
           {
               ("尖塔", "spire"), ("丸屋根", "dome"), ("陸屋根", "flat"),
           }, "spire")
           .Toggle("belfry", "鐘楼の開口あり", "鐘楼の開口なし", true)
           .EndGroup()
           .EndGroup();

        _ui.Heading("窓")
           .BeginChoiceGroup("style", "church", "shrine", "mosque")
           .Note("窓は壁のある面にしか付かない。構成が列柱のみ・柱廊＋部屋のときは付かない。")
           .Toggle("win", "窓あり", "窓なし", true)
           .BeginGroup("win")
           .IntSlider("winCount", "窓の数(各面)", 1, 16, 6)
           .IntSlider("winRows", "窓の段数", 1, 6, 3, "段数を増やすと縦長の窓になる")
           .IntSlider("winLevel", "窓の下端", 2, 12, 3, "床から何段目から始めるか")
           .EndGroup()
           .EndGroup();

        _ui.Heading("入口")
           .IntSlider("entrance", "入口の幅", 1, 9, 3, "2以上で大開口。壁のある構成のときだけ効く");

        _ui.Heading("外装")
           .Toggle("pilaster", "方柱あり", "方柱なし", true)
           .BeginGroup("pilaster")
           .IntSlider("pilasterStep", "方柱の間隔", 4, 12, 4)
           .EndGroup()
           .Toggle("baseCourse", "基壇あり", "基壇なし", true);

        _ui.Heading("使用ブロック")
           .Note("様式を変えてもブロックは共通。寺社は木材系、モスクは白系に替えると近づく。")
           .BlockPick("wall", "壁", "minecraft:smooth_sandstone")
           .BlockPick("accent", "柱", "minecraft:quartz_pillar")
           .BlockPick("floor", "床", "minecraft:smooth_sandstone")
           .BlockPick("roofBlock", "屋根", "minecraft:sandstone")
           .BlockPick("towerBlock", "塔の壁", "minecraft:smooth_sandstone")
           .BlockPick("towerRoofBlock", "塔の頂部", "minecraft:cut_sandstone")
           .BlockPick("baseBlock", "基壇・縁", "minecraft:chiseled_sandstone")
           .BlockPick("window", "窓ガラス", "minecraft:blue_stained_glass");

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
        string style = _ui.GetChoice("style", "temple");
        int w = Math.Clamp(_ui.GetInt("w"), 7, 64);
        int d = Math.Clamp(_ui.GetInt("d"), 7, 64);
        int h = Math.Clamp(_ui.GetInt("h"), 5, 48);
        string front = _ui.GetChoice("front", "south");

        string wall = _ui.GetBlock("wall", "minecraft:smooth_sandstone");
        string accent = _ui.GetBlock("accent", "minecraft:quartz_pillar");
        string floor = _ui.GetBlock("floor", "minecraft:smooth_sandstone");
        string roof = _ui.GetBlock("roofBlock", "minecraft:sandstone");
        string towerBlock = _ui.GetBlock("towerBlock", "minecraft:smooth_sandstone");
        string towerRoofBlock = _ui.GetBlock("towerRoofBlock", "minecraft:cut_sandstone");
        string baseBlock = _ui.GetBlock("baseBlock", "minecraft:chiseled_sandstone");
        string glass = _ui.GetBlock("window", "minecraft:blue_stained_glass");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        bool mosque = style == "mosque";

        // 様式ごとに独立したキーから読む。ここを共有すると神殿と寺社が同じ形になる。
        string plan = style switch
        {
            "temple" => _ui.GetChoice("planT", "temple"),
            "shrine" => _ui.GetChoice("planS", "walled"),
            _ => "walled"
        };

        int pitch = style switch
        {
            "temple" => Math.Clamp(_ui.GetInt("pitchT"), 1, 4),
            "church" => Math.Clamp(_ui.GetInt("pitchC"), 1, 4),
            "shrine" => Math.Clamp(_ui.GetInt("pitchS"), 1, 4),
            _ => 0
        };

        int eave = style switch
        {
            "temple" => Math.Clamp(_ui.GetInt("eaveT"), 0, 4),
            "church" => Math.Clamp(_ui.GetInt("eaveC"), 0, 4),
            "shrine" => Math.Clamp(_ui.GetInt("eaveS"), 0, 4),
            _ => 0
        };

        int veranda = style switch
        {
            "temple" => Math.Clamp(_ui.GetInt("verandaT"), 0, 3),
            "shrine" => Math.Clamp(_ui.GetInt("verandaS"), 0, 3),
            _ => 0
        };

        // 妻入り＝妻(三角)を正面へ。平入り＝軒を正面へ。
        // ridge_axis="x" は棟がx軸に平行＝妻が東西面(x=0/x=w-1)に出る。
        // よって南北が正面のとき、妻入りは "z"、平入りは "x"。東西が正面なら逆。
        bool gableFront = style switch
        {
            "temple" => _ui.GetChoice("ridgeT", "gable_front") == "gable_front",
            "shrine" => _ui.GetChoice("ridgeS", "eaves_front") == "gable_front",
            _ => true   // 教会は妻入り固定。モスクはドームなので棟の向きを使わない。
        };
        bool frontAlongX = (front == "south" || front == "north");
        string ridge = frontAlongX ? (gableFront ? "z" : "x") : (gableFront ? "x" : "z");

        string roofType = mosque ? "dome" : "gable";
        int dome = mosque ? Math.Clamp(_ui.GetInt("dome"), 3, 24) : 0;

        // 塔は教会・モスクだけ。角に2本以上並べる指定では平面に収まる大きさへ抑える。
        bool tower = (style == "church" || mosque) && _ui.GetBool("tower");
        string towerAlign = _ui.GetChoice("towerAlign", "front");
        int towerW = 0, towerH = 0;
        if (tower)
        {
            int limit = (towerAlign == "front_corners" || towerAlign == "four_corners")
                ? Math.Max(3, (Math.Min(w, d) - 1) / 2)
                : Math.Max(3, Math.Min(w, d) - 2);
            towerW = Math.Min(Math.Clamp(_ui.GetInt("towerW"), 3, 16), limit);
            towerH = Math.Clamp(_ui.GetInt("towerH"), 2, 32);
        }

        var ops = new List<Opening>();

        // 窓。壁で囲む構成のときだけ。神殿は様式として窓を持たない。
        if (plan == "walled" && style != "temple" && _ui.GetBool("win"))
        {
            int n = Math.Clamp(_ui.GetInt("winCount"), 1, 16);
            int rows = Math.Clamp(_ui.GetInt("winRows"), 1, 6);
            int start = Math.Clamp(_ui.GetInt("winLevel"), 2, Math.Max(2, h - 3));
            for (int r = 0; r < rows; r++)
            {
                int level = start + r;
                if (level > h - 2) break;
                AddEven(ops, "north", "window", n, w, level, glass);
                AddEven(ops, "south", "window", n, w, level, glass);
                AddEven(ops, "east", "window", n, d, level, glass);
                AddEven(ops, "west", "window", n, d, level, glass);
            }
        }

        int frontSpan = frontAlongX ? w : d;
        int center = frontSpan / 2;
        int entrance = Math.Clamp(_ui.GetInt("entrance"), 1, 9);

        if (plan == "walled")
        {
            // 大開口＋中央のドア。ドアを必ず入れて、展開側の入口保証が
            // 別の面を勝手に抜くのを防ぐ。
            if (entrance >= 2)
            {
                ops.Add(new Opening
                {
                    Face = front,
                    Kind = "gate",
                    Offset = center,
                    Level = 1,
                    Width = Math.Min(entrance, frontSpan - 2),
                    Height = Math.Min(4, h - 2)
                });
            }
            ops.Add(new Opening
            {
                Face = front,
                Kind = "door",
                Offset = center,
                Level = 1
            });
        }
        else if (plan == "temple")
        {
            // 正面は柱列なので触らない。柱の無い側面の壁へドアを1つ置いて入口保証を満たす。
            // 部屋の範囲は BuildTemple と同じ式で出す。gap は高さから決まる r で計算され、
            // 柱の r（幅・奥行でさらに絞られる）以上になるので、部屋の範囲は柱の帯と重ならない。
            // その部屋の範囲の中央を取れば、奥行が小さいときでも柱へ当たらない。
            int rByHeight = h < 10 ? 1 : (h < 18 ? 2 : 3);
            int gap = rByHeight * 2 + 1;

            string side;
            int sideOffset;
            if (frontAlongX)
            {
                side = "east";
                if (front == "south")
                {
                    int rzHi = Math.Max(1, d - 1 - gap - 1);   // 部屋は z=0..rzHi
                    sideOffset = Math.Clamp(rzHi / 2, 1, Math.Max(1, rzHi - 1));
                }
                else
                {
                    int rzLo = Math.Min(d - 2, gap + 1);       // 部屋は z=rzLo..d-1
                    sideOffset = Math.Clamp(
                        (rzLo + d - 1) / 2, rzLo + 1, Math.Max(rzLo + 1, d - 2));
                }
            }
            else
            {
                side = "north";
                if (front == "east")
                {
                    int rxHi = Math.Max(1, w - 1 - gap - 1);   // 部屋は x=0..rxHi
                    sideOffset = Math.Clamp(rxHi / 2, 1, Math.Max(1, rxHi - 1));
                }
                else
                {
                    int rxLo = Math.Min(w - 2, gap + 1);       // 部屋は x=rxLo..w-1
                    sideOffset = Math.Clamp(
                        (rxLo + w - 1) / 2, rxLo + 1, Math.Max(rxLo + 1, w - 2));
                }
            }

            ops.Add(new Opening
            {
                Face = side,
                Kind = "door",
                Offset = sideOffset,
                Level = 1
            });
        }
        // 列柱のみ(colonnade) は展開側が開口部と入口保証を通さないので何も入れない。

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roofType,
            RoofPitch = pitch,
            RidgeAxis = ridge,
            DomeHeight = dome,
            BuildingStyle = plan,
            FacadeFace = front,
            WallBlock = wall,
            FloorBlock = floor,
            RoofBlock = roof,
            AccentBlock = accent,
            PilasterStep = _ui.GetBool("pilaster") ? Math.Max(4, _ui.GetInt("pilasterStep")) : 0,
            HasBase = _ui.GetBool("baseCourse"),
            BaseBlock = baseBlock,
            VerandaWidth = veranda,
            VerandaBlock = baseBlock,
            Openings = ops,
            ChimneyCount = 0,
            EaveOverhang = eave,
            EaveNorth = eave > 0,
            EaveSouth = eave > 0,
            EaveEast = eave > 0,
            EaveWest = eave > 0,
            TowerWidth = towerW,
            TowerHeight = towerH,
            TowerAlign = towerAlign,
            TowerRoof = _ui.GetChoice("towerRoof", "spire"),
            TowerBlock = towerBlock,
            TowerRoofBlock = towerRoofBlock,
            TowerBelfry = tower && _ui.GetBool("belfry")
        };

        string styleName = style switch
        {
            "church" => "教会",
            "shrine" => "寺社",
            "mosque" => "モスク",
            _ => "神殿"
        };
        string roofNote = mosque
            ? $"ドーム{dome}"
            : $"{(gableFront ? "妻入り" : "平入り")}(勾配{pitch}/軒{eave})";
        string extra = veranda > 0 ? $" / 縁{veranda}" : "";
        string towerNote = tower ? $"塔{towerW}角×{towerH}({towerAlign})" : "塔なし";
        summary = $"{styleName} / {w}×{d}×{h} / {roofNote} / {plan}{extra} / {towerNote}";
        return spec;
    }
}
