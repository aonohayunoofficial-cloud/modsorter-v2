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
    private sealed class Top
    {
        public readonly int MastCount, MastHeight, SailW, SailH, ShieldPerSide, HeadHeight, TopY;
        public readonly int BeamStep, CastleAft, CastleFore, CastleLen;
        public readonly int GunRows, GunStep, GunBase, OarPerSide;
        public readonly int HouseDecks, HouseLen, HouseShift, Funnel, Holds;
        public readonly bool Derrick;
        public readonly string Sail, Head;
        public readonly bool SteeringOar, SternRudder;
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

            // 櫂。舷の外へ3マス出るので Extent の幅もこれを見る。
            OarPerSide = Clamp(spec.HullOarPerSide ?? 0, 0, 32);

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

            // マストを立てられる station の範囲。船楼の占める範囲（船尾なら
            // z = 0〜CastleLen-1）と、その内側の妻面のすぐ前を外す。妻面には
            // 出入口が開くので、その正面に柱が立つと戸口が塞がる。実船でも
            // マストは隔壁と戸口を避けて竜骨の上へ据えるので、避けるのが正しい。
            int lo = CastleAft > 0 ? CastleLen + 1 : 1;
            int hi = CastleFore > 0 ? f.L - CastleLen - 2 : f.L - 2;
            lo = Math.Clamp(lo, 1, Math.Max(1, f.L - 2));
            hi = Math.Clamp(hi, 1, Math.Max(1, f.L - 2));
            // 前後の船楼で船体が埋まる小舟は避けようがないので、従来どおり全長へ戻す。
            if (hi < lo) { lo = 1; hi = Math.Max(1, f.L - 2); }

            MastZs = new int[MastCount];
            int top = 0;
            int prev = int.MinValue;
            for (int i = 0; i < MastCount; i++)
            {
                int z = (int)Math.Round(f.L * (i + 1.0) / (MastCount + 1.0));
                z = Math.Clamp(z, lo, hi);
                // 範囲へ詰めた結果2本が同じ station へ重なると1本ぶん消えるので、
                // 前のマストより後ろへ1マスずつ送る。
                if (z <= prev) z = Math.Min(prev + 1, hi);
                prev = z;
                MastZs[i] = z;
                int y = f.DeckY(z) + MastHeight;
                if (y > top) top = y;
            }

            if (HeadHeight > 0)
            {
                int y = Math.Max(f.DeckY(0), f.DeckY(f.L - 1)) + HeadHeight;
                if (y > top) top = y;
            }

            // 船楼は船体中央を向く端の甲板から高さを取り、その上に手すりが1マス載る。
            // CastleFloorY と同じ式を通すので、外寸と生成物が食い違わない。
            if (CastleAft > 0)
            {
                int zi = Math.Min(CastleLen - 1, f.L - 1);
                int y = CastleFloorY(f, zi, CastleAft) + 1;
                if (y > top) top = y;
            }
            if (CastleFore > 0)
            {
                int zi = Math.Max(f.L - CastleLen, 0);
                int y = CastleFloorY(f, zi, CastleFore) + 1;
                if (y > top) top = y;
            }

            // デッキハウスと煙突の天端。BuildDeckHouse と同じ式（甲板の最大＋1を
            // 下端、1層3マス）を通すので、UI の外寸と生成物が食い違わない。
            if (HouseDecks > 0)
            {
                int len = Math.Max(3, f.L * HouseLen / 100);
                int z0 = Math.Max(1, (f.L - len) / 2 + HouseShift);
                int z1 = Math.Min(f.L - 2, z0 + len - 1);
                int baseY = f.DeckY(Math.Max(0, z0)) + 1;
                for (int z = Math.Max(0, z0); z <= Math.Max(0, z1); z++)
                    baseY = Math.Max(baseY, f.DeckY(z) + 1);

                int y = baseY + HouseDecks * 3 - 1 + Funnel;
                if (y > top) top = y;
            }

            TopY = top;
        }
    }
}
