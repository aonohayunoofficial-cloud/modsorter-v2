using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋根（採光形）: 鋸屋根と越屋根。工場・倉庫の採光が目的なので、
// 垂直面を glazing（ガラス）で張るところが他の屋根と決定的に違う。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // 鋸屋根: 棟と直交する方向に山を並べ、各山は「登り勾配 → 垂直な採光面」で構成する。
    // 屋根面は棟方向の全長に渡って必ず埋め、段差の縦面も塞ぐので山の間が空くことはない。
    // 各山の高い側の列を垂直に塞ぎ、そこを採光面(glazing)にする。実物の鋸屋根も
    // この垂直面が全面ガラス（北面採光）で、屋根形式の存在理由そのもの。
    // 棟に直交する両端（妻側）は、その列の屋根高さまで壁で立ち上げる。
    private static void BuildSawtoothRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h,
        string roof, string wall, string glazing)
    {
        // 棟がx軸に平行（既定）なら山はz方向に並ぶ。"z" 指定ならx方向に並ぶ。
        bool alongX = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant() != "z";
        int alongLen = alongX ? w : d;   // 棟に平行な方向の長さ
        int acrossLen = alongX ? d : w;  // 山が並ぶ方向の長さ

        // (棟方向 a, 山方向 c, 高さ y) を実座標へ。
        void Set(int a, int c, int y, string id)
        {
            if (alongX) cells[(a, y, c)] = id;
            else cells[(c, y, a)] = id;
        }

        int baseY = h - 1; // 壁の最上層と同じ高さから積む（他の屋根と同じ起点）

        // 勾配。未指定は2（工場の緩勾配）。1〜4にクランプ。
        int pitchRaw = spec.RoofPitch ?? 0;
        int pitch = Clamp(pitchRaw <= 0 ? 2 : pitchRaw, 1, 4);

        // 1山あたり最低3マス。未指定なら入るだけ並べる。
        int maxBays = Math.Max(1, acrossLen / 3);
        int baysRaw = spec.SawtoothBays ?? 0;
        int bays = Clamp(baysRaw <= 0 ? maxBays : baysRaw, 1, maxBays);

        // 割り切れない余りは手前の山から1マスずつ配り、全長を必ず覆う。
        int baseW = acrossLen / bays;
        int extra = acrossLen % bays;

        int c0 = 0;
        for (int b = 0; b < bays; b++)
        {
            int bw = baseW + (b < extra ? 1 : 0);
            int cEnd = c0 + bw - 1;
            int topY = baseY + (bw - 1) / pitch;

            int prevY = -1;
            for (int c = c0; c <= cEnd; c++)
            {
                int y = baseY + (c - c0) / pitch;

                // 前の列の高さから今の列の高さまで縦に埋め、斜めの隙間を塞ぐ。
                int from = (prevY < 0) ? y : prevY;
                for (int yy = from; yy <= y; yy++)
                    for (int a = 0; a < alongLen; a++)
                        Set(a, c, yy, roof);
                prevY = y;

                // 妻壁（棟に直交する両端）を、この列の屋根の手前まで立ち上げる。
                for (int yy = baseY; yy < y; yy++)
                {
                    Set(0, c, yy, wall);
                    Set(alongLen - 1, c, yy, wall);
                }
            }

            // 立ち上がり（採光面）。山の頂点の列を垂直に塞ぎ、そこで採光する。
            // 塞ぐ範囲は baseY から topY-1 まで。頂点(topY)は上の屋根面が既に載っている。
            // baseY+1 から始めると壁上端と同じ高さの1段だけが残り、
            // 山の中央部に横一列の穴（内部が見える隙間）ができるので baseY から塞ぐ。
            if (topY > baseY)
            {
                for (int yy = baseY; yy < topY; yy++)
                {
                    for (int a = 0; a < alongLen; a++)
                        Set(a, cEnd, yy, glazing);
                    Set(0, cEnd, yy, wall);
                    Set(alongLen - 1, cEnd, yy, wall);
                }
            }

            c0 = cEnd + 1;
        }
    }

    // 越屋根（モニター屋根）: 全面の平屋根の中央に、側面をガラスにした一段高い屋根を載せる。
    // 越屋根の内側は下屋根を抜いて吹き抜けにし、上から採光が落ちるようにする。
    // 吹き抜けを抜くのは棟方向の内側だけ。両端(a=0 / a=alongLen-1)の列まで抜くと、
    // 妻側の外皮が壁上端の高さ(baseY)で横一列に欠け、そこから外に内部が見えてしまう。
    // 妻壁の立ち上がりは baseY+1 からなので、baseY の1列は必ず塞いだまま残す。
    // 幅5マス未満・棟方向3マス未満・立ち上がり2マス未満では側面が張れないので平屋根のまま返す。
    private static void BuildMonitorRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h,
        string roof, string wall, string glazing)
    {
        bool alongX = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant() != "z";
        int alongLen = alongX ? w : d;
        int acrossLen = alongX ? d : w;

        (int x, int y, int z) Key(int a, int c, int y) => alongX ? (a, y, c) : (c, y, a);
        void Set(int a, int c, int y, string id) => cells[Key(a, c, y)] = id;

        int baseY = h - 1;

        // 下屋根は全面。
        for (int a = 0; a < alongLen; a++)
            for (int c = 0; c < acrossLen; c++)
                Set(a, c, baseY, roof);

        if (acrossLen < 5 || alongLen < 3) return;

        int mwRaw = spec.MonitorWidth ?? 0;
        int mw = Clamp(mwRaw <= 0 ? Math.Max(3, acrossLen / 3) : mwRaw, 3, acrossLen - 2);
        int mhRaw = spec.MonitorHeight ?? 0;
        int mh = Clamp(mhRaw <= 0 ? 3 : mhRaw, 1, 16);
        if (mh < 2) return; // 側面が張れないので平屋根のまま

        int s0 = (acrossLen - mw) / 2;
        int s1 = s0 + mw - 1;

        // 越屋根の内側を吹き抜けにする。両側の桁(s0/s1)と、妻側の外皮になる
        // 棟方向の両端(a=0 / a=alongLen-1)は残し、外から内部が見えないようにする。
        for (int a = 1; a < alongLen - 1; a++)
            for (int c = s0 + 1; c <= s1 - 1; c++)
                cells.Remove(Key(a, c, baseY));

        // 妻側の外皮。吹き抜けの縁になる baseY の列を壁で固め、下屋根との取り付きを塞ぐ。
        for (int c = s0; c <= s1; c++)
        {
            Set(0, c, baseY, wall);
            Set(alongLen - 1, c, baseY, wall);
        }

        // 側面（採光ガラス）と妻側（壁）。
        for (int yy = baseY + 1; yy <= baseY + mh - 1; yy++)
        {
            for (int a = 0; a < alongLen; a++)
            {
                Set(a, s0, yy, glazing);
                Set(a, s1, yy, glazing);
            }
            for (int c = s0; c <= s1; c++)
            {
                Set(0, c, yy, wall);
                Set(alongLen - 1, c, yy, wall);
            }
        }

        // 越屋根の頂部。
        for (int a = 0; a < alongLen; a++)
            for (int c = s0; c <= s1; c++)
                Set(a, c, baseY + mh, roof);
    }
}
