using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 桁橋（structure_type="bridge:girder_bridge"）。BridgeExpander の partial。
//
// 断面は x 方向、橋が渡る向きは z 方向。y の積み方は下から順に、
//   0 .. soffit-1   … 橋脚（桁下の空間）
//   soffit          … 主桁の下フランジ・横桁
//   soffit .. deckY-1 … 主桁のウェブ（桁高ぶん）
//   deckY           … 床版
//   surfY = deckY+1 … 車道の舗装面・地覆の下端
//   walkY = surfY+1 … 歩道面・地覆の天端
//   その上          … 高欄、さらに上に照明柱と灯具
//
// 横断方向は「外側線→車線→車線境界線→…→中央分離帯→…→外側線」の順に左から詰める。
// 区画線に専用の1マス列を与えるので、線の有無で車線幅が変わらず、左右も必ず対称になる。
//
// 丸めの扱い。床版の実寸は 0.22m、舗装は 0.08m で合わせても 0.3m しかないが、
// 1マス=1m では床版と路面を別の層に分けないと主桁と路面の境が読めない。
// よって床版1層＋路面1層の計2マスを充てる。歩道の段差（実寸 0.15〜0.25m）も
// 同じ理由で1マスとする。区画線（実寸 0.15m）も1マス列を占める。
// いずれも実寸より厚い・広い方向の丸め。
public static partial class BridgeExpander
{
    private static void BuildGirder(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        // ===== 支間割 =====
        int spans = Clamp(spec.BridgeSpans ?? 3, 1, 10);
        int span = Clamp(spec.BridgeSpan ?? 30, 8, 80);
        bool cont = spec.BridgeContinuous;
        int sideRatio = Clamp(spec.BridgeSideRatio ?? 80, 50, 100);

        // 連続桁で3径間以上のときだけ側径間を詰める。1:1.25（側径間80%）が標準。
        var lens = new List<int>();
        for (int i = 0; i < spans; i++)
        {
            int len = span;
            if (cont && spans >= 3 && (i == 0 || i == spans - 1))
                len = Math.Max(6, span * sideRatio / 100);
            lens.Add(len);
        }

        // 支点の z 座標。sup[0]=0、sup[spans]=橋長。
        var sup = new List<int> { 0 };
        foreach (int len in lens) sup.Add(sup[sup.Count - 1] + len);
        int total = sup[sup.Count - 1];

        // ===== 横断構成 =====
        int lanes = Clamp(spec.BridgeLanes ?? 2, 1, 6);
        int laneW = Clamp(spec.BridgeLaneWidth ?? 3, 3, 4);
        int median = Clamp(spec.BridgeMedian ?? 0, 0, 6);
        int walk = Clamp(spec.BridgeSidewalk ?? 2, 0, 6);
        int rail = Clamp(spec.BridgeRailing ?? 1, 0, 3);
        bool marks = spec.BridgeLaneMark;
        int markW = marks ? 1 : 0;

        // 1車線しかない橋に中央分離帯は入らない。
        if (lanes < 2) median = 0;

        // 上下線の車線数。中央分離帯があるときだけ分ける。奇数なら左側が1本多い。
        int lanesL = median > 0 ? (lanes + 1) / 2 : lanes;
        int lanesR = median > 0 ? lanes - lanesL : 0;

        // 1方向の車道幅。車線と車線境界線を交互に並べた合計。
        int Carriage(int n) => n <= 0 ? 0 : n * laneW + (n - 1) * markW;

        int roadW = markW
                  + (median > 0 ? Carriage(lanesL) + median + Carriage(lanesR) : Carriage(lanes))
                  + markW;

        int edge = walk > 0 ? walk + 1 : 1;   // 地覆1マス＋歩道
        int deckW = roadW + edge * 2;
        int roadX0 = edge;
        int roadX1 = roadX0 + roadW - 1;

        // ===== 車線・中央分離帯・区画線の割り付け =====
        // 左端の外側線の次から車線を並べ、車線と車線の間に境界線の列を挟む。
        // 中央分離帯は上り線を並べ終えた位置に入る。
        var boundaries = new List<int>();
        int medianX0 = -1;
        int cx = roadX0 + markW;
        for (int side = 0; side < (median > 0 ? 2 : 1); side++)
        {
            int n = side == 0 ? (median > 0 ? lanesL : lanes) : lanesR;
            for (int i = 0; i < n; i++)
            {
                cx += laneW;
                if (i < n - 1)
                {
                    if (markW > 0) boundaries.Add(cx);
                    cx += markW;
                }
            }
            if (side == 0 && median > 0)
            {
                medianX0 = cx;
                cx += median;
            }
        }

        // ===== 高さ =====
        int depthRatio = Clamp(spec.BridgeDepthRatio ?? 20, 12, 30);
        int maxLen = lens.Max();
        int girderH = (int)Math.Round(maxLen / (double)depthRatio);
        if (cont) girderH = girderH * 4 / 5;   // 連続桁は桁高を落とせる
        girderH = Clamp(girderH, 1, 8);

        int pierH = Clamp(spec.BridgePierHeight ?? 8, 1, 40);
        int soffit = pierH;
        int deckY = soffit + girderH;
        int surfY = deckY + 1;
        int walkY = surfY + 1;
        int topY = walk > 0 ? walkY : surfY;   // 高欄が載る高さ

        // ===== 主桁の割り付け =====
        // 未指定ならおよそ3m間隔。両端は主桁間隔の半分だけ張り出す（オーバーハング）。
        int girders = spec.BridgeGirders ?? 0;
        if (girders <= 0) girders = (int)Math.Round(deckW / 3.0);
        girders = Clamp(girders, 2, Math.Max(2, deckW / 2));

        var gx = new List<int>();
        for (int i = 0; i < girders; i++)
            gx.Add(Clamp((int)Math.Round((deckW - 1.0) * (i + 0.5) / girders), 0, deckW - 1));
        gx = gx.Distinct().OrderBy(v => v).ToList();

        // 単純桁は支点ごとに主桁を切る。切った1マスが遊間（伸縮装置）になる。
        var joints = new HashSet<int>();
        if (!cont)
            for (int i = 1; i < sup.Count - 1; i++) joints.Add(sup[i]);

        // ===== 主桁 =====
        foreach (int x in gx)
            for (int z = 0; z < total; z++)
            {
                if (joints.Contains(z)) continue;
                Fill(cells, x, x, soffit, deckY - 1, z, z, p.Girder);
            }

        // ===== 横桁（対傾構）=====
        int crossStep = Clamp(spec.BridgeCrossStep ?? 6, 0, 24);
        if (crossStep > 0 && gx.Count >= 2)
            for (int z = 0; z < total; z += crossStep)
            {
                if (joints.Contains(z)) continue;
                Fill(cells, gx[0], gx[gx.Count - 1], soffit, soffit, z, z, p.Girder);
            }

        // ===== 床版と車道 =====
        Fill(cells, 0, deckW - 1, deckY, deckY, 0, total - 1, p.Deck);
        Fill(cells, roadX0, roadX1, surfY, surfY, 0, total - 1, p.Pave);

        // ===== 中央分離帯 =====
        // 路面から1マス立ち上げる。実物の分離帯（防護柵つき）に相当。
        if (median > 0)
            Fill(cells, medianX0, medianX0 + median - 1, surfY, surfY + 1, 0, total - 1, p.Curb);

        // ===== 区画線 =====
        // 車道外側線は実線、車線境界線は実線長5m・空白長5mの破線。
        if (marks)
        {
            for (int z = 0; z < total; z++)
            {
                cells[(roadX0, surfY, z)] = p.Mark;
                cells[(roadX1, surfY, z)] = p.Mark;
            }

            foreach (int bx in boundaries)
                for (int z = 0; z < total; z++)
                    if (z % 10 < 5) cells[(bx, surfY, z)] = p.Mark;
        }

        // ===== 地覆と歩道 =====
        if (walk > 0)
        {
            int leftCurb = roadX0 - 1;
            int rightCurb = roadX1 + 1;

            Fill(cells, leftCurb, leftCurb, surfY, walkY, 0, total - 1, p.Curb);
            Fill(cells, rightCurb, rightCurb, surfY, walkY, 0, total - 1, p.Curb);

            Fill(cells, 0, leftCurb - 1, surfY, walkY - 1, 0, total - 1, p.Curb);
            Fill(cells, 0, leftCurb - 1, walkY, walkY, 0, total - 1, p.Walk);
            Fill(cells, rightCurb + 1, deckW - 1, surfY, walkY - 1, 0, total - 1, p.Curb);
            Fill(cells, rightCurb + 1, deckW - 1, walkY, walkY, 0, total - 1, p.Walk);
        }
        else
        {
            // 歩道なしのときは最外縁の1マスが地覆になる。
            Fill(cells, 0, 0, surfY, surfY, 0, total - 1, p.Curb);
            Fill(cells, deckW - 1, deckW - 1, surfY, surfY, 0, total - 1, p.Curb);
        }

        // ===== 高欄 =====
        if (rail > 0)
        {
            Fill(cells, 0, 0, topY + 1, topY + rail, 0, total - 1, p.Rail);
            Fill(cells, deckW - 1, deckW - 1, topY + 1, topY + rail, 0, total - 1, p.Rail);
        }

        // ===== 橋脚 =====
        string pierType = (spec.BridgePierType ?? "t").Trim().ToLowerInvariant();
        for (int i = 1; i < sup.Count - 1; i++)
            BuildPier(cells, p, pierType, deckW, soffit, sup[i]);

        // ===== 橋台と取付部 =====
        // 橋の前後 3 マスに壁式橋台を置き、床版と路面を続けて取付道路にする。
        if (spec.BridgeAbutment)
        {
            BuildAbutment(cells, p, deckW, roadX0, roadX1, soffit, deckY, surfY, walkY,
                walk, rail, topY, -3, -1);
            BuildAbutment(cells, p, deckW, roadX0, roadX1, soffit, deckY, surfY, walkY,
                walk, rail, topY, total, total + 2);
        }

        // ===== 照明 =====
        // 灯具間隔30m級。片側交互（千鳥）に立て、灯具は柱の頂部に載せる。
        int lightStep = Clamp(spec.BridgeLightStep ?? 30, 0, 80);
        if (lightStep > 0)
        {
            const int PoleHeight = 4;
            int baseY = topY + rail;
            bool left = true;
            for (int z = lightStep / 2; z < total; z += lightStep)
            {
                int x = left ? 0 : deckW - 1;
                Fill(cells, x, x, baseY + 1, baseY + PoleHeight - 1, z, z, p.Rail);
                cells[(x, baseY + PoleHeight, z)] = p.Light;
                left = !left;
            }
        }
    }

    // 橋脚1基。天端には全幅の梁（コーピング）を必ず載せ、その下を形式ごとに変える。
    //   "wall"  … 全幅の壁。幅員が広い橋で使う。
    //   "frame" … 2本柱のラーメン。
    //   "t"     … 中央1本の柱（張出式＝T型）。最も一般的。
    private static void BuildPier(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        string pierType, int deckW, int soffit, int z)
    {
        const int Thick = 2;   // 橋軸方向の厚み
        int z0 = z - Thick / 2;
        int z1 = z0 + Thick - 1;

        // 梁（コーピング）。桁下端のすぐ下に全幅で入る。
        int capY = soffit - 1;
        if (capY < 0) return;
        Fill(cells, 0, deckW - 1, capY, capY, z0, z1, p.Pier);
        if (capY == 0) return;

        if (pierType == "wall")
        {
            Fill(cells, 0, deckW - 1, 0, capY - 1, z0, z1, p.Pier);
            return;
        }

        if (pierType == "frame")
        {
            int colW = Math.Max(2, deckW / 6);
            int c1 = Clamp(deckW / 4 - colW / 2, 0, deckW - colW);
            int c2 = Clamp(deckW * 3 / 4 - colW / 2, 0, deckW - colW);
            Fill(cells, c1, c1 + colW - 1, 0, capY - 1, z0, z1, p.Pier);
            Fill(cells, c2, c2 + colW - 1, 0, capY - 1, z0, z1, p.Pier);
            return;
        }

        int tW = Math.Max(2, deckW / 3);
        int tx = Clamp((deckW - tW) / 2, 0, Math.Max(0, deckW - tW));
        Fill(cells, tx, tx + tW - 1, 0, capY - 1, z0, z1, p.Pier);
    }

    // 橋台と取付部。z0..z1 の範囲を橋台の躯体で埋め、上に路面・歩道・高欄を続ける。
    private static void BuildAbutment(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int deckW, int roadX0, int roadX1, int soffit, int deckY, int surfY, int walkY,
        int walk, int rail, int topY, int z0, int z1)
    {
        Fill(cells, 0, deckW - 1, 0, deckY - 1, z0, z1, p.Pier);
        Fill(cells, 0, deckW - 1, deckY, deckY, z0, z1, p.Deck);
        Fill(cells, roadX0, roadX1, surfY, surfY, z0, z1, p.Pave);

        if (walk > 0)
        {
            int leftCurb = roadX0 - 1;
            int rightCurb = roadX1 + 1;
            Fill(cells, leftCurb, leftCurb, surfY, walkY, z0, z1, p.Curb);
            Fill(cells, rightCurb, rightCurb, surfY, walkY, z0, z1, p.Curb);
            Fill(cells, 0, leftCurb - 1, surfY, walkY - 1, z0, z1, p.Curb);
            Fill(cells, 0, leftCurb - 1, walkY, walkY, z0, z1, p.Walk);
            Fill(cells, rightCurb + 1, deckW - 1, surfY, walkY - 1, z0, z1, p.Curb);
            Fill(cells, rightCurb + 1, deckW - 1, walkY, walkY, z0, z1, p.Walk);
        }
        else
        {
            Fill(cells, 0, 0, surfY, surfY, z0, z1, p.Curb);
            Fill(cells, deckW - 1, deckW - 1, surfY, surfY, z0, z1, p.Curb);
        }

        if (rail > 0)
        {
            Fill(cells, 0, 0, topY + 1, topY + rail, z0, z1, p.Rail);
            Fill(cells, deckW - 1, deckW - 1, topY + 1, topY + rail, z0, z1, p.Rail);
        }
    }
}
