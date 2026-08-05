using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 開口部（ドア・窓・アーチ・大型シャッター）の適用と、非矩形平面向けのスナップ。
// 壁セルが存在する位置にだけ作用するので、角や柱を壊さない。
// StructureExpander の partial。
public static partial class StructureExpander
{
    private static void ApplyOpening(
        Dictionary<(int x, int y, int z), string> cells,
        Opening op, int w, int d, int h, IReadOnlyList<string> allowedBlocks)
    {
        string face = (op.Face ?? "").Trim().ToLowerInvariant();
        string kind = (op.Kind ?? "").Trim().ToLowerInvariant();
        bool isDoor = kind == "door";
        bool isArch = kind == "arch";
        bool isGate = kind == "gate";
        bool isWindow = !isDoor && !isArch && !isGate;

        int y = Clamp(op.Level, 1, Math.Max(1, h - 2)); // 中間層に収める
                                                        // 窓が床ぎわ(level=1)に張り付くのを防ぎ、壁の中ほどへ引き上げる。
                                                        // ドア・アーチは床から立てるので対象外。
        if (isWindow)
        {
            // 窓は床から最低 2 段上げる（見た目の要件: y>=2。床 y=0、壁下段 y=1 の上）。
            // ただし低い壁では上端(h-2)を超えないようクランプする。中段補正はしない
            //（要件は「最低 2」。高い建物でも y=2 の腰高で素直に付ける）。
            int minY = Clamp(2, 1, Math.Max(1, h - 2));
            if (y < minY) y = minY;
        }
        // アーチは床から立てる（door と同じ起点）。level 指定は無視して y=1 から。
        if (isArch) y = 1;

        // 面ごとに、面に沿った座標(offset)から壁上の1セルを特定する。
        // また、面に沿った「横方向」を表す軸（アーチを左右に広げる方向）も決める。
        // alongX=true なら offset は x 方向、false なら z 方向に沿う。
        //
        // 開口スナップ（非矩形フットプリント対応）:
        //   非矩形（L字・コの字・十字など）では、面の固定座標（例: south の z=d-1）に
        //   壁セルが無いことがある。その場合、offset の列を面の外側から内側へ走査し、
        //   最初に見つかった壁セルの位置へ寄せる。矩形なら1回目で当たるので従来と完全一致。
        //   列全体に壁が無ければ従来どおり無視される（後段の ContainsKey で弾かれる）。
        (int x, int z)? target2;
        bool alongX;
        switch (face)
        {
            case "north": target2 = SnapToWall(cells, Clamp(op.Offset, 0, w - 1), 0, false, +1, w, d); alongX = true; break;
            case "south": target2 = SnapToWall(cells, Clamp(op.Offset, 0, w - 1), d - 1, false, -1, w, d); alongX = true; break;
            case "west": target2 = SnapToWall(cells, 0, Clamp(op.Offset, 0, d - 1), true, +1, w, d); alongX = false; break;
            case "east": target2 = SnapToWall(cells, w - 1, Clamp(op.Offset, 0, d - 1), true, -1, w, d); alongX = false; break;
            default: return;
        }

        // 列全体に壁が無ければ寄せ先が無い＝開口しない（従来の無視挙動と同じ）。
        if (target2 == null) return;

        var key = (target2.Value.x, y, target2.Value.z);

        if (isArch)
        {
            // アーチ: 半円の頭を持つ開口を床から抜く。offset を中心に width マス、
            // 中央で height に達し、両端で起拱線(height - 半径)まで下がる。
            // width/height 未指定(0以下)なら幅3・高さ3で、旧実装（中央列を archTop まで、
            // 左右1列を archTop-1 まで抜く）と1マスも変わらない結果になる。
            //   幅3・高さ3 → 半径1・起拱線2 → 中央 top=3、左右 top=2（旧実装と一致）
            // 指定ありなら凱旋門・霊廟のような大型アーチになる。
            int wallTop = Math.Max(1, h - 2);
            int aw = op.Width > 0 ? op.Width : 3;
            int ah = op.Height > 0 ? op.Height : 3;
            if (aw % 2 == 0) aw--;              // 中心を1マスに保つため奇数へ寄せる
            if (aw < 1) aw = 1;
            ah = Math.Min(ah, wallTop);         // 壁の高さ（屋根の手前 h-2）に収める
            int r = (aw - 1) / 2;               // 半円の半径＝幅の半分
            int springY = Math.Max(1, ah - r);  // 起拱線。ここから上が半円になる。

            int cx = target2.Value.x, cz = target2.Value.z;
            for (int s = -r; s <= r; s++)
            {
                int sx = alongX ? cx + s : cx;
                int sz = alongX ? cz : cz + s;
                // 開口が壁の外周面からはみ出さないよう、その面上の有効範囲かを確認する。
                if (sx < 0 || sx >= w || sz < 0 || sz >= d) continue;

                int top = springY + (int)Math.Round(Math.Sqrt(Math.Max(0, r * r - s * s)));
                if (top > wallTop) top = wallTop;
                for (int yy = 1; yy <= top; yy++)
                    cells.Remove((sx, yy, sz));
            }
            return;
        }

        if (isGate)
        {
            // 大型シャッター/搬入口: offset を中心に width×height の矩形を床から抜く。
            // 工場・倉庫・格納庫の間口。壁セルのある位置だけを抜くので角は壊れない。
            int gw = op.Width > 0 ? op.Width : 3;
            int gh = op.Height > 0 ? op.Height : 3;
            gh = Math.Min(gh, Math.Max(1, h - 2));

            int gx = target2.Value.x, gz = target2.Value.z;
            int leftSide = (gw - 1) / 2;
            for (int s = -leftSide; s <= gw - 1 - leftSide; s++)
            {
                int sx = alongX ? gx + s : gx;
                int sz = alongX ? gz : gz + s;
                if (sx < 0 || sx >= w || sz < 0 || sz >= d) continue;
                for (int yy = 1; yy <= gh; yy++)
                    cells.Remove((sx, yy, sz));
            }
            return;
        }

        // 壁セルでなければ無視（角や非外周を壊さない）
        if (!cells.ContainsKey(key)) return;

        if (isDoor)
        {
            cells.Remove(key); // ドア下段
                               // ドアは縦2マス。1つ上の段も同じ面・同じ位置を開ける（壁セルのときのみ）。
            var upper = (target2.Value.x, y + 1, target2.Value.z);
            if (cells.ContainsKey(upper)) cells.Remove(upper);
        }
        else
        {
            string glass = Pick(op.Block ?? "minecraft:glass", allowedBlocks, "minecraft:glass");
            cells[key] = glass; // 窓=ガラス置換
        }
    }

    // 開口スナップ用。面上の固定座標(fixedCoord)から、指定 offset の列を面の内側へ
    // step 方向に走査し、最初に壁セル（いずれかの y に cells が存在する x,z）を持つ
    // 位置を返す。alongZ=true なら offset は z、走査は x 方向; false なら offset は x、
    // 走査は z 方向。列全体に壁が無ければ null（＝開口しない）。
    //   引数の意味:
    //     alongZ=false（north/south）… offsetX 固定、z を fixedCoord から step 方向へ走査
    //     alongZ=true （east/west）  … offsetZ 固定、x を fixedCoord から step 方向へ走査
    private static (int x, int z)? SnapToWall(
        Dictionary<(int x, int y, int z), string> cells,
        int a, int b, bool alongZ, int step, int w, int d)
    {
        // north/south: a=offsetX(固定), b=面のz(=0 or d-1), 走査は z 方向
        // east/west  : a=面のx(=0 or w-1), b=offsetZ(固定), 走査は x 方向
        if (!alongZ)
        {
            int x = a;
            for (int z = b; z >= 0 && z < d; z += step)
                if (HasWallColumn(cells, x, z)) return (x, z);
        }
        else
        {
            int z = b;
            for (int x = a; x >= 0 && x < w; x += step)
                if (HasWallColumn(cells, x, z)) return (x, z);
        }
        return null;
    }

    // (x,z) の柱にいずれかの高さ(y>=1)で壁セルが存在するか。
    // 床(y=0)や屋根だけの位置を壁と誤認しないよう、y>=1 のセルの有無で判定する。
    private static bool HasWallColumn(
        Dictionary<(int x, int y, int z), string> cells, int x, int z)
    {
        foreach (var k in cells.Keys)
            if (k.x == x && k.z == z && k.y >= 1)
                return true;
        return false;
    }
}
