using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 縦型容器（サイロ・給水塔・タンク）。3種はパラメータが近いので kind 引数で1クラスに束ねる。
// 屋根の段数は IndustryExpander.RoofLevels を呼んで求めるので、Height の式が
// 展開側とずれない。
//
// 既定値の根拠（実寸）:
//   サイロ … コンクリート製の円型は直径1.5〜6m・高さ5〜18m（直径の2.5〜3倍）。
//            600tのセメントサイロはスカート支持で直径6.4m×高さ20.1m。
//   給水塔 … 千葉高架水槽は内径11m・有効水深5m・容量475m³・満水位標高50m、
//            中央の昇降路は直径約2m。
//   タンク … 10万バレル級で直径39m×高さ20.7m。円錐屋根は勾配1/16以下。
//   防油堤 … 高さ0.5m以上。側板からの距離はタンク高さの1/3（直径15m未満）／
//            1/2（15m以上）以上。
public sealed class VesselParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;
    private readonly string _kind;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public VesselParamsControl(string kind)
    {
        _kind = (kind ?? "silo").Trim().ToLowerInvariant();
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "はしご・開口の向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        if (_kind == "water_tower")
        {
            _ui.Heading("水槽")
               .IntSlider("dia", "水槽の内径", 5, 24, 11, "千葉高架水槽は内径11m・容量475m³")
               .IntSlider("body", "有効水深", 2, 12, 5, "千葉高架水槽は有効水深5m");

            _ui.Heading("塔身")
               .IntSlider("shaftw", "塔身の直径", 2, 20, 4, "千葉高架水槽の中央昇降路は直径約2m")
               .IntSlider("shafth", "塔身の高さ", 4, 60, 30, "満水位標高50m級の高置水槽に相当");

            _ui.Heading("屋根")
               .Choice("roof", "屋根の形", new[]
               {
                   ("ドーム", "dome"), ("円錐", "cone"), ("陸屋根", "flat"),
               }, "dome")
               .IntSlider("pitch", "屋根の 1/n", 1, 24, 3,
                          "円錐は勾配1/n、ドームは高さ＝直径の1/n");

            _ui.Heading("付帯設備")
               .Toggle("balcony", "点検デッキあり", "デッキなし", true)
               .Toggle("ladder", "外部ラダーあり", "ラダーなし", true)
               .Toggle("manhole", "塔身の出入口あり", "出入口なし", true);
        }
        else if (_kind == "tank")
        {
            _ui.Heading("寸法")
               .IntSlider("dia", "直径", 6, 80, 39, "10万バレル級で直径39m")
               .IntSlider("body", "側板の高さ", 4, 32, 21, "10万バレル級で20.7m");

            _ui.Heading("屋根")
               .Choice("roof", "屋根の形", new[]
               {
                   ("円錐", "cone"), ("ドーム", "dome"), ("陸屋根", "flat"),
               }, "cone")
               .IntSlider("pitch", "屋根の 1/n", 4, 24, 16,
                          "円錐屋根の勾配1/16以下が放爆構造の条件（直径15m以上・高さ9m以上）");

            _ui.Heading("付帯設備")
               .IntSlider("girder", "風止めリングの間隔", 0, 16, 6, "0でリングなし")
               .Toggle("stair", "らせん階段あり", "階段なし", true)
               .Toggle("ladder", "屋根までのラダーあり", "ラダーなし", false)
               .Toggle("manhole", "側板マンホールあり", "マンホールなし", true);

            _ui.Heading("防油堤")
               .IntSlider("dike", "堤の高さ", 0, 4, 1, "実物0.5m以上。1マスが実寸相当。0で堤なし")
               .Note("側板から堤までの距離は、直径15m未満でタンク高さの1/3、" +
                     "15m以上で1/2を自動で取る。");
        }
        else
        {
            _ui.Heading("寸法")
               .IntSlider("dia", "直径", 3, 16, 6, "コンクリート製の円型は直径1.5〜6m。セメントサイロは6.4m")
               .IntSlider("body", "胴の高さ", 4, 48, 18, "直径の2.5〜3倍が目安。600tのセメントサイロで20.1m")
               .IntSlider("skirt", "スカートの高さ", 0, 16, 4, "胴を持ち上げて下に払い出しの空間を作る。0で直置き")
               .Toggle("hopper", "下部ホッパーあり", "平底", true)
               .Note("ホッパーはスカートが2マス以上のときだけ入る。");

            _ui.Heading("屋根")
               .Choice("roof", "屋根の形", new[]
               {
                   ("ドーム", "dome"), ("円錐", "cone"), ("陸屋根", "flat"),
               }, "dome")
               .IntSlider("pitch", "屋根の 1/n", 1, 24, 2,
                          "円錐は勾配1/n、ドームは高さ＝直径の1/n。2で半球");

            _ui.Heading("付帯設備")
               .Toggle("ladder", "外部ラダーあり", "ラダーなし", true)
               .Toggle("manhole", "頂部点検口あり", "点検口なし", true)
               .Toggle("chute", "投入シュートあり", "シュートなし", false);
        }

        _ui.Heading("使用ブロック")
           .BlockPick("shell", "胴板・塔身", "minecraft:light_gray_concrete")
           .BlockPick("roofb", "屋根", "minecraft:gray_concrete")
           .BlockPick("base", "基礎・スカート・防油堤", "minecraft:stone_bricks")
           .BlockPick("deck", "床・デッキ・踏板", "minecraft:smooth_stone")
           .BlockPick("stair", "階段", "minecraft:stone_brick_stairs")
           .BlockPick("rail", "手すり", "minecraft:iron_bars")
           .BlockPick("accent", "バンド・風止めリング", "minecraft:iron_block")
           .BlockPick("glaze", "点検口", "minecraft:glass")
           .BlockPick("light", "灯火", "minecraft:redstone_lamp")
           .Note("外部ラダーは minecraft:ladder を使うので選択項目にしていない。" +
                 "階段は階段ブロックを平ブロックと交互に置くので、そのまま登れる。");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string shell = _ui.GetBlock("shell", "minecraft:light_gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(shell);
        // 梯子は選択項目にしていないので、許可ブロックへ自前で足す。
        if (!allowed.Contains("minecraft:ladder")) allowed.Add("minecraft:ladder");

        string roof = _ui.GetChoice("roof", _kind == "tank" ? "cone" : "dome");
        int pitch = _ui.GetInt("pitch");
        int dia = _ui.GetInt("dia");
        int body = _ui.GetInt("body");
        int roofLv = IndustryExpander.RoofLevels(dia, roof, pitch);

        string roofNote = roof switch
        {
            "cone" => $"円錐屋根 勾配1/{pitch}",
            "flat" => "陸屋根",
            _ => $"ドーム屋根 高さ＝直径の1/{pitch}",
        };

        var spec = new StructureSpec
        {
            StructureType = "industry:" + _kind,
            FacadeFace = _ui.GetChoice("face", "south"),
            IndustryDiameter = dia,
            IndustryBodyHeight = body,
            IndustryRoof = roof,
            IndustryRoofPitch = pitch,
            IndustryLadder = _ui.GetBool("ladder"),
            IndustryManhole = _ui.GetBool("manhole"),
            WallBlock = shell,
            RoofBlock = _ui.GetBlock("roofb", "minecraft:gray_concrete"),
            BaseBlock = _ui.GetBlock("base", "minecraft:stone_bricks"),
            FloorBlock = _ui.GetBlock("deck", "minecraft:smooth_stone"),
            ParapetBlock = _ui.GetBlock("rail", "minecraft:iron_bars"),
            VerandaBlock = _ui.GetBlock("stair", "minecraft:stone_brick_stairs"),
            AccentBlock = _ui.GetBlock("accent", "minecraft:iron_block"),
            GlazingBlock = _ui.GetBlock("glaze", "minecraft:glass"),
            SeatBlock = _ui.GetBlock("light", "minecraft:redstone_lamp")
        };

        int width;
        int height;

        if (_kind == "water_tower")
        {
            int sw = _ui.GetInt("shaftw");
            int sh = _ui.GetInt("shafth");
            bool balcony = _ui.GetBool("balcony");

            spec.IndustryShaftWidth = sw;
            spec.IndustryShaftHeight = sh;
            spec.IndustryBalcony = balcony;

            width = dia + (balcony ? 2 : 0);
            height = 1 + sh + 1 + body + roofLv + 1;
            summary = $"給水塔 水槽 内径{dia}×有効水深{body} / 塔身 直径{sw}×高さ{sh} / " +
                      $"{roofNote} / {(balcony ? "点検デッキあり" : "デッキなし")} / " +
                      $"{(spec.IndustryLadder ? "ラダーあり" : "ラダーなし")} / 全高{height}";
        }
        else if (_kind == "tank")
        {
            int girder = _ui.GetInt("girder");
            int dike = _ui.GetInt("dike");
            bool stair = _ui.GetBool("stair");

            spec.IndustryWindGirder = girder;
            spec.IndustryDike = dike;
            spec.IndustryStair = stair;

            int dist = Math.Max(1, body / (dia < 15 ? 3 : 2));
            width = dike > 0 ? dia + 2 * (dist + 1) : dia + 2;
            height = 2 + body + roofLv;
            string dikeNote = dike > 0
                ? $"防油堤 高さ{dike}・側板から{dist}（タンク高さの1/{(dia < 15 ? 3 : 2)}）"
                : "防油堤なし";
            summary = $"タンク 直径{dia}×側板{body} / {roofNote} / " +
                      $"{(girder > 0 ? $"風止めリング{girder}間隔" : "リングなし")} / " +
                      $"{(stair ? "らせん階段あり（階段ブロック・踏面2・約26.6度）" : "階段なし")} / " +
                      $"{dikeNote} / 全高{height}・敷地{width}角";
        }
        else
        {
            int skirt = _ui.GetInt("skirt");
            bool hopper = _ui.GetBool("hopper");
            bool chute = _ui.GetBool("chute");

            spec.IndustrySkirt = skirt;
            spec.IndustryHopper = hopper;
            spec.IndustryChute = chute;

            width = dia + 2;
            height = 1 + skirt + body + roofLv + (chute ? 1 : 0);
            summary = $"サイロ 直径{dia}×胴{body}（直径の{body / (double)dia:0.0}倍）/ " +
                      $"スカート{skirt} / {(hopper && skirt >= 2 ? "下部ホッパーあり" : "平底")} / " +
                      $"{roofNote} / {(chute ? "投入シュートあり" : "シュートなし")} / 全高{height}";
        }

        spec.Width = width;
        spec.Depth = width;
        spec.Height = height;
        return spec;
    }
}
