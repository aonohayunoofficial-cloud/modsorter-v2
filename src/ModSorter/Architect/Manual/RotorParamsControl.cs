using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 回転体（風車・水車）。2種はパラメータが近いので kind 引数で1クラスに束ねる。
//
// 既定値の根拠（実寸）:
//   近代風車 … 2,000kW級でタワー高さ78m・タワー直径4.3m・ローター直径86m。
//              ナセルは長さ10.4m・幅3.5m・輸送時高さ4m（設置時5.4m）。
//   オランダ型 … 下太りの塔身＋回転キャップ＋4枚の格子羽根。
//   水車 … 上掛けの実施例で水輪 直径3m・ブレード幅2.5m、胸掛けで直径3.0m・内幅0.4m。
//          Laxey Wheel は直径22.1m・幅1.8m・バケット192個。
//   水車小屋 … 五間×二間半（約9m×4.5m）。水輪は小屋の外で、軸が壁を貫いて中へ入る。
public sealed class RotorParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;
    private readonly string _kind;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public RotorParamsControl(string kind)
    {
        _kind = (kind ?? "wind_turbine").Trim().ToLowerInvariant();
        _ui = new ParamPanelBuilder(this, Raise);

        var faces = new[] { ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west") };

        if (_kind == "water_wheel")
        {
            _ui.Heading("向き")
                .Choice("flow", "流れの向き", faces, "east")
                .Note("水路は流れの向きに通り、水輪の面はそれと直交する。");

            _ui.Heading("水輪")
                .IntSlider("wd", "水輪の直径", 3, 24, 4,
                    "上掛けの実施例で直径3m。世界最大の実働水車 Laxey Wheel は22.1m")
                .IntSlider("ww", "水輪の幅", 1, 6, 1,
                    "胸掛けで内幅0.4m、上掛けの実施例でブレード幅2.5m")
                .IntSlider("paddles", "羽根の枚数", 4, 32, 12, "Laxey Wheel はバケット192個")
                .IntSlider("angle", "回転角", 0, 359, 0, "羽根の位置。0で1枚目が下流側の水平")
                .Choice("wtype", "水の掛け方", new[]
                {
                    ("上掛け", "overshot"), ("胸掛け", "breast"), ("下掛け", "undershot"),
                }, "overshot")
                .Note("上掛けと胸掛けは羽根に水を溜めるバケットの底板が付く。" +
                      "下掛けは水輪の下端を水中に入れる。");

            _ui.Heading("付帯設備")
                .Toggle("flume", "導水路あり", "導水路なし", true)
                .Toggle("house", "水車小屋あり", "小屋なし", true)
                .Note("導水路は上掛け・胸掛けにだけ出る。4マスおきに支柱を川床まで落とす。" +
                      "小屋は9×5マスで、軸が壁を貫いて中へ入る。");

            _ui.Heading("使用ブロック")
                .BlockPick("blade", "水輪の骨組み", "minecraft:oak_planks")
                .BlockPick("deck", "羽根・バケット", "minecraft:spruce_planks")
                .BlockPick("accent", "車軸", "minecraft:stripped_oak_log")
                .BlockPick("base", "護岸・軸受け・導水路", "minecraft:cobblestone")
                .BlockPick("shell", "小屋の壁", "minecraft:oak_planks")
                .BlockPick("roofb", "小屋の屋根", "minecraft:dark_oak_planks")
                .BlockPick("glaze", "窓", "minecraft:glass_pane")
                .Note("水は minecraft:water を使うので選択項目にしていない。");
        }
        else
        {
            _ui.Heading("形式")
                .Choice("type", "形式", new[]
                {
                    ("近代・水平軸", "modern"),
                    ("オランダ型", "dutch"),
                    ("ダリウス型（φ型）", "darrieus"),
                    ("直線翼（H型・ジャイロミル）", "gyromill"),
                    ("ヘリカル（ねじり直線翼）", "helical"),
                    ("サボニウス型（S型ロータ）", "savonius"),
                }, "modern")
                .Note("上2つが水平軸、下4つが垂直軸。形式ごとに寸法・付帯設備・" +
                      "既定のブロックが入れ替わる。");

            // ===== 近代・水平軸 =====
            _ui.BeginChoiceGroup("type", "modern")
                .Heading("向き")
                .Choice("face_m", "ローターが向く方角", faces, "south")
                .Heading("塔")
                .IntSlider("th_m", "タワーの高さ", 6, 120, 78, "2,000kW級でタワー高さ78m")
                .IntSlider("tb_m", "タワー基部の直径", 3, 24, 4,
                    "2,000kW級でタワー直径4.3m。頂部は基部-2まで細る")
                .Heading("ローター")
                .IntSlider("rd_m", "ローター直径", 6, 200, 86, "2,000kW級でローター直径86m")
                .IntSlider("blades_m", "翼の枚数", 1, 8, 3, "3枚は重心が安定して振動が少ない")
                .IntSlider("thick_m", "翼の厚み", 1, 4, 1, "回転面の z 方向の厚み")
                .IntSlider("angle_m", "回転角", 0, 359, 0, "翼の位置。0で1枚目が水平")
                .Note("翼端が地面へ潜らないよう、ローター直径はハブ高さの2倍-4で抑える。")
                .Heading("付帯設備")
                .Toggle("nacelle", "ナセルあり", "ナセルなし", true)
                .Toggle("manhole_m", "塔の出入口あり", "出入口なし", true)
                .Note("ナセルは長さをタワー高さの1/7.5で決める（78mで10マス、" +
                      "V90-2.0MW の10.4mに相当）。頂部に航空障害灯が付く。")
                .Heading("使用ブロック")
                .BlockPick("shell_m", "タワー", "minecraft:white_concrete")
                .BlockPick("blade_m", "翼", "minecraft:white_concrete")
                .BlockPick("accent_m", "ナセル・主軸", "minecraft:light_gray_concrete")
                .BlockPick("base_m", "基礎", "minecraft:smooth_stone")
                .BlockPick("light_m", "灯火", "minecraft:redstone_lamp")
                .EndGroup();

            // ===== オランダ型 =====
            _ui.BeginChoiceGroup("type", "dutch")
                .Heading("向き")
                .Choice("face_d", "羽根が向く方角", faces, "south")
                .Heading("塔身")
                .IntSlider("th_d", "塔身の高さ", 6, 48, 20, "頂部は基部-4まで細る下太りの塔")
                .IntSlider("tb_d", "塔身の基部の直径", 5, 24, 10)
                .Heading("羽根")
                .IntSlider("rd_d", "羽根の回転直径", 6, 80, 26)
                .IntSlider("sails_d", "羽根の枚数", 2, 8, 4, "オランダ型は4枚が基本")
                .IntSlider("chord_d", "帆の幅", 2, 8, 4,
                    "縦框の片側に張る帆の幅。縁と3マスおきの帆桟は全ブロック、間は帆布")
                .IntSlider("angle_d", "回転角", 0, 359, 0, "羽根の位置。0で1枚目が水平")
                .Note("縦框（whip）と帆桟を全ブロックで通し、帆布だけ透けるブロックにする。")
                .Heading("付帯設備")
                .Toggle("balcony", "ギャラリーあり", "ギャラリーなし", true)
                .Toggle("manhole_d", "入口あり", "入口なし", true)
                .Heading("使用ブロック")
                .BlockPick("shell_d", "塔身", "minecraft:spruce_planks")
                .BlockPick("blade_d", "縦框・帆桟", "minecraft:stripped_oak_log")
                .BlockPick("lattice_d", "帆布", "minecraft:oak_fence")
                .BlockPick("cap_d", "キャップ", "minecraft:dark_oak_planks")
                .BlockPick("accent_d", "風車軸", "minecraft:stripped_dark_oak_log")
                .BlockPick("base_d", "基礎", "minecraft:cobblestone")
                .BlockPick("deck_d", "ギャラリーの床", "minecraft:oak_planks")
                .BlockPick("rail_d", "手すり", "minecraft:oak_fence")
                .BlockPick("glaze_d", "窓", "minecraft:glass_pane")
                .EndGroup();

            // ===== 垂直軸型 共通の向き =====
            _ui.BeginChoiceGroup("type", "darrieus", "gyromill", "helical", "savonius")
                .Heading("向き")
                .Choice("face_v", "翼の初期位置の基準", faces, "south")
                .Note("垂直軸型は風向に依存しないので、この向きは翼の並びを回すだけ。")
                .EndGroup();

            // ===== ダリウス型（φ型）=====
            // 寸法の桁が形式ごとに違うので、スライダーも形式ごとに分ける。
            _ui.BeginChoiceGroup("type", "darrieus")
                .Heading("ローター")
                .IntSlider("rd_da", "回転直径", 6, 64, 34,
                    "Sandia 34-m テストベッドは直径34m。Éole の64mが史上最大")
                .IntSlider("rh_da", "ローターの高さ", 6, 96, 42,
                    "高さ/直径は1.25が標準。Éole は直径64mに対し96m")
                .IntSlider("blades_da", "翼の枚数", 2, 3, 2,
                    "Sandia・FloWind・Éole いずれも2枚")
                .IntSlider("chord_da", "翼弦長", 1, 8, 2,
                    "Sandia 34-m は根元1.22m・赤道0.91m。根元は自動で1.4倍に広がる")
                .IntSlider("angle_da", "回転角", 0, 359, 0, "翼の位置")
                .Heading("主軸・基部")
                .IntSlider("post_da", "ローター下端の高さ", 0, 20, 3,
                    "φ型は翼の下端が主軸の根元に付くので低い")
                .IntSlider("shaft_da", "主軸（トルクチューブ）の直径", 1, 12, 2,
                    "回転直径の5%を下回る値は自動で引き上げる")
                .Toggle("guy_da", "ガイワイヤあり", "ガイワイヤなし", true)
                .Note("翼は直線＋円弧でトロポスキーン曲線を描く。主軸の頂部から3方向へ支線を張り、" +
                      "根元に増速機と発電機の機械室が付く。")
                .EndGroup();

            // ===== 直線翼（H型・ジャイロミル）=====
            _ui.BeginChoiceGroup("type", "gyromill")
                .Heading("ローター")
                .IntSlider("rd_gy", "回転直径", 3, 40, 26,
                    "Falkenberg の200kW機は回転直径26m・翼長24m")
                .IntSlider("rh_gy", "翼の長さ", 3, 40, 24, "高さ/直径はほぼ1")
                .IntSlider("blades_gy", "翼の枚数", 2, 4, 3, "H型は3枚が基本")
                .IntSlider("chord_gy", "翼弦長", 1, 8, 2,
                    "N×弦長/直径 が0.25を下回る値は自動で引き上げる")
                .IntSlider("angle_gy", "回転角", 0, 359, 0, "翼の位置")
                .Heading("塔・主軸")
                .IntSlider("post_gy", "ローター下端の高さ", 0, 60, 24, "塔の上に載る形式")
                .IntSlider("shaft_gy", "主軸の直径", 1, 12, 2)
                .Note("支持アームは上下端に入り、翼長が回転直径を超える機体では中間にも入る。")
                .EndGroup();

            // ===== ヘリカル（ねじり直線翼）=====
            _ui.BeginChoiceGroup("type", "helical")
                .Heading("ローター")
                .IntSlider("rd_he", "回転直径", 2, 12, 3, "qr5 は直径3.1m。建物・街路用の小型機")
                .IntSlider("rh_he", "翼の長さ", 3, 24, 5, "qr5 は高さ5m。掃過面積13.6m²")
                .IntSlider("blades_he", "翼の枚数", 2, 5, 3, "qr5 は3枚")
                .IntSlider("chord_he", "翼弦長", 1, 6, 1)
                .IntSlider("twist_he", "ねじり角", 60, 360, 120,
                    "1枚が下端から上端までに回る角度。qr5 は120度で3枚が全周を覆う")
                .IntSlider("angle_he", "回転角", 0, 359, 0, "翼の位置")
                .Heading("塔・主軸")
                .IntSlider("post_he", "ローター下端の高さ", 0, 30, 9, "マストに載せる形式")
                .IntSlider("shaft_he", "主軸の直径", 1, 8, 1)
                .EndGroup();

            // ===== サボニウス型（S型ロータ）=====
            _ui.BeginChoiceGroup("type", "savonius")
                .Heading("ローター")
                .IntSlider("rd_sa", "回転直径", 1, 16, 4, "小型機は0.5〜3m。大型でも数m")
                .IntSlider("rh_sa", "ローターの高さ", 2, 32, 8, "高さ/直径は2〜4が最良")
                .IntSlider("blades_sa", "バケットの枚数", 2, 3, 2, "2枚が基本")
                .IntSlider("stages_sa", "段数", 1, 4, 2,
                    "段ごとに位相をずらすとどの向きの風でも起動できる")
                .IntSlider("angle_sa", "回転角", 0, 359, 0, "バケットの位置")
                .Note("重なり比は0.2、端板は直径1.1倍で自動。弦長はバケットなので持たない。")
                .Heading("塔・主軸")
                .IntSlider("post_sa", "ローター下端の高さ", 0, 30, 2)
                .IntSlider("shaft_sa", "主軸の直径", 1, 8, 1)
                .EndGroup();

            // ===== 垂直軸型 共通の設置形態・ブロック =====
            _ui.BeginChoiceGroup("type", "darrieus", "gyromill", "helical", "savonius")
                .Heading("設置形態")
                .Toggle("float_v", "浮体式（洋上）", "陸上", false)
                .BeginGroup("float_v")
                .IntSlider("draft_v", "喫水", 2, 60, 18,
                    "SeaTwirl の30kW機は全高31m・水面上13mで、水面下は18m")
                .Note("海底の板と水を敷き、水面下に浮体を立てる。ガイワイヤは張らない。")
                .EndGroup()
                .Heading("使用ブロック")
                .BlockPick("shell_v", "主軸", "minecraft:light_gray_concrete")
                .BlockPick("blade_v", "翼", "minecraft:white_concrete")
                .BlockPick("accent_v", "アーム・軸受け・浮体", "minecraft:iron_block")
                .BlockPick("deck_v", "端板・機械室の屋根", "minecraft:gray_concrete")
                .BlockPick("base_v", "基礎・機械室・海底・錨", "minecraft:smooth_stone")
                .BlockPick("lattice_v", "ガイワイヤ", "minecraft:iron_bars")
                .BlockPick("light_v", "灯火", "minecraft:redstone_lamp")
                .Note("浮体式の水は minecraft:water を使うので選択項目にしていない。" +
                      "灯火は全高30マス以上のときだけ付く。")
                .EndGroup();

            Content = _ui.Root;
        }
    }

    // 流れの向き → 面が向く方角。canonical は面が南（+z）で流れが東（+x）。
    // Rotate は1手ごとに 面: 南→西→北→東 / 流れ: 東→南→西→北 と回る。
    private static string FaceForFlow(string flow) => flow switch
    {
        "south" => "west",
        "west" => "north",
        "north" => "east",
        _ => "south",
    };

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        allowed = _ui.BlockIds();

        if (_kind == "water_wheel")
        {
            string blade = _ui.GetBlock("blade", "minecraft:oak_planks");
            if (allowed.Count == 0) allowed.Add(blade);
            // 水は選択項目にしていないので、許可ブロックへ自前で足す。
            if (!allowed.Contains("minecraft:water")) allowed.Add("minecraft:water");

            int wd = _ui.GetInt("wd");
            int ww = _ui.GetInt("ww");
            string wtype = _ui.GetChoice("wtype", "overshot");
            bool house = _ui.GetBool("house");
            bool flume = _ui.GetBool("flume");
            string flow = _ui.GetChoice("flow", "east");

            var wspec = new StructureSpec
            {
                StructureType = "industry:water_wheel",
                FacadeFace = FaceForFlow(flow),
                IndustryRotorDiameter = wd,
                IndustryRotorWidth = ww,
                IndustryBladeCount = _ui.GetInt("paddles"),
                IndustryRotorAngle = _ui.GetInt("angle"),
                IndustryWheelType = wtype,
                IndustryMillHouse = house,
                IndustryFlume = flume,
                WallBlock = _ui.GetBlock("shell", "minecraft:oak_planks"),
                RoofBlock = _ui.GetBlock("roofb", "minecraft:dark_oak_planks"),
                BaseBlock = _ui.GetBlock("base", "minecraft:cobblestone"),
                FloorBlock = _ui.GetBlock("deck", "minecraft:spruce_planks"),
                AccentBlock = _ui.GetBlock("accent", "minecraft:stripped_oak_log"),
                GlazingBlock = _ui.GetBlock("glaze", "minecraft:glass_pane"),
                TowerBlock = blade
            };

            // 展開側と同じ式で外形を出す。
            int bottom = wtype == "undershot" ? 1 : 2;
            int wheelTop = bottom + wd;
            int wheelWidth = (int)Math.Ceiling(wd / 2.0) * 2 + 17;
            int wheelDepth = ww + 6 + (house ? 7 : 0);
            int wheelHeight = wheelTop + (flume && wtype != "undershot" ? 3 : 2);

            wspec.Width = wheelWidth;
            wspec.Depth = wheelDepth;
            wspec.Height = wheelHeight;

            string wtypeJp = wtype switch
            {
                "breast" => "胸掛け",
                "undershot" => "下掛け",
                _ => "上掛け",
            };
            summary = $"水車 {wtypeJp} 水輪 直径{wd}×幅{ww} / 羽根{wspec.IndustryBladeCount}枚 / " +
                      $"{(flume && wtype != "undershot" ? "導水路あり" : "導水路なし")} / " +
                      $"{(house ? "水車小屋あり（9×5）" : "小屋なし")} / " +
                      $"流れは{FaceJp(flow)}へ / 全高{wheelHeight}";
            return wspec;
        }

        string type = _ui.GetChoice("type", "modern");
        int width, depth, height;

        // ===== オランダ型 =====
        if (type == "dutch")
        {
            string shellD = _ui.GetBlock("shell_d", "minecraft:spruce_planks");
            string bladeD = _ui.GetBlock("blade_d", "minecraft:stripped_oak_log");
            string latticeD = _ui.GetBlock("lattice_d", "minecraft:oak_fence");
            string capD = _ui.GetBlock("cap_d", "minecraft:dark_oak_planks");
            allowed = new List<string>
            {
                shellD, bladeD, latticeD, capD,
                _ui.GetBlock("accent_d", "minecraft:stripped_dark_oak_log"),
                _ui.GetBlock("base_d", "minecraft:cobblestone"),
                _ui.GetBlock("deck_d", "minecraft:oak_planks"),
                _ui.GetBlock("rail_d", "minecraft:oak_fence"),
                _ui.GetBlock("glaze_d", "minecraft:glass_pane"),
            };

            int th = _ui.GetInt("th_d");
            int tb = _ui.GetInt("tb_d");
            int rd = _ui.GetInt("rd_d");
            int sails = _ui.GetInt("sails_d");
            int chord = _ui.GetInt("chord_d");
            string face = _ui.GetChoice("face_d", "south");
            bool balcony = _ui.GetBool("balcony");

            var dspec = new StructureSpec
            {
                StructureType = "industry:wind_turbine",
                FacadeFace = face,
                IndustryMillType = "dutch",
                IndustryTowerHeight = th,
                IndustryTowerBase = tb,
                IndustryRotorDiameter = rd,
                IndustryRotorWidth = chord,
                IndustryBladeCount = sails,
                IndustryRotorAngle = _ui.GetInt("angle_d"),
                IndustryBalcony = balcony,
                IndustryManhole = _ui.GetBool("manhole_d"),
                IndustryLatticeBlock = latticeD,
                WallBlock = shellD,
                RoofBlock = capD,
                TowerRoofBlock = capD,
                TowerBlock = bladeD,
                AccentBlock = _ui.GetBlock("accent_d", "minecraft:stripped_dark_oak_log"),
                BaseBlock = _ui.GetBlock("base_d", "minecraft:cobblestone"),
                FloorBlock = _ui.GetBlock("deck_d", "minecraft:oak_planks"),
                ParapetBlock = _ui.GetBlock("rail_d", "minecraft:oak_fence"),
                GlazingBlock = _ui.GetBlock("glaze_d", "minecraft:glass_pane")
            };

            int capH = Math.Max(2, (int)Math.Round(Math.Max(4.0, tb - 4.0) / 2.0));
            int hubY = th + 1 + Math.Max(1, capH / 2);
            int zFront = (int)Math.Round(Math.Max(4.0, tb - 4.0) / 2.0) + 2;
            width = Math.Max(rd + 2, tb + 4);
            depth = tb + zFront + 2;
            height = Math.Max(th + capH + 2, hubY + rd / 2 + 1);
            summary = $"風車 オランダ型 塔身 高さ{th}×基部{tb} / 羽根{sails}枚・径{rd}・帆幅{chord} / " +
                      $"{(balcony ? "ギャラリーあり" : "ギャラリーなし")} / " +
                      $"正面{FaceJp(face)} / 全高{height}";

            dspec.Width = width;
            dspec.Depth = depth;
            dspec.Height = height;
            return dspec;
        }

        // ===== 垂直軸型 =====
        if (type == "darrieus" || type == "gyromill" || type == "helical" || type == "savonius")
        {
            string shellV = _ui.GetBlock("shell_v", "minecraft:light_gray_concrete");
            string bladeV = _ui.GetBlock("blade_v", "minecraft:white_concrete");
            string latticeV = _ui.GetBlock("lattice_v", "minecraft:iron_bars");
            allowed = new List<string>
            {
                shellV, bladeV, latticeV,
                _ui.GetBlock("accent_v", "minecraft:iron_block"),
                _ui.GetBlock("deck_v", "minecraft:gray_concrete"),
                _ui.GetBlock("base_v", "minecraft:smooth_stone"),
                _ui.GetBlock("light_v", "minecraft:redstone_lamp"),
            };

            // スライダーは形式ごとに分けてあるので、選ばれた形式の接尾辞で読む。
            string sfx = type switch
            {
                "darrieus" => "da",
                "gyromill" => "gy",
                "helical" => "he",
                _ => "sa",
            };

            int rd = _ui.GetInt("rd_" + sfx);
            int rh = _ui.GetInt("rh_" + sfx);
            int blades = _ui.GetInt("blades_" + sfx);
            int post = _ui.GetInt("post_" + sfx);
            int ang = _ui.GetInt("angle_" + sfx);
            int wantShaft = _ui.GetInt("shaft_" + sfx);
            int wantChord = type == "savonius" ? 1 : _ui.GetInt("chord_" + sfx);
            int stages = type == "savonius" ? _ui.GetInt("stages_sa") : 1;
            int twist = type == "helical" ? _ui.GetInt("twist_he") : 0;
            bool guy = type == "darrieus" && _ui.GetBool("guy_da");
            bool floating = _ui.GetBool("float_v");
            int draft = floating ? _ui.GetInt("draft_v") : 0;
            string face = _ui.GetChoice("face_v", "south");

            // 弦長と主軸径の下限は展開側と同じ判定を使い回す。
            int chord = IndustryExpander.VawtChord(type, rd, blades, wantChord);
            int shaft = IndustryExpander.VawtShaft(rd, wantShaft);

            if (floating && !allowed.Contains("minecraft:water")) allowed.Add("minecraft:water");

            var vspec = new StructureSpec
            {
                StructureType = "industry:wind_turbine",
                FacadeFace = face,
                IndustryMillType = type,
                IndustryRotorDiameter = rd,
                IndustryRotorHeight = rh,
                IndustryBladeCount = blades,
                IndustryRotorWidth = chord,
                IndustryRotorAngle = ang,
                IndustryRotorTwist = twist,
                IndustryVawtStages = stages,
                IndustryTowerHeight = post,
                IndustryTowerBase = shaft,
                IndustryGuy = guy,
                IndustryFloating = floating,
                IndustryDraft = draft,
                IndustryLatticeBlock = latticeV,
                WallBlock = shellV,
                TowerBlock = bladeV,
                AccentBlock = _ui.GetBlock("accent_v", "minecraft:iron_block"),
                FloorBlock = _ui.GetBlock("deck_v", "minecraft:gray_concrete"),
                BaseBlock = _ui.GetBlock("base_v", "minecraft:smooth_stone"),
                SeatBlock = _ui.GetBlock("light_v", "minecraft:redstone_lamp")
            };

            // 展開側と同じ式。サボニウスの端板（直径1.1倍）・機械室・海面の広がり・
            // ガイワイヤの錨まで含めて外形を出す。
            double sea = Math.Max(shaft / 2.0 + 4.0, rd / 4.0);
            int houseHalf = Math.Clamp(rd / 8, 2, 6);
            int span = type == "savonius" ? (int)Math.Ceiling(rd * 1.1) + 2 : rd + 2;
            if (!floating && Math.Min(post, 4) >= 2) span = Math.Max(span, houseHalf * 2 + 3);
            if (guy) span = Math.Max(span, 2 * (rd / 2 + 4) + 2);
            if (floating) span = Math.Max(span, (int)Math.Ceiling(sea) * 2 + 2);
            width = span;
            depth = span;
            height = draft + post + rh + 2;

            string typeJp = type switch
            {
                "darrieus" => "ダリウス型（φ型）",
                "gyromill" => "直線翼（H型）",
                "helical" => $"ヘリカル ねじり{twist}度",
                _ => $"サボニウス型（S型ロータ）{stages}段",
            };
            summary = $"風車 {typeJp} 回転直径{rd}×ローター高さ{rh} / " +
                      $"翼{blades}枚{(type == "savonius" ? "" : $"・弦長{chord}")} / " +
                      $"主軸 直径{shaft}・下端高さ{post} / " +
                      $"{(floating ? $"浮体式 喫水{draft}" : "陸上")}" +
                      $"{(guy ? " / ガイワイヤあり" : "")} / 全高{height}・敷地{span}角" +
                      (chord > wantChord ? $"（弦長は{wantChord}→{chord}に上げた）" : "") +
                      (shaft > wantShaft ? $"（主軸は{wantShaft}→{shaft}に上げた）" : "");

            vspec.Width = width;
            vspec.Depth = depth;
            vspec.Height = height;
            return vspec;
        }

        // ===== 近代・水平軸 =====
        // ここはメソッド直下のスコープなので、内側の分岐（水車・オランダ型）で使う
        // blade・hubY と同名を置けない。他の項目と同じ M 接尾辞で分ける。
        string shell = _ui.GetBlock("shell_m", "minecraft:white_concrete");
        string bladeM = _ui.GetBlock("blade_m", "minecraft:white_concrete");
        allowed = new List<string>
        {
            shell, bladeM,
            _ui.GetBlock("accent_m", "minecraft:light_gray_concrete"),
            _ui.GetBlock("base_m", "minecraft:smooth_stone"),
            _ui.GetBlock("light_m", "minecraft:redstone_lamp"),
        };

        int thM = _ui.GetInt("th_m");
        int tbM = _ui.GetInt("tb_m");
        int rdM = _ui.GetInt("rd_m");
        int bladesM = _ui.GetInt("blades_m");
        int thickM = _ui.GetInt("thick_m");
        string faceM = _ui.GetChoice("face_m", "south");
        bool nacelle = _ui.GetBool("nacelle");

        var spec = new StructureSpec
        {
            StructureType = "industry:wind_turbine",
            FacadeFace = faceM,
            IndustryMillType = "modern",
            IndustryTowerHeight = thM,
            IndustryTowerBase = tbM,
            IndustryRotorDiameter = rdM,
            IndustryRotorWidth = thickM,
            IndustryBladeCount = bladesM,
            IndustryRotorAngle = _ui.GetInt("angle_m"),
            IndustryNacelle = nacelle,
            IndustryManhole = _ui.GetBool("manhole_m"),
            WallBlock = shell,
            TowerBlock = bladeM,
            AccentBlock = _ui.GetBlock("accent_m", "minecraft:light_gray_concrete"),
            BaseBlock = _ui.GetBlock("base_m", "minecraft:smooth_stone"),
            SeatBlock = _ui.GetBlock("light_m", "minecraft:redstone_lamp")
        };

        // 展開側と同じ式。ナセル長・高さ・ハブ高さ・ローターの抑え方まで揃える。
        int nl = Math.Max(4, (int)Math.Round(thM / 7.5));
        int nh = Math.Max(3, Math.Min(6, tbM));
        int hubYM = thM + nh / 2;
        int rdEff = Math.Min(rdM, 2 * (hubYM - 2));
        int pad = Math.Clamp(tbM * 4, 6, 32);
        width = Math.Max(rdEff + 2, pad + 2);
        depth = nl + thickM + 2;
        height = Math.Max(hubYM + nh, hubYM + rdEff / 2 + 1);
        summary = $"風車 近代・水平軸 タワー 高さ{thM}×基部{tbM} / ローター径{rdEff}・{bladesM}枚 / " +
                  $"{(nacelle ? $"ナセル 長さ{nl}" : "ナセルなし")} / " +
                  $"正面{FaceJp(faceM)} / 全高{height}" +
                  (rdEff < rdM ? $"（ローター径は{rdM}→{rdEff}に抑えた）" : "");

        spec.Width = width;
        spec.Depth = depth;
        spec.Height = height;
        return spec;
    }

    private static string FaceJp(string v) => v switch
    {
        "north" => "北",
        "east" => "東",
        "west" => "西",
        _ => "南",
    };
}
