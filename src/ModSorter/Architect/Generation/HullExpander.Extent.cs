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

        // 舷の外へ出る部品のうち、いちばん遠くまで出るものを左右へ足す。
        // 盾掛けと貫通横梁の木口は1マス、櫂は Top.OarSide マス（最大3）。
        // 櫂は水面の手前で止まるので、乾舷が1マスの端艇では1マスしか出ない。
        // 一律3マスにすると外寸だけが太るため、Top が数えた実際の張り出しを使う。
        // 中心線舵は船尾材の後ろへ1マス出るので奥行きが1増える。
        // マスト・船楼・船首材の飾りは甲板より上へ伸びるので、竜骨の張り出しぶんを足して比べる。
        // 開放艇の床板・漕ぎ座は舷縁の内側なので外寸には効かない。
        int side = Math.Max(
            t.OarSide,
            t.ShieldPerSide > 0 || t.BeamStep >= 2 ? 1 : 0);
        int width = w + side * 2;
        int depth = f.L + (t.SternRudder ? 1 : 0);
        int height = Math.Max(h, t.TopY + f.KeelDepth + 1);
        return (width, depth, height);
    }
}
