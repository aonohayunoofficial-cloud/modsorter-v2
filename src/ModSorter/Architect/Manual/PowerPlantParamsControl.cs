using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 発電所。港湾・空港と同じく単体構造物ごとに小分類を持ち、kind 引数で1クラスに束ねる。
//
// 既定値の根拠（実寸）:
//   タービン建屋 … 実施例で長さ60m×幅36m×高さ32m。別例で83m×83m×高さ36m。
//   ボイラ建屋（排熱回収ボイラ）… 長さ45m×幅30m×高さ40m。基礎スラブは4m厚。
//   煙突 … 東京電力の15火力で平均170m・最高230m。100m級で口径5.7m。
//          地上高60m以上に航空障害灯が必要。
//   冷却塔 … 高さ190mの実機で底部半径65.25m・喉部42m・頂部43.45m、喉部は全高の0.75。
//          高さ131.1m・底部98.0m・頂部58.18mの例もある。斜め柱は直径1.2m・高さ9m。
//   格納容器 … ABWR は内径29m・内高29.5m。PWR はドーム内半径22.8m級。
//   変電ヤード … 門型で引留め、三相の母線を渡す。変圧器は防火壁で仕切る。
public sealed class PowerPlantParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;
    private readonly string _kind;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public PowerPlantParamsControl(string kind)
    {
        _kind = (kind ?? "turbine_hall").Trim().ToLowerInvariant();
        _ui = new ParamPanelBuilder(this, Raise);

        var faces = new[] { ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west") };

        if (_kind == "boiler_house" || _kind == "turbine_hall")
        {
            bool boiler = _kind == "boiler_house";

            _ui.Heading("向き")
                .Choice("face", "正面の向き", faces, "south")
                .Note("搬入口は正面の反対側の妻に開く。付属棟は正面から見て右手に付く。");

            _ui.Heading("寸法")
                .IntSlider("len", "長さ", 10, 120, boiler ? 45 : 60,
                    boiler ? "排熱回収ボイラの実施例で45m" : "実施例で60m。大型で83m")
                .IntSlider("wid", "幅", 10, 120, boiler ? 30 : 36,
                    boiler ? "実施例で30m" : "実施例で36m")
                .IntSlider("hgt", "高さ", 8, 80, boiler ? 40 : 32,
                    boiler ? "排熱回収ボイラは40m級" : "タービン建屋は32〜36m")
                .IntSlider("bay", "柱間隔", 3, 24, 9, "鉄骨柱と採光帯の割り付け")
                .IntSlider("lv", "床の段数", 1, 8, boiler ? 4 : 2,
                    boiler ? "ボイラは点検床を何層も持つ" : "運転床と中間床");

            _ui.Heading("付帯設備")
                .Toggle("crane", "天井クレーンあり", "クレーンなし", !boiler)
                .Toggle("louver", "採光帯あり", "採光帯なし", true)
                .Toggle("gate", "機器搬入口あり", "搬入口なし", true)
                .Toggle("annex", "付属棟あり", "付属棟なし", !boiler)
                .Toggle("light", "航空障害灯あり", "灯火なし", false)
                .Note(boiler
                    ? "上部から煙道が張り出す。煙突・タービン建屋と繋ぐ位置の目印になる。"
                    : "天井クレーンは機器の吊り出し用で、屋根直下に走行梁が通る。");
        }
        else if (_kind == "stack")
        {
            _ui.Heading("寸法")
                .IntSlider("hgt", "高さ", 10, 240, 120,
                    "東京電力の15火力で最低85m・最高230m・平均170m")
                .IntSlider("db", "底部の直径", 3, 40, 14, "高さ120m級で底部は十数m")
                .IntSlider("dt", "頂部の直径", 2, 40, 6, "100m級で口径5.7m")
                .IntSlider("flues", "内筒の本数", 0, 4, 2, "集合煙突は複数の内筒を外筒に納める");

            _ui.Heading("付帯設備")
                .Toggle("ladder", "点検はしごあり", "はしごなし", true)
                .Toggle("light", "航空障害灯あり", "灯火なし", true)
                .Note("はしごは外筒のテーパに沿って登り、20マスごとに踊り場が付く。" +
                      "灯火は地上高60m以上のときだけ付き、45マスごとに段を増やす。");
        }
        else if (_kind == "cooling_tower")
        {
            _ui.Heading("寸法")
                .IntSlider("hgt", "高さ", 20, 220, 100,
                    "実機は131〜202m。高さ190mの例で底部直径130m")
                .IntSlider("db", "底部の直径", 12, 200, 76, "高さの0.65〜0.75倍が目安")
                .IntSlider("throat", "喉部の直径", 6, 200, 49, "底部の0.6〜0.65倍。全高の3/4の位置")
                .IntSlider("top", "頂部の直径", 6, 200, 52, "喉部よりわずかに広がる")
                .IntSlider("inlet", "空気取入口の高さ", 2, 40, 9, "斜め柱の高さ。実機で9m")
                .IntSlider("cols", "斜め柱の組数", 4, 96, 24, "1組がV字の2本");

            _ui.Heading("付帯設備")
                .Toggle("basin", "水盤あり", "水盤なし", true)
                .Toggle("light", "航空障害灯あり", "灯火なし", true)
                .Note("水は minecraft:water を使うので選択項目にしていない。" +
                      "喉部が底部より太いと双曲線が引けないので、自動でほぼ円筒になる。");
        }
        else if (_kind == "containment")
        {
            _ui.Heading("向き")
                .Choice("face", "正面の向き", faces, "south")
                .Note("エアロックは正面の反対側に、補助建屋は正面から見て奥に付く。");

            _ui.Heading("形式")
                .Choice("shape", "形式", new[]
                {
                    ("PWR（円筒＋ドーム）", "cylinder"), ("BWR（角形建屋）", "box"),
                }, "cylinder")
                .Note("PWR は円筒に半球ドームを載せる。BWR は角形の厚壁で、" +
                      "上部に燃料取替床と天井クレーンが入る。");

            _ui.Heading("寸法")
                .IntSlider("dia", "内径（一辺）", 12, 90, 40,
                    "ABWR は内径29m。PWR はドーム内半径22.8m級")
                .IntSlider("hgt", "円筒（建屋）の高さ", 8, 80, 30, "ABWR は内高29.5m")
                .IntSlider("wall", "壁の厚み", 1, 5, 2, "遮蔽壁。厚いほど実物に近い");

            _ui.Heading("付帯設備")
                .Toggle("gate", "機器搬入口あり", "搬入口なし", true)
                .Toggle("crane", "天井クレーンあり", "クレーンなし", true)
                .Toggle("annex", "補助建屋あり", "補助建屋なし", true)
                .Toggle("light", "航空障害灯あり", "灯火なし", false)
                .Note("天井クレーンは BWR（角形）のときだけ入る。");
        }
        else
        {
            _ui.Heading("向き")
                .Choice("face", "正面の向き", faces, "south")
                .Note("門は正面側のフェンス中央に開く。制御建屋は正面から見て手前左。");

            _ui.Heading("寸法")
                .IntSlider("len", "長さ", 16, 200, 60, "回線数ぶんの門型が並ぶ長さ")
                .IntSlider("wid", "幅", 16, 200, 40, "引留めから母線までの奥行き")
                .IntSlider("gh", "門型の高さ", 6, 40, 16, "母線は門型の2マス下を走る")
                .IntSlider("bays", "回線数", 1, 16, 4, "門型の数")
                .IntSlider("trs", "変圧器の台数", 0, 8, 2, "1台ごとに防火壁で仕切る");

            _ui.Heading("付帯設備")
                .Toggle("fence", "外周フェンスあり", "フェンスなし", true)
                .Toggle("light", "門型の灯火あり", "灯火なし", true);
        }

        _ui.Heading("使用ブロック")
            .BlockPick("shell", "外壁・シェル・防火壁", "minecraft:light_gray_concrete")
            .BlockPick("roofb", "屋根", "minecraft:gray_concrete")
            .BlockPick("base", "基礎・舗装・堤", "minecraft:stone_bricks")
            .BlockPick("deck", "床・デッキ・踊り場", "minecraft:smooth_stone")
            .BlockPick("frame", "鉄骨・クレーン・門型", "minecraft:iron_block")
            .BlockPick("accent", "柱・内筒・機器", "minecraft:gray_concrete")
            .BlockPick("rail", "手すり・フェンス・放熱器", "minecraft:iron_bars")
            .BlockPick("lattice", "母線・格子・充填材", "minecraft:iron_chain")
            .BlockPick("glaze", "採光帯・窓・がいし", "minecraft:glass")
            .BlockPick("light", "灯火", "minecraft:redstone_lamp")
            .Note("はしごは minecraft:ladder、冷却塔の水は minecraft:water を使うので" +
                  "選択項目にしていない。");

        Content = _ui.Root;
    }

    private static string FaceJp(string v) => v switch
    {
        "north" => "北",
        "east" => "東",
        "west" => "西",
        _ => "南",
    };

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string shell = _ui.GetBlock("shell", "minecraft:light_gray_concrete");
        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(shell);
        if (_kind == "stack" && !allowed.Contains("minecraft:ladder"))
            allowed.Add("minecraft:ladder");
        if (_kind == "cooling_tower" && !allowed.Contains("minecraft:water"))
            allowed.Add("minecraft:water");

        var spec = new StructureSpec
        {
            StructureType = "industry:" + _kind,
            WallBlock = shell,
            RoofBlock = _ui.GetBlock("roofb", "minecraft:gray_concrete"),
            BaseBlock = _ui.GetBlock("base", "minecraft:stone_bricks"),
            FloorBlock = _ui.GetBlock("deck", "minecraft:smooth_stone"),
            TowerBlock = _ui.GetBlock("frame", "minecraft:iron_block"),
            AccentBlock = _ui.GetBlock("accent", "minecraft:gray_concrete"),
            ParapetBlock = _ui.GetBlock("rail", "minecraft:iron_bars"),
            IndustryLatticeBlock = _ui.GetBlock("lattice", "minecraft:iron_chain"),
            GlazingBlock = _ui.GetBlock("glaze", "minecraft:glass"),
            SeatBlock = _ui.GetBlock("light", "minecraft:redstone_lamp"),
            PowerLight = _ui.GetBool("light"),
        };

        int width, depth, height;

        if (_kind == "boiler_house" || _kind == "turbine_hall")
        {
            bool boiler = _kind == "boiler_house";
            int len = _ui.GetInt("len"), wid = _ui.GetInt("wid"), hgt = _ui.GetInt("hgt");
            int bay = _ui.GetInt("bay"), lv = _ui.GetInt("lv");
            bool annex = _ui.GetBool("annex");
            string face = _ui.GetChoice("face", "south");

            spec.FacadeFace = face;
            spec.PowerLength = len;
            spec.PowerWidth = wid;
            spec.PowerHeight = hgt;
            spec.PowerBay = bay;
            spec.PowerLevels = lv;
            spec.PowerCrane = _ui.GetBool("crane");
            spec.PowerLouver = _ui.GetBool("louver");
            spec.PowerGate = _ui.GetBool("gate");
            spec.PowerAnnex = annex;

            width = len + 2 + (boiler ? 6 : 0);
            depth = wid + 2 + (annex ? 8 : 0);
            height = hgt + 2;
            summary = $"{(boiler ? "ボイラ建屋" : "タービン建屋")} " +
                      $"長さ{len}×幅{wid}×高さ{hgt} / 柱間隔{bay}・床{lv}段 / " +
                      $"{(spec.PowerCrane ? "天井クレーンあり" : "クレーンなし")} / " +
                      $"{(spec.PowerGate ? "搬入口あり" : "搬入口なし")} / " +
                      $"{(annex ? "付属棟あり" : "付属棟なし")} / 正面{FaceJp(face)} / " +
                      $"全高{height}・敷地{width}×{depth}" +
                      (boiler ? " / 上部から煙道が張り出す" : "");
        }
        else if (_kind == "stack")
        {
            int hgt = _ui.GetInt("hgt"), db = _ui.GetInt("db");
            int dt = Math.Min(_ui.GetInt("dt"), db);
            int flues = _ui.GetInt("flues");

            spec.PowerHeight = hgt;
            spec.PowerDiameter = db;
            spec.PowerTopDiameter = dt;
            spec.PowerCount = flues;
            spec.PowerLadder = _ui.GetBool("ladder");

            width = depth = db + 6;
            height = hgt + 2;
            summary = $"煙突 高さ{hgt} / 底部 直径{db}→頂部 直径{dt} / " +
                      $"{(flues > 0 ? $"内筒{flues}本" : "内筒なし")} / " +
                      $"{(spec.PowerLadder ? "はしご・踊り場あり" : "はしごなし")} / " +
                      $"{(spec.PowerLight && hgt >= 60 ? "航空障害灯あり" : "灯火なし")} / " +
                      $"全高{height}・敷地{width}角" +
                      (spec.PowerLight && hgt < 60 ? "（60未満なので灯火は付かない）" : "") +
                      (dt < _ui.GetInt("dt") ? "（頂部の直径は底部までに抑えた）" : "");
        }
        else if (_kind == "cooling_tower")
        {
            int hgt = _ui.GetInt("hgt"), db = _ui.GetInt("db");
            int th = Math.Min(_ui.GetInt("throat"), db);
            int tp = Math.Min(Math.Max(_ui.GetInt("top"), th), db);
            int inlet = Math.Min(_ui.GetInt("inlet"), Math.Max(2, hgt / 3));
            int cols = _ui.GetInt("cols");

            spec.PowerHeight = hgt;
            spec.PowerDiameter = db;
            spec.PowerThroat = th;
            spec.PowerTopDiameter = tp;
            spec.PowerInlet = inlet;
            spec.PowerCount = cols;
            spec.PowerBasin = _ui.GetBool("basin");

            width = depth = db + 6;
            height = hgt + 1;
            int yt = inlet + (int)Math.Round((hgt - inlet) * 0.75);
            summary = $"自然通風冷却塔 高さ{hgt} / 底部{db}・喉部{th}（高さ{yt}）・頂部{tp} / " +
                      $"空気取入口{inlet}・斜め柱{cols}組 / " +
                      $"{(spec.PowerBasin ? "水盤あり" : "水盤なし")} / " +
                      $"{(spec.PowerLight && hgt >= 60 ? "航空障害灯あり" : "灯火なし")} / " +
                      $"全高{height}・敷地{width}角" +
                      (th >= db ? "（喉部が底部以上なのでほぼ円筒になる）" : "");
        }
        else if (_kind == "containment")
        {
            string shape = _ui.GetChoice("shape", "cylinder");
            int dia = _ui.GetInt("dia"), hgt = _ui.GetInt("hgt"), wall = _ui.GetInt("wall");
            bool annex = _ui.GetBool("annex");
            string face = _ui.GetChoice("face", "south");
            bool box = shape == "box";

            spec.FacadeFace = face;
            spec.PowerShape = shape;
            spec.PowerDiameter = dia;
            spec.PowerHeight = hgt;
            spec.PowerWall = wall;
            spec.PowerGate = _ui.GetBool("gate");
            spec.PowerCrane = _ui.GetBool("crane");
            spec.PowerAnnex = annex;

            int dome = box ? 0 : Math.Max(2, dia / 2);
            width = dia + 6 + (annex ? Math.Max(8, dia / 3) : 0);
            depth = dia + 6;
            height = hgt + dome + 2;
            summary = $"原子炉格納容器（{(box ? "BWR 角形建屋" : "PWR 円筒＋ドーム")}） " +
                      $"内径{dia}×高さ{hgt}・壁厚{wall}" +
                      (box ? "" : $" / 半球ドーム 高さ{dome}") +
                      $" / {(spec.PowerGate ? "エアロックあり" : "エアロックなし")}" +
                      (box ? $" / {(spec.PowerCrane ? "天井クレーンあり" : "クレーンなし")}" : "") +
                      $" / {(annex ? "補助建屋あり" : "補助建屋なし")} / 正面{FaceJp(face)} / " +
                      $"全高{height}・敷地{width}×{depth}";
        }
        else
        {
            int len = _ui.GetInt("len"), wid = _ui.GetInt("wid");
            int gh = _ui.GetInt("gh"), bays = _ui.GetInt("bays"), trs = _ui.GetInt("trs");
            string face = _ui.GetChoice("face", "south");

            spec.FacadeFace = face;
            spec.PowerLength = len;
            spec.PowerWidth = wid;
            spec.PowerHeight = gh;
            spec.PowerCount = bays;
            spec.PowerTransformers = trs;
            spec.PowerFence = _ui.GetBool("fence");

            width = len;
            depth = wid;
            height = gh + 3;
            int span = Math.Max(6, len / Math.Max(1, bays));
            summary = $"変電ヤード 長さ{len}×幅{wid} / 門型 高さ{gh}・{bays}回線（間隔{span}）/ " +
                      $"母線3相 / 変圧器{trs}台 / " +
                      $"{(spec.PowerFence ? "外周フェンスあり" : "フェンスなし")} / " +
                      $"正面{FaceJp(face)} / 全高{height}・敷地{width}×{depth}";
        }

        spec.Width = width;
        spec.Depth = depth;
        spec.Height = height;
        return spec;
    }
}
