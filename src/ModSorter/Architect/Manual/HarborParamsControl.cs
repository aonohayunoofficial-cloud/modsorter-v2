using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 港湾の平面土木3種（岸壁・桟橋・防波堤）のパラメータUI。
// 中分類ごとにクラスを分けず、コンストラクタで種類を受けて表示を切り替える。
// ManualCatalog からは new HarborParamsControl("quay") のように種類を渡す。
// 座標生成は HarborExpander が受け持つので、ここは structure_type="harbor:<種類>" と
// 断面の寸法・素材を渡すだけ。
//
// 既定値の根拠（実寸）:
//   岸壁   … 計画水深 10m 級。天端高は朔望平均満潮位 +0.5〜1.5m。エプロン幅は水深別に
//            10〜20m、コンテナ荷役では 30m 級。ケーソン幅は水深と同程度。
//            係船柱（曲柱）の最大間隔は船型別に 10〜45m、岸壁端部から 2.0m。
//            ガントリークレーンの軌間は 30.48m（100ft）が標準。
//   桟橋   … 直杭式横桟橋。鋼管杭は径 0.6〜1.0m・杭間隔 4〜6m、上部工厚 1.5〜2m。
//            陸側とは幅 8m 前後の渡橋でつなぐ。
//   防波堤 … 混成堤。基礎マウンド（捨石）の斜面 1:2、堤体幅 10m 前後、天端は上部
//            コンクリートで押さえ、海側に消波ブロックを被覆する。
//
// 延長は構造物の上限に合わせて最大 64。実物の 1 バース（185m 級）のうち 64m ぶんを
// 切り出す前提で、並べて置けば 1 バースになる粒度にしてある。
public sealed class HarborParamsControl : UserControl, IManualParamControl
{
    private readonly string _kind;
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public HarborParamsControl(string kind)
    {
        _kind = KindOf(kind);
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("sea", "海側", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        switch (_kind)
        {
            case "pier": BuildPierUi(); break;
            case "breakwater": BuildBreakwaterUi(); break;
            default: BuildQuayUi(); break;
        }

        _ui.Heading("使用ブロック")
           .BlockPick("body", "本体コンクリート", "minecraft:gray_concrete")
           .BlockPick("pave", "舗装・天端", "minecraft:light_gray_concrete")
           .BlockPick("rubble", "捨石・中詰", "minecraft:cobblestone")
           .BlockPick("trim", "縁石・レール・杭", "minecraft:polished_andesite")
           .BlockPick("fitting", "係船柱・防舷材", "minecraft:black_concrete")
           .BlockPick("armor", "消波ブロック", "minecraft:andesite")
           .BlockPick("parapet", "パラペット", "minecraft:gray_concrete");

        Content = _ui.Root;
    }

    private static string KindOf(string kind) => (kind ?? "").Trim().ToLowerInvariant() switch
    {
        "pier" => "pier",
        "breakwater" => "breakwater",
        _ => "quay"
    };

    private void BuildQuayUi()
    {
        _ui.Heading("規模")
           .Note("水深10m級・天端高は満潮位+1.5m相当・エプロン20mが一般貨物の目安。")
           .IntSlider("len", "延長", 12, 64, 48)
           .IntSlider("depth", "計画水深", 3, 24, 10)
           .IntSlider("crown", "天端高", 1, 6, 2, "水面から天端まで")
           .IntSlider("body", "ケーソン幅", 4, 24, 10, "水深と同程度が目安")
           .IntSlider("apron", "エプロン幅", 4, 40, 20, "コンテナ荷役なら30")
           .IntSlider("mound", "基礎マウンド高", 0, 8, 2, "捨石。海側へ1:2で下る");

        _ui.Heading("付帯設備")
           .Toggle("bollard", "係船柱あり", "係船柱なし", true)
           .BeginGroup("bollard")
           .IntSlider("bstep", "係船柱の間隔", 10, 45, 20)
           .EndGroup()
           .Toggle("fender", "防舷材あり", "防舷材なし", true)
           .Toggle("rail", "クレーン軌道あり", "クレーン軌道なし", false)
           .BeginGroup("rail")
           .IntSlider("gauge", "軌間", 10, 32, 30, "30でガントリークレーンの100ft相当")
           .EndGroup();
    }

    private void BuildPierUi()
    {
        _ui.Heading("規模")
           .Note("鋼管杭を4〜6m間隔の格子に打ち、厚さ2mの上部工を載せる。")
           .IntSlider("len", "延長", 12, 64, 40)
           .IntSlider("wide", "幅", 6, 40, 15)
           .IntSlider("depth", "計画水深", 3, 24, 8)
           .IntSlider("crown", "天端高", 1, 6, 2)
           .IntSlider("step", "杭間隔", 3, 10, 5)
           .IntSlider("slab", "上部工厚", 1, 4, 2);

        _ui.Heading("渡橋")
           .Note("陸側へ幅8mで延ばす取付部。0で本体のみ。")
           .Toggle("appr", "渡橋あり", "渡橋なし", true)
           .BeginGroup("appr")
           .IntSlider("apprLen", "渡橋の長さ", 4, 32, 12)
           .EndGroup();

        _ui.Heading("付帯設備")
           .Toggle("bollard", "係船柱あり", "係船柱なし", true)
           .BeginGroup("bollard")
           .IntSlider("bstep", "係船柱の間隔", 10, 45, 20)
           .EndGroup()
           .Toggle("fender", "防舷材あり", "防舷材なし", true);
    }

    private void BuildBreakwaterUi()
    {
        _ui.Heading("規模")
           .Note("混成堤。捨石マウンドの上にケーソンを据え、上部コンクリートで押さえる。")
           .IntSlider("len", "延長", 12, 64, 48)
           .IntSlider("depth", "計画水深", 3, 24, 10)
           .IntSlider("crown", "天端高", 1, 10, 5, "水面から天端まで")
           .IntSlider("body", "堤体幅", 4, 24, 10)
           .IntSlider("mound", "基礎マウンド高", 1, 10, 3, "斜面は1:2")
           .IntSlider("parapet", "パラペット", 0, 4, 2, "海側の立ち上がり");

        _ui.Heading("消波工")
           .Toggle("armor", "消波ブロックあり", "消波ブロックなし", false)
           .BeginGroup("armor")
           .IntSlider("armorW", "被覆幅", 2, 12, 8)
           .EndGroup();
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string body = _ui.GetBlock("body", "minecraft:gray_concrete");
        string pave = _ui.GetBlock("pave", "minecraft:light_gray_concrete");
        string rubble = _ui.GetBlock("rubble", "minecraft:cobblestone");
        string trim = _ui.GetBlock("trim", "minecraft:polished_andesite");
        string fitting = _ui.GetBlock("fitting", "minecraft:black_concrete");
        string armor = _ui.GetBlock("armor", "minecraft:andesite");
        string parapet = _ui.GetBlock("parapet", "minecraft:gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        var spec = new StructureSpec
        {
            StructureType = "harbor:" + _kind,
            FacadeFace = _ui.GetChoice("sea", "south"),
            WallBlock = body,
            FloorBlock = pave,
            BaseBlock = rubble,
            AccentBlock = trim,
            SeatBlock = fitting,
            RoofBlock = armor,
            ParapetBlock = parapet
        };

        switch (_kind)
        {
            case "pier": summary = FillPier(spec); break;
            case "breakwater": summary = FillBreakwater(spec); break;
            default: summary = FillQuay(spec); break;
        }
        return spec;
    }

    private string FillQuay(StructureSpec spec)
    {
        int len = _ui.GetInt("len");
        int depth = _ui.GetInt("depth");
        int crown = _ui.GetInt("crown");
        int body = _ui.GetInt("body");
        int apron = _ui.GetInt("apron");
        bool rail = _ui.GetBool("rail");
        bool bollard = _ui.GetBool("bollard");

        spec.Width = len;
        spec.Depth = body + apron;          // 参考値。断面は harbor_* から組む
        spec.Height = depth + crown;        // 参考値
        spec.HarborDepth = depth;
        spec.HarborCrown = crown;
        spec.HarborBody = body;
        spec.HarborApron = apron;
        spec.HarborMound = _ui.GetInt("mound");
        spec.HarborGauge = rail ? _ui.GetInt("gauge") : 0;
        spec.HarborBollardStep = bollard ? _ui.GetInt("bstep") : 0;
        spec.HarborFender = _ui.GetBool("fender");

        string railText = rail ? $"軌道(軌間{spec.HarborGauge})" : "軌道なし";
        string bollardText = bollard ? $"係船柱{spec.HarborBollardStep}m間隔" : "係船柱なし";
        return $"岸壁 延長{len} / 水深{depth}・天端高{crown} / ケーソン幅{body}＋エプロン{apron} / {railText} / {bollardText}";
    }

    private string FillPier(StructureSpec spec)
    {
        int len = _ui.GetInt("len");
        int wide = _ui.GetInt("wide");
        int depth = _ui.GetInt("depth");
        int crown = _ui.GetInt("crown");
        bool appr = _ui.GetBool("appr");
        bool bollard = _ui.GetBool("bollard");

        spec.Width = len;
        spec.Depth = wide + (appr ? _ui.GetInt("apprLen") : 0);
        spec.Height = depth + crown;
        spec.HarborDepth = depth;
        spec.HarborCrown = crown;
        spec.HarborBody = wide;
        spec.HarborPileStep = _ui.GetInt("step");
        spec.HarborSlab = _ui.GetInt("slab");
        spec.HarborApproach = appr ? _ui.GetInt("apprLen") : 0;
        spec.HarborBollardStep = bollard ? _ui.GetInt("bstep") : 0;
        spec.HarborFender = _ui.GetBool("fender");

        string apprText = appr ? $"渡橋{spec.HarborApproach}" : "渡橋なし";
        return $"桟橋 {len}×{wide} / 水深{depth}・天端高{crown} / 杭間隔{spec.HarborPileStep}・上部工厚{spec.HarborSlab} / {apprText}";
    }

    private string FillBreakwater(StructureSpec spec)
    {
        int len = _ui.GetInt("len");
        int depth = _ui.GetInt("depth");
        int crown = _ui.GetInt("crown");
        int body = _ui.GetInt("body");
        int mound = _ui.GetInt("mound");
        bool armor = _ui.GetBool("armor");

        spec.Width = len;
        spec.Depth = body + 4 * mound + (armor ? _ui.GetInt("armorW") : 0);
        spec.Height = depth + crown;
        spec.HarborDepth = depth;
        spec.HarborCrown = crown;
        spec.HarborBody = body;
        spec.HarborMound = mound;
        spec.HarborArmor = armor ? _ui.GetInt("armorW") : 0;
        spec.ParapetHeight = _ui.GetInt("parapet");

        string armorText = armor ? $"消波{spec.HarborArmor}" : "消波なし";
        return $"防波堤 延長{len} / 水深{depth}・天端高{crown} / 堤体幅{body}・マウンド{mound} / パラペット{spec.ParapetHeight} / {armorText}";
    }
}
