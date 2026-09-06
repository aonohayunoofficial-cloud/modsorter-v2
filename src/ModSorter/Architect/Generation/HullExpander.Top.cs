using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 上部構造の素材と寸法。組み立ては次のファイルへ分ける。
//   HullExpander.Rig.cs    … マスト・盾掛け・側舵・船首材の飾り
//   HullExpander.Sail.cs   … 横帆・縦帆と帆桁
//   HullExpander.Gun.cs    … 砲門と砲身
//   HullExpander.Oar.cs    … 櫂
//   HullExpander.Beam.cs   … 貫通横梁・中心線舵
//   HullExpander.Castle.cs … 船楼
//   HullExpander.House.cs  … デッキハウス・煙突
//   HullExpander.Cargo.cs  … 貨物艙口・デリック
// 1ファイル9KB以下の目安に収めるための分割。船種が増えて部品が増えても、
// 寸法の解決はこの Top に集まるので Extent と組み立てで値が食い違わない。
//
// このファイルが10,434バイトになったので、Top の長い計算（櫂の張り出し・
// マストの station・天端）を HullExpander.Top.Fit.cs へ移した。readonly フィールドは
// コンストラクタでしか代入できないため、計算を static メソッドへ出して戻り値を
// 受け取る形にしてある。Top を partial にしたのはそのため。
//
// TopPalette と Top はどちらも HullExpander の直下に置く。Top は Rig / Sail /
// Gun / Oar / Beam / Castle / House / Cargo の各ファイルが引数の型として使うので、
// TopPalette の中へ入れると（= ネスト型になると）それらから型を解決できない。
public static partial class HullExpander
{
    private sealed class TopPalette
    {
        public readonly string Mast, Sail, Shield, ShieldAlt, Fitting, Castle, Funnel, Glass;

        public TopPalette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Mast = Pick(spec.SuperstructureBlock, allowed, fallback);
            Sail = Pick(spec.RoofBlock, allowed, Mast);
            Shield = Pick(spec.TowerBlock, allowed, Mast);
            ShieldAlt = Pick(spec.HullShieldBlockAlt ?? spec.TowerBlock, allowed, Shield);
            Fitting = Pick(spec.SeatBlock, allowed, Mast);
            Castle = Pick(spec.HullCastleBlock ?? spec.SuperstructureBlock, allowed, Mast);
            Funnel = Pick(spec.HullFunnelBlock ?? spec.HullCastleBlock, allowed, Castle);

            // 窓は allowed に無ければガラスを使わず壁と同じ材にする。素材選択に
            // ガラスを入れていない船種で、窓だけ勝手に別の材が混ざるのを避ける。
            Glass = Pick(spec.GlazingBlock, allowed, Castle);
        }
    }

    // 上部構造の寸法。Extent と BuildTopside の両方がこれを通るので、
    // UI に出す外寸と生成物の外寸が食い違わない。
    private sealed partial class Top
    {
        public readonly int MastCount, MastHeight, SailW, SailH, ShieldPerSide, HeadHeight, TopY;
        public readonly int BeamStep, CastleAft, CastleFore, CastleLen;
        public readonly int GunRows, GunStep, GunBase, OarPerSide, OarSide;
        public readonly int HouseDecks, HouseLen, HouseShift, Funnel, Holds;
        public readonly bool Derrick;
        public readonly string Sail, Head;
        public readonly bool SteeringOar, SternRudder;
        public readonly bool OpenBoat;
        public readonly int ThwartStep;
        public readonly int[] MastZs;

        public Top(StructureSpec spec, Form f)
        {
            MastCount = Clamp(spec.HullMastCount ?? 0, 0, 3);
            MastHeight = Clamp(spec.HullMastHeight ?? Math.Max(3, f.L / 2), 2, 64);

            // set=横帆 / fore=縦帆（ガフ帆）/ furled=畳む / none=なし。
            string sail = (spec.HullSail ?? "none").Trim().ToLowerInvariant();
            Sail = sail is "set" or "fore" or "furled" ? sail : "none";
            SailW = Clamp(spec.HullSailWidth ?? MastHeight, 2, 64);
            SailH = Clamp(spec.HullSailHeight ?? Math.Max(1, MastHeight - 1), 1, 64);

            ShieldPerSide = Clamp(spec.HullShieldPerSide ?? 0, 0, 32);
            SteeringOar = spec.HullSteeringOar ?? false;
            SternRudder = spec.HullSternRudder ?? false;

            // 砲門。段数だけ指定しても間隔が0なら開かない。間隔1では口が隣と
            // つながって切り欠きになるので2へ丸める。
            GunRows = Clamp(spec.HullGunRows ?? 0, 0, 4);
            int gs = spec.HullGunStep ?? 0;
            GunStep = gs <= 0 ? 0 : Clamp(gs, 2, 16);
            GunBase = Clamp(spec.HullGunBase ?? 1, 0, 8);

            // 櫂。舷の外へ出るので Extent の幅もこれを見る。
            // 実際の張り出し（OarSide）は BuildOars と同じ選び方・止め方で数える。
            OarPerSide = Clamp(spec.HullOarPerSide ?? 0, 0, 32);
            OarSide = OarReachOf(f, OarPerSide);

            // 開放艇と漕ぎ座。座は開放艇のときだけ置く（甲板が塞がっていれば座る場所が
            // ない）。間隔1では座が連なって甲板と見分けが付かないのでフレームと同じく
            // 2へ丸める。
            OpenBoat = spec.HullOpenBoat ?? false;
            int th = spec.HullThwartStep ?? 0;
            ThwartStep = OpenBoat && th > 0 ? Clamp(th, 2, 32) : 0;

            // デッキハウスと煙突。層数が0なら煙突も立てない（煙突は箱の屋根を
            // 基準に高さを取るので、箱が無いと基準が無い）。
            HouseDecks = Clamp(spec.HullHouseDecks ?? 0, 0, 8);
            HouseLen = Clamp(spec.HullHouseLength ?? 15, 5, 60);
            HouseShift = Clamp(spec.HullHouseShift ?? 0, -60, 60);
            Funnel = HouseDecks > 0 ? Clamp(spec.HullFunnel ?? 0, 0, 16) : 0;

            // 貨物艙口とデリック。
            Holds = Clamp(spec.HullHolds ?? 0, 0, 8);
            Derrick = spec.HullDerrick ?? false;

            // 貫通横梁の間隔。1マスおきでは外板と見分けが付かないので2へ丸める。
            int bs = spec.HullBeamStep ?? 0;
            BeamStep = bs <= 0 ? 0 : Clamp(bs, 2, 32);

            CastleAft = Clamp(spec.HullCastleAft ?? 0, 0, 16);
            CastleFore = Clamp(spec.HullCastleFore ?? 0, 0, 16);
            CastleLen = Math.Max(2,
                (int)Math.Round(f.L * Clamp(spec.HullCastleLength ?? 20, 5, 40) / 100.0));

            string head = (spec.HullStemHead ?? "none").Trim().ToLowerInvariant();
            Head = head == "spiral" || head == "dragon" ? head : "none";
            HeadHeight = Head == "dragon" ? 5 : Head == "spiral" ? 3 : 0;

            // マストの station と天端。天端はマスト・船首飾り・船楼・デッキハウスの
            // すべてを見るので、他のフィールドが埋まったあとに最後へ置く。
            MastZs = MastZsOf(f, MastCount, CastleAft, CastleFore, CastleLen);
            TopY = TopYOf(f, this);
        }
    }
}
