using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 上部構造の素材と寸法。組み立ては次の2ファイルへ分ける。
//   HullExpander.Rig.cs    … マスト・帆桁・帆・盾掛け・側舵・船首材の飾り
//   HullExpander.Castle.cs … 貫通横梁・中心線舵・船楼
// 1ファイル9KB以下の目安に収めるための分割。船種が増えて部品が増えても、
// 寸法の解決はこの Top に集まるので Extent と組み立てで値が食い違わない。
public static partial class HullExpander
{
    private sealed class TopPalette
    {
        public readonly string Mast, Sail, Shield, ShieldAlt, Fitting, Castle;

        public TopPalette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Mast = Pick(spec.SuperstructureBlock, allowed, fallback);
            Sail = Pick(spec.RoofBlock, allowed, Mast);
            Shield = Pick(spec.TowerBlock, allowed, Mast);
            ShieldAlt = Pick(spec.HullShieldBlockAlt ?? spec.TowerBlock, allowed, Shield);
            Fitting = Pick(spec.SeatBlock, allowed, Mast);
            Castle = Pick(spec.HullCastleBlock ?? spec.SuperstructureBlock, allowed, Mast);
        }
    }

    // 上部構造の寸法。Extent と BuildTopside の両方がこれを通るので、
    // UI に出す外寸と生成物の外寸が食い違わない。
    private sealed class Top
    {
        public readonly int MastCount, MastHeight, SailW, SailH, ShieldPerSide, HeadHeight, TopY;
        public readonly int BeamStep, CastleAft, CastleFore, CastleLen;
        public readonly string Sail, Head;
        public readonly bool SteeringOar, SternRudder;
        public readonly int[] MastZs;

        public Top(StructureSpec spec, Form f)
        {
            MastCount = Clamp(spec.HullMastCount ?? 0, 0, 3);
            MastHeight = Clamp(spec.HullMastHeight ?? Math.Max(3, f.L / 2), 2, 64);

            string sail = (spec.HullSail ?? "none").Trim().ToLowerInvariant();
            Sail = sail == "set" || sail == "furled" ? sail : "none";
            SailW = Clamp(spec.HullSailWidth ?? MastHeight, 2, 64);
            SailH = Clamp(spec.HullSailHeight ?? Math.Max(1, MastHeight - 1), 1, 64);

            ShieldPerSide = Clamp(spec.HullShieldPerSide ?? 0, 0, 32);
            SteeringOar = spec.HullSteeringOar ?? false;
            SternRudder = spec.HullSternRudder ?? false;

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

            MastZs = new int[MastCount];
            int top = 0;
            for (int i = 0; i < MastCount; i++)
            {
                int z = (int)Math.Round(f.L * (i + 1.0) / (MastCount + 1.0));
                MastZs[i] = Math.Clamp(z, 1, Math.Max(1, f.L - 2));
                int y = f.DeckY(MastZs[i]) + MastHeight;
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
            TopY = top;
        }
    }
}
