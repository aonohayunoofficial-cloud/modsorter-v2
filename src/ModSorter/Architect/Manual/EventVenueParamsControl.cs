using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 屋外イベント会場。座標生成は VenueExpander（structure_type="venue"）が持ち、
// ここは実在の会場の寸法比に沿った既定値と、その根拠の提示だけを担う。
//   円形闘技場   … コロッセウム（外形188×156m / アリーナ87×55m / 高さ48m /
//                  ポディウム壁5m / 外周アーチ約6.8m間隔3層 / 屋根なし）
//   競技場       … 近代スタジアム（角丸の連続ボウル）。片面スタンド単体も同じ種類の中。
//   野外音楽堂   … エピダウロス劇場（オルケストラ径24.65m / カヴェア径119m / 55段 /
//                  下34段でディアゾマ分割 / 勾配26度）＋ハリウッドボウルの同心円シェル
//   ステージ     … 櫓ステージ。屋根は4隅の柱と桁で支える
//   テント広場   … マーケットテントの列。地面は既定で敷かない
public sealed class EventVenueParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public EventVenueParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("種類")
           .Choice("kind", "会場の種類", new[]
           {
               ("円形闘技場", "arena"),
               ("競技場", "stadium"),
               ("野外音楽堂", "bandshell"),
               ("ステージ", "stage"),
               ("テント広場", "tents"),
           }, "arena")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south");

        // ===== 円形闘技場 =====
        _ui.BeginChoiceGroup("kind", "arena")
           .Heading("円形闘技場")
           .Note("コロッセウム基準。外形188×156m・アリーナ87×55m・高さ48m・" +
                 "アリーナと最前列の間に5mのポディウム壁・外周は約6.8m間隔のアーチ列3層・屋根なし。" +
                 "既定値はこの比率（アリーナが長径の44%と短径の33%、全高が長径の28%）に合わせてある。" +
                 "本物の客席勾配は37度だが、踏面3・蹴上2の34度にして登れる形にしている。")
           .IntSlider("arW", "長径", 25, 63, 61)
           .IntSlider("arD", "短径", 21, 63, 51)
           .IntSlider("arRows", "客席の段数", 2, 14, 5)
           .IntSlider("arRun", "踏面", 1, 4, 3, "1段の奥行")
           .IntSlider("arRise", "蹴上", 1, 3, 2, "1段の高さ")
           .IntSlider("arPodium", "ポディウム壁", 0, 12, 5, "アリーナ面から最前列までの高さ")
           .IntSlider("arWall", "外壁", 0, 12, 4, "最上段からさらに上へ")
           .Toggle("arGate", "入場路あり", "入場路なし", true)
           .Toggle("arRoof", "日除けあり", "屋根なし", false)
           .EndGroup();

        // ===== 競技場 =====
        _ui.BeginChoiceGroup("kind", "stadium")
           .Heading("競技場")
           .Note("四周が連続した角丸のボウル。ピッチの上は開き、外周は閉じる。" +
                 "片面だけのスタンドは背面のコンコース棟・妻壁・持ち出し屋根まで作って単独で完結させる。")
           .Choice("stMode", "形式", new[]
           {
               ("四周ボウル", "bowl"), ("片面スタンド単体", "single"),
           }, "bowl")
           .BeginChoiceGroup("stMode", "bowl")
           .IntSlider("stW", "全体の幅", 25, 63, 57)
           .IntSlider("stD", "全体の奥行", 21, 63, 47)
           .IntSlider("stWall", "外装の立ち上がり", 0, 12, 3)
           .Toggle("stGate", "入場路あり", "入場路なし", true)
           .Toggle("stRoofBowl", "スタンド屋根あり", "屋根なし", false)
           .EndGroup()
           .BeginChoiceGroup("stMode", "single")
           .IntSlider("stOneW", "スタンドの幅", 11, 63, 41)
           .IntSlider("stOneWall", "背面壁の高さ", 1, 12, 4)
           .Toggle("stRoofOne", "屋根あり", "屋根なし", true)
           .EndGroup()
           .IntSlider("stRows", "段数", 2, 16, 7)
           .IntSlider("stRun", "踏面", 1, 4, 2)
           .IntSlider("stRise", "蹴上", 1, 3, 1)
           .IntSlider("stPodium", "ピッチ側の立ち上がり", 0, 8, 2)
           .IntSlider("stRoofH", "屋根の持ち上げ", 1, 12, 4)
           .EndGroup();

        // ===== 野外音楽堂 =====
        _ui.BeginChoiceGroup("kind", "bandshell")
           .Heading("野外音楽堂")
           .Note("エピダウロス劇場の扇形（客席は210度・カヴェア径はオルケストラ径の約4.8倍・" +
                 "勾配26度・下34段のあとに水平通路ディアゾマ・放射状の階段）に、" +
                 "ハリウッドボウルの同心円シェル（半ドーム・3リングごとの縞）を合わせる。")
           .IntSlider("bsOrch", "オルケストラ半径", 3, 16, 6)
           .IntSlider("bsRows", "客席の段数", 3, 24, 12)
           .IntSlider("bsRun", "踏面", 1, 3, 2)
           .IntSlider("bsRise", "蹴上", 1, 3, 1)
           .IntSlider("bsShellR", "シェルの半径", 4, 20, 9)
           .IntSlider("bsShellH", "シェルの高さ", 5, 28, 12)
           .IntSlider("bsStage", "舞台の高さ", 0, 6, 2)
           .EndGroup();

        // ===== ステージ =====
        _ui.BeginChoiceGroup("kind", "stage")
           .Heading("ステージ")
           .Note("台・背面の幕・4隅の柱と桁・切妻屋根。屋根は柱で支え、妻面も塞ぐので浮かない。")
           .IntSlider("sgW", "間口", 7, 41, 15)
           .IntSlider("sgD", "奥行", 5, 25, 9)
           .IntSlider("sgDeck", "台の高さ", 1, 8, 3)
           .IntSlider("sgBack", "背面の幕", 0, 16, 8, "0で幕なし")
           .IntSlider("sgPost", "柱の高さ", 0, 16, 6, "0で屋根なし")
           .Choice("sgRoofType", "屋根の形", new[] { ("切妻", "gable"), ("陸屋根", "flat") }, "gable")
           .EndGroup();

        // ===== テント広場 =====
        _ui.BeginChoiceGroup("kind", "tents")
           .Heading("テント広場")
           .Note("マーケットテント基準（間口3〜6m・軒2.2m・棟3.5m前後）。" +
                 "地面は既定で敷かないので、床とテントの床が二重にならない。")
           .IntSlider("tnCount", "張数", 1, 12, 4)
           .IntSlider("tnW", "1張の間口", 3, 15, 7)
           .IntSlider("tnD", "1張の奥行", 3, 21, 9)
           .IntSlider("tnEave", "軒の高さ", 2, 8, 3)
           .IntSlider("tnGap", "テント間隔", 1, 10, 3)
           .Toggle("tnTwoRow", "2列に並べる", "1列に並べる", false)
           .BeginGroup("tnTwoRow")
           .IntSlider("tnAisle", "列間の通路幅", 2, 16, 6)
           .EndGroup()
           .Toggle("tnClosed", "側面を閉じる", "側面を開ける", false)
           .Toggle("tnPave", "地面を敷く", "地面を敷かない", false)
           .EndGroup();

        _ui.Heading("使用ブロック")
           .BlockPick("structure", "躯体", "minecraft:smooth_stone")
           .BlockPick("seat", "座席", "minecraft:polished_andesite")
           .BlockPick("field", "競技面・床", "minecraft:sand")
           .BlockPick("roof", "屋根・天幕", "minecraft:white_concrete")
           .BlockPick("accent", "外装・装飾", "minecraft:stone_bricks");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string structure = _ui.GetBlock("structure", "minecraft:smooth_stone");
        string seat = _ui.GetBlock("seat", "minecraft:polished_andesite");
        string field = _ui.GetBlock("field", "minecraft:sand");
        string roof = _ui.GetBlock("roof", "minecraft:white_concrete");
        string accent = _ui.GetBlock("accent", "minecraft:stone_bricks");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(structure);

        string kind = _ui.GetChoice("kind", "arena");
        string front = _ui.GetChoice("front", "south");

        var spec = new StructureSpec
        {
            StructureType = "venue",
            VenueKind = kind,
            FacadeFace = front,
            WallBlock = structure,
            SeatBlock = seat,
            FloorBlock = field,
            RoofBlock = roof,
            AccentBlock = accent,
            NoEntrance = true
        };

        switch (kind)
        {
            case "stadium":
                summary = FillStadium(spec);
                break;
            case "bandshell":
                summary = FillBandshell(spec);
                break;
            case "stage":
                summary = FillStage(spec);
                break;
            case "tents":
                summary = FillTents(spec);
                break;
            default:
                summary = FillArena(spec);
                break;
        }

        summary += $" / 正面{front}";
        return spec;
    }

    private string FillArena(StructureSpec spec)
    {
        int w = _ui.GetInt("arW");
        int d = _ui.GetInt("arD");
        int rows = _ui.GetInt("arRows");
        int run = _ui.GetInt("arRun");
        int rise = _ui.GetInt("arRise");
        int podium = _ui.GetInt("arPodium");
        int wall = _ui.GetInt("arWall");
        bool awning = _ui.GetBool("arRoof");

        spec.Width = w;
        spec.Depth = d;
        spec.Height = podium + (rows - 1) * rise + wall + (awning ? 2 : 1);
        spec.VenueRows = rows;
        spec.VenueRun = run;
        spec.VenueRise = rise;
        spec.VenuePodium = podium;
        spec.VenueWall = wall;
        spec.VenueGates = _ui.GetBool("arGate");
        spec.VenueRoof = awning;

        int ring = 2 + rows * run;
        int aw = Math.Max(0, w - ring * 2);
        int ad = Math.Max(0, d - ring * 2);
        return $"円形闘技場 外形{w}×{d} / アリーナ{aw}×{ad} / {rows}段(踏面{run}・蹴上{rise})" +
               $" / ポディウム{podium} / 外壁{wall}" + (awning ? " / 日除けあり" : " / 屋根なし");
    }

    private string FillStadium(StructureSpec spec)
    {
        string mode = _ui.GetChoice("stMode", "bowl");
        int rows = _ui.GetInt("stRows");
        int run = _ui.GetInt("stRun");
        int rise = _ui.GetInt("stRise");
        int podium = _ui.GetInt("stPodium");

        spec.VenueSides = mode;
        spec.VenueRows = rows;
        spec.VenueRun = run;
        spec.VenueRise = rise;
        spec.VenuePodium = podium;
        spec.VenueRoofHeight = _ui.GetInt("stRoofH");

        int top = podium + (rows - 1) * rise;

        if (mode == "single")
        {
            int w = _ui.GetInt("stOneW");
            int wall = _ui.GetInt("stOneWall");
            bool roof = _ui.GetBool("stRoofOne");

            spec.Width = w;
            spec.Depth = rows * run + 3;
            spec.Height = top + wall + 2;
            spec.VenueWall = wall;
            spec.VenueRoof = roof;
            spec.VenueGates = false;

            return $"片面スタンド 幅{w}×奥行{spec.Depth} / {rows}段(踏面{run}・蹴上{rise})" +
                   $" / 背面壁{wall}" + (roof ? " / 屋根あり" : " / 屋根なし");
        }

        int bw = _ui.GetInt("stW");
        int bd = _ui.GetInt("stD");
        int bwall = _ui.GetInt("stWall");
        bool broof = _ui.GetBool("stRoofBowl");

        spec.Width = bw;
        spec.Depth = bd;
        spec.Height = top + bwall + (broof ? _ui.GetInt("stRoofH") + 1 : 1);
        spec.VenueWall = bwall;
        spec.VenueRoof = broof;
        spec.VenueGates = _ui.GetBool("stGate");

        int ring = 1 + rows * run;
        int pw = Math.Max(0, bw - ring * 2);
        int pd = Math.Max(0, bd - ring * 2);
        return $"競技場 外形{bw}×{bd} / ピッチ{pw}×{pd} / 四周{rows}段(踏面{run}・蹴上{rise})" +
               (broof ? " / スタンド屋根あり" : " / 屋根なし");
    }

    private string FillBandshell(StructureSpec spec)
    {
        int orch = _ui.GetInt("bsOrch");
        int rows = _ui.GetInt("bsRows");
        int run = _ui.GetInt("bsRun");
        int rise = _ui.GetInt("bsRise");
        int shellR = _ui.GetInt("bsShellR");
        int shellH = _ui.GetInt("bsShellH");
        int stage = _ui.GetInt("bsStage");

        while (rows > 1 && (orch + 1 + rows * run) * 2 + 1 > 63) rows--;
        int cavea = orch + 1 + rows * run;

        spec.Width = cavea * 2 + 1;
        spec.Depth = shellR + 2 + cavea + 1;
        spec.Height = Math.Max(stage + shellH, 1 + rows * rise) + 1;
        spec.VenueOrchestra = orch;
        spec.VenueRows = rows;
        spec.VenueRun = run;
        spec.VenueRise = rise;
        spec.VenueShellRadius = shellR;
        spec.VenueShellHeight = shellH;
        spec.VenueStage = stage;

        return $"野外音楽堂 {spec.Width}×{spec.Depth} / オルケストラ半径{orch} / 客席{rows}段(210度)" +
               $" / シェル半径{shellR}×高{shellH} / 舞台高{stage}";
    }

    private string FillStage(StructureSpec spec)
    {
        int w = _ui.GetInt("sgW");
        int d = _ui.GetInt("sgD");
        int deck = _ui.GetInt("sgDeck");
        int back = _ui.GetInt("sgBack");
        int post = _ui.GetInt("sgPost");
        string roofType = _ui.GetChoice("sgRoofType", "gable");

        int ridge = post > 0
            ? deck + post + 1 + (roofType == "gable" ? (d - 1) / 2 : 0)
            : deck + back;

        spec.Width = w;
        spec.Depth = d;
        spec.Height = Math.Max(ridge, deck + back) + 1;
        spec.VenueStage = deck;
        spec.VenueWall = back;
        spec.VenueRoofHeight = post;
        spec.RoofType = roofType;

        string bk = back > 0 ? $" / 幕{back}" : " / 幕なし";
        string rf = post > 0 ? $" / 柱{post}・{(roofType == "gable" ? "切妻" : "陸屋根")}" : " / 屋根なし";
        return $"ステージ {w}×{d} / 台高{deck}{bk}{rf}";
    }

    private string FillTents(StructureSpec spec)
    {
        int count = _ui.GetInt("tnCount");
        int tw = _ui.GetInt("tnW") | 1;
        int td = _ui.GetInt("tnD");
        int eave = _ui.GetInt("tnEave");
        int gap = _ui.GetInt("tnGap");
        bool twoRow = _ui.GetBool("tnTwoRow");
        int aisle = _ui.GetInt("tnAisle");

        int rows = twoRow ? 2 : 1;
        int perRow = (count + rows - 1) / rows;

        spec.Width = perRow * tw + (perRow - 1) * gap;
        spec.Depth = twoRow ? td * 2 + aisle : td;
        spec.Height = eave + (tw - 1) / 2 + 1;
        spec.VenueTentCount = count;
        spec.VenueTentWidth = tw;
        spec.VenueTentDepth = td;
        spec.VenueTentEave = eave;
        spec.VenueTentGap = gap;
        spec.VenueTentRows = rows;
        spec.VenueTentAisle = aisle;
        spec.VenueTentClosed = _ui.GetBool("tnClosed");
        spec.VenueTentPave = _ui.GetBool("tnPave");

        string row = twoRow ? $"2列(通路{aisle})" : "1列";
        string side = spec.VenueTentClosed ? "側面閉鎖" : "側面開放";
        string pave = spec.VenueTentPave ? "地面あり" : "地面なし";
        return $"テント広場 {count}張 {tw}×{td} 軒{eave} / {row} / 間隔{gap} / {side} / {pave}";
    }
}
