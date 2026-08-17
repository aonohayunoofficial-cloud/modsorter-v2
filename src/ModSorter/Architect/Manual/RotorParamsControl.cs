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
            _ui.Heading("向き")
                .Choice("face", "ローターが向く方角", faces, "south");

            _ui.Heading("形式")
                .Choice("type", "形式", new[]
                {
                    ("近代・水平軸", "modern"), ("オランダ型", "dutch"),
                }, "modern")
                .Note("近代は鋼製タワー＋ナセル＋3枚翼。" +
                      "オランダ型は下太りの塔身＋回転キャップ＋4枚の格子羽根。");

            _ui.Heading("塔")
                .IntSlider("th", "塔の高さ", 6, 120, 78,
                    "2,000kW級でタワー高さ78m。オランダ型は20前後")
                .IntSlider("tb", "塔の基部の直径", 3, 24, 4,
                    "2,000kW級でタワー直径4.3m。オランダ型は10前後");

            _ui.Heading("ローター")
                .IntSlider("rd", "ローター直径", 6, 200, 86,
                    "2,000kW級でローター直径86m。オランダ型は26前後")
                .IntSlider("blades", "翼の枚数", 1, 8, 3, "近代は3枚、オランダ型は4枚")
                .IntSlider("thick", "翼の厚み", 1, 4, 1, "近代型のみ。格子羽根は1マス")
                .IntSlider("angle", "回転角", 0, 359, 0, "翼の位置。0で1枚目が水平")
                .Note("翼端が地面へ潜らないよう、ローター直径はハブ高さの2倍-4で抑える。");

            _ui.Heading("付帯設備")
                .Toggle("nacelle", "ナセルあり", "ナセルなし", true)
                .Toggle("balcony", "ギャラリーあり", "ギャラリーなし", true)
                .Toggle("manhole", "塔の出入口あり", "出入口なし", true)
                .Note("ナセルと航空障害灯は近代型、ギャラリー（外周デッキ）と採光窓は" +
                      "オランダ型にだけ出る。");

            _ui.Heading("使用ブロック")
                .BlockPick("shell", "タワー・塔身", "minecraft:white_concrete")
                .BlockPick("blade", "翼・羽根", "minecraft:white_concrete")
                .BlockPick("cap", "キャップ", "minecraft:dark_oak_planks")
                .BlockPick("accent", "ナセル・主軸", "minecraft:light_gray_concrete")
                .BlockPick("base", "基礎", "minecraft:smooth_stone")
                .BlockPick("deck", "デッキ", "minecraft:stone_bricks")
                .BlockPick("rail", "手すり", "minecraft:iron_bars")
                .BlockPick("glaze", "窓", "minecraft:glass")
                .BlockPick("light", "灯火", "minecraft:redstone_lamp");
        }

        Content = _ui.Root;
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

        string shell = _ui.GetBlock("shell", "minecraft:white_concrete");
        if (allowed.Count == 0) allowed.Add(shell);

        string type = _ui.GetChoice("type", "modern");
        int th = _ui.GetInt("th");
        int tb = _ui.GetInt("tb");
        int rd = _ui.GetInt("rd");
        int blades = _ui.GetInt("blades");
        int thick = _ui.GetInt("thick");
        string face = _ui.GetChoice("face", "south");
        bool nacelle = _ui.GetBool("nacelle");
        bool balcony = _ui.GetBool("balcony");

        var spec = new StructureSpec
        {
            StructureType = "industry:wind_turbine",
            FacadeFace = face,
            IndustryMillType = type,
            IndustryTowerHeight = th,
            IndustryTowerBase = tb,
            IndustryRotorDiameter = rd,
            IndustryRotorWidth = thick,
            IndustryBladeCount = blades,
            IndustryRotorAngle = _ui.GetInt("angle"),
            IndustryNacelle = nacelle,
            IndustryBalcony = balcony,
            IndustryManhole = _ui.GetBool("manhole"),
            WallBlock = shell,
            RoofBlock = _ui.GetBlock("cap", "minecraft:dark_oak_planks"),
            BaseBlock = _ui.GetBlock("base", "minecraft:smooth_stone"),
            FloorBlock = _ui.GetBlock("deck", "minecraft:stone_bricks"),
            ParapetBlock = _ui.GetBlock("rail", "minecraft:iron_bars"),
            AccentBlock = _ui.GetBlock("accent", "minecraft:light_gray_concrete"),
            GlazingBlock = _ui.GetBlock("glaze", "minecraft:glass"),
            SeatBlock = _ui.GetBlock("light", "minecraft:redstone_lamp"),
            TowerBlock = _ui.GetBlock("blade", "minecraft:white_concrete"),
            TowerRoofBlock = _ui.GetBlock("cap", "minecraft:dark_oak_planks")
        };

        int width, depth, height;

        if (type == "dutch")
        {
            int capH = Math.Max(2, (int)Math.Round(Math.Max(4.0, tb - 4.0) / 2.0));
            int hubY = th + 1 + Math.Max(1, capH / 2);
            int zFront = (int)Math.Round(Math.Max(4.0, tb - 4.0) / 2.0) + 2;
            width = Math.Max(rd + 2, tb + 4);
            depth = tb + zFront + 2;
            height = Math.Max(th + capH + 2, hubY + rd / 2 + 1);
            summary = $"風車 オランダ型 塔 高さ{th}×基部{tb} / 羽根{blades}枚・径{rd}（格子） / " +
                      $"{(balcony ? "ギャラリーあり" : "ギャラリーなし")} / " +
                      $"正面{FaceJp(face)} / 全高{height}";
        }
        else
        {
            // 展開側と同じ式。ナセル長・高さ・ハブ高さ・ローターの抑え方まで揃える。
            int nl = Math.Max(4, (int)Math.Round(th / 7.5));
            int nh = Math.Max(3, Math.Min(6, tb));
            int hubY = th + nh / 2;
            int rdEff = Math.Min(rd, 2 * (hubY - 2));
            int pad = Math.Clamp(tb * 4, 6, 32);
            width = Math.Max(rdEff + 2, pad + 2);
            depth = nl + thick + 2;
            height = Math.Max(hubY + nh, hubY + rdEff / 2 + 1);
            summary = $"風車 近代・水平軸 タワー 高さ{th}×基部{tb} / ローター径{rdEff}・{blades}枚 / " +
                      $"{(nacelle ? $"ナセル 長さ{nl}" : "ナセルなし")} / " +
                      $"正面{FaceJp(face)} / 全高{height}" +
                      (rdEff < rd ? $"（ローター径は{rd}→{rdEff}に抑えた）" : "");
        }

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
