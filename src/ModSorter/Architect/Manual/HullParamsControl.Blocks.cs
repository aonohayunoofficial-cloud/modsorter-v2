namespace ModSorter.Architect.Manual;

// 船体の「使用ブロック」の並び。HullParamsControl の partial。
// Panel.cs が9KBの目安に達したので、素材の選択だけを分けた。
// 船種が増えて部品が増えるとここが伸びるので、スライダー側と別に持つ。
public sealed partial class HullParamsControl
{
    private void BuildBlockPanel(HullPreset p)
    {
        _ui.Heading("使用ブロック")
           .BlockPick("shell", "外板", p.Shell)
           .BlockPick("deck", "甲板・舷縁", p.Deck)
           .BlockPick("keelb", "竜骨・船首材・船尾材", p.Keelb)
           .BlockPick("frameb", "フレーム・フロア材", p.Frameb)
           .BlockPick("railb", "舷墻・手すり", p.Railb)
           .BlockPick("mastb", "マスト・帆桁・櫂", p.Mastb)
           .BlockPick("sailb", "帆", p.Sailb)
           .BlockPick("shieldb", "盾（1枚目）", p.Shieldb)
           .BlockPick("shieldb2", "盾（2枚目）", p.Shieldb2)
           .BlockPick("fitb", "舵・舵柄・貫通横梁・砲身・飾り", p.Fitb)
           .BlockPick("castleb", "船楼", p.Castleb)
           .Note("ゴクスタ船は船体がオーク、甲板が松。コグ船はオーク一色。" +
                 "軍艦は船体を黒く塗るのでダークオーク、甲板は白木のマツ材に近い白樺。" +
                 "高さや枚数が0の部品はブロックを使わない。");
    }
}
