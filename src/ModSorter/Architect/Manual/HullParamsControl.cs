using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 船体（structure_type="hull:<船種>"）のパラメータUI。船種は kind 引数で切り替える。
// 座標生成は HullExpander が受け持つので、ここは hull_* を渡すだけ。
// 外寸は HullExpander.Extent（展開側と同じ Form を通る）から取るので、
// スライダーの表示値と生成物の外寸が食い違わない。
//
// ファイル分割（1ファイル9KB以下の目安）:
//   HullParamsControl.cs         … クラスの器と BuildSpec（spec への詰め替え）
//   HullParamsControl.Panel.cs   … スライダー・選択肢・ブロック選択の並び
//   HullParamsControl.Summary.cs … 要約文
//   HullPresets.cs               … 船種ごとの初期値と実物の説明文
// スライダーの並びは全船種で共通で、船種ごとに変わるのは初期値だけ。
public sealed partial class HullParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;
    private readonly string _kind;
    private readonly HullPreset _p;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public HullParamsControl(string kind)
    {
        _kind = (kind ?? "longship").Trim().ToLowerInvariant();
        _p = HullPresets.Of(_kind);
        _ui = new ParamPanelBuilder(this, Raise);

        BuildPanel(_p);

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string shell = _ui.GetBlock("shell", _p.Shell);
        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(shell);

        string face = _ui.GetChoice("bow", "south");
        int depth = _ui.GetInt("depth");
        int draft = Math.Min(_ui.GetInt("draft"), depth);

        var spec = new StructureSpec
        {
            StructureType = "hull:" + _kind,
            FacadeFace = face,
            HullLength = _ui.GetInt("len"),
            HullBeam = _ui.GetInt("beam"),
            HullDepth = depth,
            HullDraft = draft,
            HullSection = _ui.GetInt("section"),
            HullEntryAngle = _ui.GetInt("entry"),
            HullBowFullness = _ui.GetInt("bowfull"),
            HullRunRatio = _ui.GetInt("run"),
            HullSternFullness = _ui.GetInt("sternfull"),
            HullTransom = _ui.GetInt("transom"),
            HullStemRake = _ui.GetInt("rake"),
            HullSternRise = _ui.GetInt("rise"),
            HullFlare = _ui.GetInt("flare"),
            HullTumblehome = _ui.GetInt("tumble"),
            HullSheer = _ui.GetInt("sheer"),
            HullFrameStep = _ui.GetInt("frame"),
            HullKeelDepth = _ui.GetInt("keel"),
            HullBulwark = _ui.GetInt("bulwark"),
            HullBeamStep = _ui.GetInt("beam_step"),
            HullOpenBoat = _ui.GetBool("open_boat"),
            HullThwartStep = _ui.GetInt("thwart"),
            HullMastCount = _ui.GetInt("masts"),
            HullMastHeight = _ui.GetInt("mast_h"),
            HullSail = _ui.GetChoice("sail", "none"),
            HullSailWidth = _ui.GetInt("sail_w"),
            HullSailHeight = _ui.GetInt("sail_h"),
            HullGunRows = _ui.GetInt("gun_rows"),
            HullGunStep = _ui.GetInt("gun_step"),
            HullGunBase = _ui.GetInt("gun_base"),
            HullOarPerSide = _ui.GetInt("row_oars"),
            HullHouseDecks = _ui.GetInt("house_decks"),
            HullHouseLength = _ui.GetInt("house_len"),
            HullHouseShift = _ui.GetInt("house_shift"),
            HullFunnel = _ui.GetInt("funnel"),
            HullHolds = _ui.GetInt("holds"),
            HullDerrick = _ui.GetBool("derrick"),
            HullShieldPerSide = _ui.GetInt("shields"),
            HullSteeringOar = _ui.GetBool("rudder"),
            HullSternRudder = _ui.GetBool("stern_rudder"),
            HullCastleAft = _ui.GetInt("castle_aft"),
            HullCastleFore = _ui.GetInt("castle_fore"),
            HullCastleLength = _ui.GetInt("castle_len"),
            HullStemHead = _ui.GetChoice("head", "none"),
            HullBlock = shell,
            DeckBlock = _ui.GetBlock("deck", _p.Deck),
            BaseBlock = _ui.GetBlock("keelb", _p.Keelb),
            AccentBlock = _ui.GetBlock("frameb", _p.Frameb),
            ParapetBlock = _ui.GetBlock("railb", _p.Railb),
            SuperstructureBlock = _ui.GetBlock("mastb", _p.Mastb),
            RoofBlock = _ui.GetBlock("sailb", _p.Sailb),
            TowerBlock = _ui.GetBlock("shieldb", _p.Shieldb),
            HullShieldBlockAlt = _ui.GetBlock("shieldb2", _p.Shieldb2),
            SeatBlock = _ui.GetBlock("fitb", _p.Fitb),
            HullCastleBlock = _ui.GetBlock("castleb", _p.Castleb),
            HullFunnelBlock = _ui.GetBlock("funnelb", _p.Funnelb),
            GlazingBlock = _ui.GetBlock("glassb", _p.Glassb)
        };

        // 外寸は展開側の Form と Top から取る。UI と生成側で式を二重に持たない。
        // Extent は canonical（船首 +z）なので、東西向きでは幅と奥行きを入れ替える。
        var ext = HullExpander.Extent(spec);
        bool swap = face is "east" or "west";
        spec.Width = swap ? ext.Depth : ext.Width;
        spec.Depth = swap ? ext.Width : ext.Depth;
        spec.Height = ext.Height;

        summary = BuildSummary(spec, face, depth, draft);
        return spec;
    }
}
