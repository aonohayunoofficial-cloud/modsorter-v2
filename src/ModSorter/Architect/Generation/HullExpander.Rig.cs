using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 艤装の組み立て。マスト・帆桁・帆・盾掛け・側舵・船首材の飾り。
// 貫通横梁・中心線舵・船楼は HullExpander.Castle.cs にある。
//
// 実物の根拠（ロングシップ / ゴクスタ船）:
//   全長23.22m・幅5.18m、外板16列、片舷16の櫂穴で盾は32枚。
//   マストは船体中央の帆柱受けに立ち、高さは11〜13m と推定される。
//   帆は1枚の横帆で面積110m²級（幅11m×丈10m 相当）。
//   舵は船尾の片舷に吊る舵（クォーターラダー）で、舵柄が船内へ伸びる。
//   舷墻は持たず、最上列の外板が舷縁を兼ねる。盾は舷縁の外側へ掛ける。
//   竜頭は1マス未満の彫刻なので、頭部の高さだけで表す。
public static partial class HullExpander
{
    private static void BuildTopside(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, StructureSpec spec, TopPalette t)
    {
        var top = new Top(spec, f);

        BuildRig(cells, props, f, top, t);
        BuildShields(cells, props, f, top, t);
        BuildQuarterRudder(cells, f, top, t);
        BuildBeams(cells, f, top, t);
        BuildSternRudder(cells, f, top, t);
        BuildCastles(cells, f, top, t);
        BuildHeads(cells, f, top, t);
    }

    // マスト・帆桁・帆。帆桁と帆を先に置き、最後にマストの列で上書きする。
    // 実船でも帆はマストの後ろを通るので、交差はマストが勝つのが正しい。
    private static void BuildRig(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t)
    {
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;

        foreach (int z in top.MastZs)
        {
            int baseY = f.DeckY(z) + 1;
            int mastTop = baseY + top.MastHeight - 1;

            if (top.Sail != "none")
            {
                int yardY = Math.Max(baseY, mastTop - 1);
                int xa = cx0 - (top.SailW - 1) / 2;
                int xb = xa + top.SailW - 1;

                // 帆桁。横に寝た丸太なので axis=x を持たせる。回転で axis も回る。
                for (int x = xa; x <= xb; x++)
                {
                    var key = (x, yardY, z);
                    cells[key] = t.Mast;
                    props[key] = new Dictionary<string, string> { ["axis"] = "x" };
                }

                int sailTop = yardY - 1;
                int rows = top.Sail == "furled" ? 1 : top.SailH;
                for (int k = 0; k < rows; k++)
                {
                    int y = sailTop - k;
                    if (y < baseY) break;
                    for (int x = xa; x <= xb; x++) cells[(x, y, z)] = t.Sail;
                }
            }

            for (int y = baseY; y <= mastTop; y++)
                for (int x = cx0; x <= cx1; x++)
                {
                    cells[(x, y, z)] = t.Mast;
                    props.Remove((x, y, z));   // 帆桁の axis が縦のマストに残らないようにする
                }
        }
    }

    // 盾掛け（skjaldrim）。舷縁（舷墻があればその天端）の外側へ、薄板を舷側の面へ
    // 張り付けて並べる。実物の盾は直径94cm・厚さ2〜3cmの板で、互いに半分ずつ重ねて
    // 櫂穴を覆うので、横から見れば切れ目のない帯になる。フルブロックだと厚さ1mの
    // 張り出しになって船体が太るため、トラップドアを開いた状態（厚さ3/16マスの
    // 垂直板）で張る。ゴクスタ船の盾は黄と黒の交互なので、2種を1枚おきに使う。
    //
    // 盾を掛ける相手が要る。舷墻は幅3マス以上の station にしか立たないので、
    // 舷墻があるときは同じ条件に揃える。舷墻なしなら舷縁（甲板の縁）が相手になる。
    private static void BuildShields(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t)
    {
        if (top.ShieldPerSide <= 0) return;

        int span = f.Bulwark > 0 ? 2 : 1;
        int z0 = Math.Max(1, f.L / 2 - top.ShieldPerSide / 2);
        for (int i = 0; i < top.ShieldPerSide; i++)
        {
            int z = z0 + i;
            if (z >= f.L - 1) break;

            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            if (x1 - x0 < span) continue;   // 掛ける相手がないところには掛けない

            int y = dk + f.Bulwark;
            string id = (i & 1) == 0 ? t.Shield : t.ShieldAlt;

            // 開いたトラップドアの板は facing の「反対」側の面に立つ
            // （facing は開く向きで、蝶番は反対側。バニラの blockstates で
            //  facing=north,open=true は y 回転なし、開いた模型の要素は z=13〜16=南面）。
            // 左舷セルは船体が +x 側にあるので facing=west で板が東面＝船体に密着し、
            // 右舷セルは facing=east で板が西面に立つ。回転では RotateFacing が facing を回す。
            PutShield(cells, props, (x0 - 1, y, z), id, "west");
            PutShield(cells, props, (x1 + 1, y, z), id, "east");
        }
    }

    // クォーターラダーと舵柄。船尾の片舷（+x 側）に吊る。
    // facade_face の回転で舷は入れ替わる。
    private static void BuildQuarterRudder(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (!top.SteeringOar) return;

        int z = Math.Max(1, f.L / 8);
        int dk = f.DeckY(z);
        f.Span(f.HalfAt(z, dk), out int x0, out int x1);

        for (int k = 0; k < 3; k++)
        {
            int y = dk - k;
            if (y < 0) break;
            cells[(x1 + 1, y, z)] = t.Fitting;
        }
        cells[(x1, dk + 1 + f.Bulwark, z)] = t.Fitting;   // 舷内へ伸びる舵柄
    }

    // 船首材・船尾材の飾り。z 方向へは出さず高さだけで表す。
    private static void BuildHeads(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.HeadHeight <= 0) return;
        PutHead(cells, f, t, top, f.L - 1);
        PutHead(cells, f, t, top, 0);
    }

    private static void PutHead(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, TopPalette t, Top top, int z)
    {
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int baseY = f.DeckY(z) + 1;
        for (int k = 0; k < top.HeadHeight; k++)
        {
            // 上へ行くほど1列に絞る。渦巻きも竜頭も先端は細い。
            int a = k >= top.HeadHeight - 1 ? cx0 : cx0;
            int b = k >= top.HeadHeight - 1 ? cx0 : cx1;
            for (int x = a; x <= b; x++) cells[(x, baseY + k, z)] = t.Fitting;
        }
    }

    // 盾1枚。トラップドアなら垂直の薄板として舷側の面へ張る。
    // それ以外のブロックが選ばれたときはブロックステートを付けずに置く
    // （持たない状態を書くと不正な blockstate になる）。
    private static void PutShield(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        (int x, int y, int z) key, string id, string facing)
    {
        cells[key] = id;
        if (!id.EndsWith("_trapdoor", StringComparison.OrdinalIgnoreCase))
        {
            props.Remove(key);
            return;
        }

        props[key] = new Dictionary<string, string>
        {
            ["facing"] = facing,
            ["half"] = "bottom",
            ["open"] = "true",
            ["powered"] = "false",
            ["waterlogged"] = "false",
        };
    }

    // 横に寝たブロックの axis。x と z が入れ替わり、y は変わらない。
    private static string RotateAxis(string axis, int turns)
    {
        if ((turns & 1) == 0) return axis;
        return axis switch { "x" => "z", "z" => "x", _ => axis };
    }
}
