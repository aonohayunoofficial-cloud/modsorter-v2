using System;

namespace ModSorter.Architect.Generation;

// 船体の外寸。HullExpander.cs が12,628バイトになったので分けた。
//
// UI（HullParamsControl.BuildSpec）と展開側の両方がここを通る。桁橋で起きた
// 「UIと展開側で式が二重になる」状態を作らないため、外寸の式はこの1か所に置く。
public static partial class HullExpander
{
    // UI が Width / Height を先に出すために使う。展開側と同じ Form を通すので、
    // スライダーの表示値と生成物の外寸が食い違わない。
    // 返す値は canonical（船首 +z）での外寸。facade_face が east / west のときは
    // 呼び側で Width と Depth を入れ替える。
    public static (int Width, int Depth, int Height) Extent(StructureSpec spec)
    {
        var f = new Form(spec);
        var (w, h) = f.Bounds();
        var t = new Top(spec, f);
        var s = new ScrewFit(spec, f);

        // 舷の外へ出る部品のうち、いちばん遠くまで出るものを左右へ足す。
        // 盾掛けと貫通横梁の木口は1マス、櫂は Top.OarSide マス（最大3）。
        // 櫂は水面の手前で止まるので、乾舷が1マスの端艇では1マスしか出ない。
        // 一律3マスにすると外寸だけが太るため、Top が数えた実際の張り出しを使う。
        // マスト・船楼・船首材の飾りは甲板より上へ伸びるので、竜骨の張り出しぶんを足して比べる。
        // 開放艇の床板・漕ぎ座は舷縁の内側なので外寸には効かない。
        // プロペラは型幅の内側にしか置かないので幅には効かない。
        int side = Math.Max(
            t.OarSide,
            t.ShieldPerSide > 0 || t.BeamStep >= 2 ? 1 : 0);
        int width = w + side * 2;

        // 船尾材より後ろへ出るのは中心線舵（1マス）とスクリューの舵（舵の前後長）。
        // どちらも z=-1 から後ろへ伸びるので、遠いほうだけを数える。
        // 両方を同時には置かない（ScrewFit.Rudder が中心線舵を見て降りる）。
        int aft = Math.Max(t.SternRudder ? 1 : 0, s.Rudder ? s.Chord : 0);
        int depth = f.L + aft;

        // プロペラは船体の底より下へ吊ることがある（滑走艇は船底から下へ出す）。
        // 竜骨の張り出しより深いぶんだけ高さが増える。径はアパーチャ＋1マスに
        // 収めてあるので、増えるのは最大1マス。
        int below = Math.Max(0, -s.PropBottom - f.KeelDepth);
        int height = Math.Max(h, t.TopY + f.KeelDepth + 1) + below;
        return (width, depth, height);
    }
}
