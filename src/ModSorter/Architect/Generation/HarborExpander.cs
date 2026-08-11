using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 港湾の単体構造物（structure_type="harbor:<種類>"）の座標生成。
// ship / venue / civic と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・開口部・
// 入口保証・フットプリントマスクは一切通らない。既存の中分類には影響しない。
//
//   quay       … 岸壁（重力式ケーソン式係船岸）。計画水深 10m 級、天端高は朔望平均満潮位
//                +0.5〜1.5m。エプロン幅は水深別に 10〜20m で、コンテナ荷役では 30m 級。
//                係船柱（曲柱）の最大間隔は船型別に 10〜45m、岸壁端部から 2.0m の位置。
//                ガントリークレーンの軌間は 30.48m（100ft）が標準。
//   pier       … 桟橋（直杭式横桟橋）。鋼管杭は径 0.6〜1.0m、杭間隔 4〜6m、上部工厚は
//                1.5〜2m。陸側とは幅 8m 前後の渡橋でつなぐ。背後の護岸は別中分類に委ねる。
//   breakwater … 防波堤（混成堤）。基礎マウンド（捨石）の斜面は 1:2、堤体（ケーソン）幅は
//                10m 前後、天端は上部コンクリートで押さえ、海側に消波ブロックを被覆する。
//   drydock    … ドライドック（乾ドック）。中型で全長 200m 級・幅 30〜40m・深さ 10m 級。
//                側壁は作業段（アルター）が段状に下り、盤木（キールブロック）が中心線上に
//                1.2〜2m 間隔で並ぶ。海側の入口はケーソンゲートで閉じる。
//                延長は 64 マス上限のため、実物の中央部を切り出す粒度にしてある。
//   lighthouse … 灯台。塔高 20〜30m 級、塔身は下部直径 6〜8m から上へテーパーする。
//                頂部に回廊（バルコニー）と灯室が載り、灯室はガラス張り。
//                基礎の上に立つ独立塔で、水深・マウンドは使わない。
//   crane      … ガントリークレーン。軌間 30.48m（100ft）、アウトリーチ 38〜60m、
//                バックリーチ 8〜28m、全揚程 45m 級。門形の脚 2 組の上に横行桁が載り、
//                海側のブームは起伏式で不使用時に跳ね上がる。
//   bridgecrane… 橋形クレーン。荷役ヤードで使う門形で、スパン 23〜26m・揚程 15〜18m、
//                両端に張り出し（カンチレバー）5〜15m。crane と同じ骨格を低く短く組む。
//   bollard    … 係船柱の単体。曲柱（bitt）と直柱（bollard）があり、柱径 0.3〜0.6m・
//                高さ 0.5〜1m。台座付きで岸壁の天端に据える金物。
//
// 断面は「海側が z=0、陸側が z の増加方向」で組み、最後に Rotate で海の向きを回す。
// 1マス=1m。捨石マウンドや消波ブロック・防舷材は z<0 へ張り出すが、Normalize で 0 起点へ寄る。
// 水位は y=depth の面（y=0..depth-1 が水面下、y=depth 以上が干出）。
//
// StructureSpec との対応。
//   width          … 延長（x 方向）。depth/height は使わない（断面は harbor_* から組む）
//   harbor_depth   … 計画水深、harbor_crown … 天端高（水面上）
//   harbor_body    … 岸壁・防波堤の堤体幅／桟橋の幅、harbor_apron … エプロン幅
//   harbor_mound   … 基礎マウンド（捨石）の高さ、harbor_armor … 消波ブロックの幅
//   harbor_pile_step … 杭間隔、harbor_slab … 上部工厚、harbor_approach … 渡橋の長さ
//   harbor_gauge   … クレーンレールの軌間（0 でレールなし）
//   harbor_bollard_step … 係船柱の間隔（0 で係船柱なし）、harbor_fender … 防舷材
//   parapet_height … 防波堤の上部工パラペット、facade_face … 海側の向き（既定 south）
//   wall_block=本体コンクリート / floor_block=舗装・上部工天端 / base_block=捨石・中詰
//   accent_block=縁石・レール・杭 / seat_block=係船柱・防舷材 / roof_block=消波ブロック
//   parapet_block=パラペット
public static class HarborExpander
{
    public const string Prefix = "harbor:";

    // StructureExpander から呼ぶ判定。"harbor:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "pier":
            case "jetty": return "pier";
            case "breakwater":
            case "mole": return "breakwater";
            case "drydock":
            case "dock": return "drydock";
            case "lighthouse":
            case "light": return "lighthouse";
            case "crane":
            case "gantry": return "crane";
            case "bridgecrane": return "bridgecrane";
            case "bollard":
            case "bitt": return "bollard";
            default: return "quay";
        }
    }

    private sealed class Palette
    {
        public readonly string Body, Pave, Rubble, Trim, Fitting, Armor, Parapet;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Body = Pick(spec.WallBlock, allowed, fallback);
            Pave = Pick(spec.FloorBlock, allowed, Body);
            Rubble = Pick(spec.BaseBlock, allowed, Body);
            Trim = Pick(spec.AccentBlock, allowed, Body);
            Fitting = Pick(spec.SeatBlock, allowed, Trim);
            Armor = Pick(spec.RoofBlock, allowed, Rubble);
            Parapet = Pick(spec.ParapetBlock, allowed, Body);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        switch (KindOf(spec.StructureType))
        {
            case "pier": BuildPier(cells, spec, p); break;
            case "breakwater": BuildBreakwater(cells, spec, p); break;
            case "drydock": BuildDryDock(cells, spec, p); break;
            case "lighthouse": BuildLighthouse(cells, spec, p); break;
            case "crane": BuildCrane(cells, spec, p, tall: true); break;
            case "bridgecrane": BuildCrane(cells, spec, p, tall: false); break;
            case "bollard": BuildBollard(cells, spec, p); break;
            default: BuildQuay(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== 岸壁（重力式ケーソン式係船岸）=====
    private static void BuildQuay(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 12, 64);                  // 延長
        int depth = Clamp(spec.HarborDepth ?? 10, 3, 24);     // 計画水深
        int crown = Clamp(spec.HarborCrown ?? 2, 1, 6);       // 天端高（水面上）
        int body = Clamp(spec.HarborBody ?? 10, 4, 24);       // ケーソン幅
        int apron = Clamp(spec.HarborApron ?? 20, 4, 40);     // エプロン幅
        int gauge = Math.Max(0, spec.HarborGauge ?? 0);       // クレーンレール軌間
        int bstep = Math.Max(0, spec.HarborBollardStep ?? 0); // 係船柱の間隔

        int h = depth + crown;              // 全高
        int top = h - 1;                    // 天端の y
        int water = depth;                  // 水面の y（この段から上が干出）
        int mound = Clamp(spec.HarborMound ?? 2, 0, Math.Max(0, h - 3));

        // 基礎マウンド（捨石）。ケーソンの下に敷き、海側へ肩を出して 1:2 で下る。
        for (int k = 0; k < mound; k++)
        {
            int reach = 2 * (mound - 1 - k);
            Fill(cells, 0, len - 1, k, k, -reach, body - 1, p.Rubble);
        }

        // ケーソン本体（外殻コンクリート＋中詰）と上部コンクリート。
        Box(cells, 0, len - 1, mound, top - 1, 0, body - 1, p.Body, p.Rubble);
        Fill(cells, 0, len - 1, top, top, 0, body - 1, p.Body);

        // 裏込め石とエプロンの路盤。背面から離れるほど薄くなる（見えない深部は詰めない）。
        int backTop = top - 1;
        for (int i = 0; i < apron; i++)
        {
            int z = body + i;
            int bottom = Math.Min(backTop - 2, mound + 2 * i / 3);
            if (bottom < 0) bottom = 0;
            Fill(cells, 0, len - 1, bottom, backTop, z, z, p.Rubble);
        }

        // エプロン舗装と前面の縁石。
        Fill(cells, 0, len - 1, top, top, body, body + apron - 1, p.Pave);
        for (int x = 0; x < len; x++) cells[(x, top, 0)] = p.Trim;

        // 係船柱（曲柱）。岸壁端部から 2.0m の位置に、指定間隔で並べる。
        if (bstep >= 5)
        {
            int bz = Math.Min(2, body - 1);
            for (int x = bstep / 2; x < len; x += bstep) cells[(x, top + 1, bz)] = p.Fitting;
        }

        // 防舷材。前面に 1マス張り出し、水面付近をまたぐ高さに付く。
        if (spec.HarborFender)
        {
            int y0 = Math.Max(mound, water - 1);
            int y1 = Math.Min(top - 1, water + 1);
            for (int x = 2; x < len - 2; x += 5)
                for (int y = y0; y <= y1; y++) cells[(x, y, -1)] = p.Fitting;
        }

        // ガントリークレーンの軌道。海側レールは前面から 4m、陸側はそこから軌間ぶん。
        int rail0 = 4;
        int rail1 = rail0 + gauge;
        if (gauge >= 6 && rail1 <= body + apron - 1)
            for (int x = 0; x < len; x++)
            {
                cells[(x, top, rail0)] = p.Trim;
                cells[(x, top, rail1)] = p.Trim;
            }
    }

    // ===== 桟橋（直杭式横桟橋）=====
    private static void BuildPier(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 12, 64);                 // 延長
        int wide = Clamp(spec.HarborBody ?? 15, 6, 40);       // 桟橋の幅
        int depth = Clamp(spec.HarborDepth ?? 8, 3, 24);
        int crown = Clamp(spec.HarborCrown ?? 2, 1, 6);
        int step = Clamp(spec.HarborPileStep ?? 5, 3, 10);    // 杭間隔
        int appr = Math.Max(0, spec.HarborApproach ?? 0);     // 渡橋の長さ
        int bstep = Math.Max(0, spec.HarborBollardStep ?? 0);

        int h = depth + crown;
        int top = h - 1;
        int water = depth;
        int slab = Clamp(spec.HarborSlab ?? 2, 1, Math.Max(1, top));
        int slabBottom = top - slab + 1;

        // 鋼管杭。等間隔の格子で海底から上部工の下端まで通す。
        var xs = Grid(len, step);
        var zs = Grid(wide, step);
        foreach (int x in xs)
            foreach (int z in zs)
                Fill(cells, x, x, 0, slabBottom - 1, z, z, p.Trim);

        // 上部工（受梁・床版）と舗装、両縁の縁石。
        Fill(cells, 0, len - 1, slabBottom, top - 1, 0, wide - 1, p.Body);
        Fill(cells, 0, len - 1, top, top, 0, wide - 1, p.Pave);
        for (int x = 0; x < len; x++)
        {
            cells[(x, top, 0)] = p.Trim;
            cells[(x, top, wide - 1)] = p.Trim;
        }

        // 係船柱と防舷材。海側の縁に付く。
        if (bstep >= 5)
        {
            int bz = Math.Min(2, wide - 1);
            for (int x = bstep / 2; x < len; x += bstep) cells[(x, top + 1, bz)] = p.Fitting;
        }
        if (spec.HarborFender)
        {
            int y0 = Math.Max(0, water - 1);
            for (int x = 2; x < len - 2; x += 5)
                for (int y = y0; y <= top - 1; y++) cells[(x, y, -1)] = p.Fitting;
        }

        // 渡橋。陸側へ幅 8m 前後で延ばし、両側の杭で支える。
        if (appr >= 4)
        {
            int bw = Math.Min(8, wide);
            int bx0 = (len - bw) / 2;
            int bx1 = bx0 + bw - 1;
            int bz1 = wide + appr - 1;

            Fill(cells, bx0, bx1, slabBottom, top - 1, wide, bz1, p.Body);
            Fill(cells, bx0, bx1, top, top, wide, bz1, p.Pave);
            for (int z = wide; z <= bz1; z++)
            {
                cells[(bx0, top, z)] = p.Trim;
                cells[(bx1, top, z)] = p.Trim;
            }
            for (int z = wide; z <= bz1; z += step)
            {
                Fill(cells, bx0, bx0, 0, slabBottom - 1, z, z, p.Trim);
                Fill(cells, bx1, bx1, 0, slabBottom - 1, z, z, p.Trim);
            }
        }
    }

    // ===== 防波堤（混成堤）=====
    private static void BuildBreakwater(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 12, 64);                 // 延長
        int depth = Clamp(spec.HarborDepth ?? 10, 3, 24);
        int crown = Clamp(spec.HarborCrown ?? 5, 1, 10);     // 天端高（水面上）
        int body = Clamp(spec.HarborBody ?? 10, 4, 24);      // 堤体（ケーソン）幅
        int armor = Math.Max(0, spec.HarborArmor ?? 0);      // 消波ブロックの幅
        int parapet = Clamp(spec.ParapetHeight ?? 0, 0, 4);

        int h = depth + crown;
        int top = h - 1;
        int mound = Clamp(spec.HarborMound ?? 3, 1, Math.Max(1, h - 3));

        // 基礎マウンド（捨石）。海側・港内側の両方へ 1:2 で広がる。
        for (int k = 0; k < mound; k++)
        {
            int reach = 2 * (mound - 1 - k);
            Fill(cells, 0, len - 1, k, k, -reach, body - 1 + reach, p.Rubble);
        }

        // 堤体（ケーソン）と上部コンクリート。
        Box(cells, 0, len - 1, mound, top - 1, 0, body - 1, p.Body, p.Rubble);
        Fill(cells, 0, len - 1, top, top, 0, body - 1, p.Body);

        // 上部工のパラペット。海側の端を立ち上げて越波を抑える。
        for (int py = 1; py <= parapet; py++)
            for (int x = 0; x < len; x++) cells[(x, top + py, 0)] = p.Parapet;

        // 消波ブロック。堤体の海側前面に寄せ、天端付近から 1:1.33 で下る。
        for (int j = 0; j < armor; j++)
        {
            int t = top - 1 - 3 * j / 4;
            if (t < 1) break;
            Fill(cells, 0, len - 1, 0, t, -1 - j, -1 - j, p.Armor);
        }
    }

    // ===== ドライドック（乾ドック）=====
    // 海側（z=0）にゲート、そこから陸側へ掘り込む。掘り込みの中は空洞のまま残し、
    // 側壁・底版・作業段・盤木だけを置く。ドック外周の地表面も 1マス分だけ敷いて
    // 縁を出すので、プレビューで掘り込みの深さが分かる。
    private static void BuildDryDock(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int len = Clamp(spec.Width, 16, 64);                 // ドックの長さ（x 方向）
        int wide = Clamp(spec.HarborBody ?? 34, 10, 40);      // ドックの内幅
        int deep = Clamp(spec.HarborDepth ?? 10, 4, 20);      // 掘り込みの深さ
        int steps = Clamp(spec.HarborAltarSteps ?? 3, 0, 6);  // 作業段の段数
        int kstep = Math.Max(0, spec.HarborKeelStep ?? 0);    // 盤木の間隔
        int gate = Math.Max(0, spec.HarborGate ?? 0);         // ゲートの厚み
        int wall = 3;                                        // 側壁の厚み

        int floorY = 0;                  // 底版の上面
        int topY = deep;                 // 地表面（ドック縁）の y
        int z0 = gate;                   // 掘り込みの海側端（ゲートのぶん奥へ寄る）
        int z1 = z0 + len - 1;           // 陸側端（ドック頭部）

        // 底版。ドック内幅より側壁ぶん外へ広げて敷く。
        Fill(cells, -wall, wide + wall - 1, floorY, floorY, z0, z1 + wall, p.Body);

        // 側壁（左右）とドック頭部の壁。地表面まで立ち上げる。
        Fill(cells, -wall, -1, floorY + 1, topY, z0, z1 + wall, p.Body);
        Fill(cells, wide, wide + wall - 1, floorY + 1, topY, z0, z1 + wall, p.Body);
        Fill(cells, -wall, wide + wall - 1, floorY + 1, topY, z1 + 1, z1 + wall, p.Body);

        // 作業段（アルター）。側壁の内側を段状に下げ、上ほど内側へ張り出す。
        // 段の高さは掘り込みを段数で割り、幅は 1 段 1マスずつ内側へ寄せる。
        for (int s = 0; s < steps; s++)
        {
            int hStep = Math.Max(1, (deep - 1) / Math.Max(1, steps));
            int y0 = floorY + 1 + s * hStep;
            int y1 = Math.Min(topY, y0 + hStep - 1);
            int inset = steps - s;                       // 上の段ほど内側へ出ない
            if (y0 > topY) break;
            Fill(cells, 0, inset - 1, y0, y1, z0, z1, p.Body);
            Fill(cells, wide - inset, wide - 1, y0, y1, z0, z1, p.Body);
        }

        // 盤木（キールブロック）。中心線上に等間隔で並ぶ、底版から 2マスの台。
        if (kstep >= 2)
        {
            int cx = wide / 2;
            for (int z = z0 + kstep; z <= z1 - 2; z += kstep)
            {
                cells[(cx, floorY + 1, z)] = p.Rubble;
                cells[(cx, floorY + 2, z)] = p.Trim;
            }
        }

        // ケーソンゲート。海側の入口を塞ぐ扉体。底版から地表面まで通す。
        for (int g = 0; g < gate; g++)
            Fill(cells, -wall, wide + wall - 1, floorY, topY, g, g, p.Trim);

        // ドック縁の舗装。周囲 4マス分を地表面の高さに敷き、縁石を回す。
        int apron = 4;
        Fill(cells, -wall - apron, wide + wall + apron - 1, topY, topY,
             z0 - apron, z1 + wall + apron, p.Pave);
        // 掘り込みの内側は舗装を抜き、縁だけ縁石にする。
        for (int x = 0; x < wide; x++)
            for (int z = z0; z <= z1; z++) cells.Remove((x, topY, z));
        for (int z = z0 - 1; z <= z1 + 1; z++)
        {
            cells[(-1, topY, z)] = p.Trim;
            cells[(wide, topY, z)] = p.Trim;
        }
        for (int x = -1; x <= wide; x++) cells[(x, topY, z1 + 1)] = p.Trim;

        // 係船柱。ドック縁の両側に並べる。
        int bstep = Math.Max(0, spec.HarborBollardStep ?? 0);
        if (bstep >= 5)
            for (int z = z0 + bstep / 2; z <= z1; z += bstep)
            {
                cells[(-2, topY + 1, z)] = p.Fitting;
                cells[(wide + 1, topY + 1, z)] = p.Fitting;
            }
    }

    // ===== 灯台 =====
    // 円形テーパーの塔身の上に回廊と灯室を載せる。水深・マウンドは使わず、
    // 基礎の上に立つ独立塔として組む。
    private static void BuildLighthouse(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int shaft = Clamp(spec.HarborShaft ?? 7, 3, 21);       // 下部直径
        int h = Clamp(spec.HarborCrown ?? 24, 6, 60);          // 塔身の高さ
        int taper = Math.Max(0, spec.HarborTaper ?? 8);        // 何マスで直径 1 絞るか
        int gallery = Clamp(spec.HarborGallery ?? 1, 0, 3);    // 回廊の張り出し
        int lantern = Clamp(spec.HarborLantern ?? 4, 0, 10);   // 灯室の高さ
        int baseH = Clamp(spec.HarborMound ?? 2, 0, 8);        // 基礎の高さ

        // 中心は下部直径の中央。上で絞っても中心は動かさない。
        double c = (shaft - 1) / 2.0;
        int y = 0;

        // 基礎。塔身より 2マス外へ広い円盤。
        for (; y < baseH; y++) Disc(cells, c, shaft + 4, y, p.Body, filled: true);

        // 塔身。テーパーで直径を絞りながら、外周 1マス厚のリングを積む。
        // 内部は空洞にして階段室に見せる。窓は各層に 1つ、海側へ向けて抜く。
        int dia = shaft;
        int shaftTop = y + h - 1;
        for (int i = 0; y <= shaftTop; y++, i++)
        {
            if (taper > 0 && i > 0 && i % taper == 0 && dia > 3) dia -= 2;
            Disc(cells, c, dia, y, p.Body, filled: false);
            if (i > 2 && i % 6 == 0) Window(cells, c, dia, y, p.Fitting);
        }

        // 回廊（バルコニー）。塔身の上端に張り出す床と、その外周の手すり。
        int gdia = dia + 2 * gallery;
        if (gallery > 0)
        {
            Disc(cells, c, gdia, y, p.Pave, filled: true);
            Disc(cells, c, gdia, y + 1, p.Fitting, filled: false);
            y += 2;
        }

        // 灯室。回廊の内側にガラス張りで立ち上げ、上を屋根で塞ぐ。
        int ldia = Math.Max(3, dia - 2);
        for (int k = 0; k < lantern; k++, y++) Disc(cells, c, ldia, y, p.Armor, filled: false);
        if (lantern > 0)
        {
            Disc(cells, c, ldia, y, p.Parapet, filled: true);
            Disc(cells, c, Math.Max(1, ldia - 2), y + 1, p.Parapet, filled: true);
        }
    }

    // 中心 c・直径 dia の円を 1 段だけ置く。filled=false なら外周 1マス厚のリング。
    private static void Disc(
        Dictionary<(int x, int y, int z), string> cells,
        double c, int dia, int y, string block, bool filled)
    {
        if (dia <= 0) return;
        double r = dia / 2.0;
        int lo = (int)Math.Floor(c - r), hi = (int)Math.Ceiling(c + r);

        for (int x = lo; x <= hi; x++)
            for (int z = lo; z <= hi; z++)
            {
                double dx = x - c, dz = z - c;
                double dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist > r) continue;
                if (!filled && dist < r - 1.0) continue;
                cells[(x, y, z)] = block;
            }
    }

    // 塔身の海側（z 最小の方向）に窓を 1マス抜き、ガラスを入れる。
    private static void Window(
        Dictionary<(int x, int y, int z), string> cells,
        double c, int dia, int y, string glass)
    {
        int cx = (int)Math.Round(c);
        int z = (int)Math.Round(c - dia / 2.0);
        for (int dz = 0; dz < 2; dz++)
            if (cells.ContainsKey((cx, y, z + dz)))
            {
                cells[(cx, y, z + dz)] = glass;
                return;
            }
    }

    // ===== クレーン（ガントリークレーン／橋形クレーン）=====
    // 門形の脚 2 組（海側・陸側）の上に横行桁を渡し、海側へアウトリーチ、
    // 陸側へバックリーチを張り出す。tall=true がコンテナクレーン（軌間 30m 級・
    // 脚高 30m 級・ブーム起伏あり）、false が荷役ヤードの橋形クレーン。
    // 走行方向を x、海陸方向を z にとる。z=0 が海側の桁先端。
    //
    // ブームの起伏角は実物に合わせて 0〜84 度（港湾クレーンの諸元表の範囲）。
    // ラチスブームの実用上限は 70〜80 度で、STS の格納姿勢がその上端にあたる。
    // 起伏の支点は海側脚の直上（seaRail）で、そこから先端までを円弧ではなく
    // 直線の傾斜として組む。桁と同じ厚み・同じ幅を保ったまま持ち上げる。
    private static void BuildCrane(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p, bool tall)
    {
        int gauge = Clamp(spec.HarborGauge ?? (tall ? 30 : 24), 6, 40);        // 軌間
        int legH = Clamp(spec.HarborLegHeight ?? (tall ? 32 : 16), 6, 56);     // 脚の高さ
        int legS = Clamp(spec.HarborLegSize ?? 2, 1, 5);                       // 脚の太さ
        int legB = Clamp(spec.HarborLegBase ?? (tall ? 16 : 10), legS * 2, 40); // 走行方向の脚間隔
        int outr = Clamp(spec.HarborOutreach ?? (tall ? 38 : 8), 0, 60);       // アウトリーチ
        int back = Clamp(spec.HarborBackreach ?? (tall ? 14 : 8), 0, 30);      // バックリーチ
        int raise = tall ? Clamp(spec.HarborBoomRaise ?? 0, 0, 84) : 0;        // ブーム起伏角（度）
        bool mach = spec.HarborMachinery;

        int girder = 3;                       // 横行桁の厚み（上下弦材＋斜材ぶん）
        int seaRail = outr;                   // 海側レールの z
        int landRail = outr + gauge;          // 陸側レールの z
        int zEnd = landRail + back;           // 陸側の桁先端
        int topY = legH;                      // 横行桁の下端の y
        int width = legB + legS;              // 走行方向の全幅（脚の外々）

        // 走行装置（シルビーム）。脚の下端をレール方向へつなぐ台車。
        foreach (int z in new[] { seaRail, landRail })
        {
            Fill(cells, 0, width - 1, 0, 0, z - legS / 2, z - legS / 2 + legS - 1, p.Trim);
            Fill(cells, 0, width - 1, 1, 1, z - legS / 2, z - legS / 2 + legS - 1, p.Body);
        }

        // 門形の脚。走行方向の前後 2 本×海陸 2 組の計 4 本を立てている。
        var legXs = new[] { 0, width - legS };
        foreach (int lx in legXs)
            foreach (int z in new[] { seaRail, landRail })
                Fill(cells, lx, lx + legS - 1, 2, topY - 1, z - legS / 2, z - legS / 2 + legS - 1, p.Body);

        // 脚の頭をつなぐ横梁（ポータルビーム）。海陸それぞれで走行方向に渡す。
        foreach (int z in new[] { seaRail, landRail })
            Fill(cells, 0, width - 1, topY - 2, topY - 1, z - legS / 2, z - legS / 2 + legS - 1, p.Trim);

        // 脚間の横行桁とバックリーチ。桁は下弦・腹・上弦の 3 層で、走行方向は全幅ぶん。
        // 下弦と上弦を面で張り、その間の腹は走行方向の両端だけを閉じた箱桁にする。
        GirderSpan(cells, p, 0, width - 1, topY, girder, seaRail, zEnd, 0, 0);

        // アウトリーチ（海側ブーム）。支点は海側レールで、そこから先端へ raise 度で上がる。
        // 1 マス進むごとの上がり幅を tan(raise) から出し、桁と同じ断面のまま傾ける。
        if (outr > 0)
        {
            double slope = Math.Tan(raise * Math.PI / 180.0);
            int tipLift = 0;
            for (int i = 1; i <= outr; i++)
            {
                int z = seaRail - i;
                int lift = (int)Math.Round(slope * i);
                tipLift = lift;
                // 前の断面との段差を垂直材で埋め、斜路が途切れないようにする。
                int prevLift = (int)Math.Round(slope * (i - 1));
                GirderSpan(cells, p, 0, width - 1, topY + lift, girder, z, z, prevLift - lift, 0);
            }

            // ブーム先端の妻面。トロリの行き止まりになる面を閉じる。
            Fill(cells, 0, width - 1, topY + tipLift, topY + tipLift + girder - 1,
                 seaRail - outr, seaRail - outr, p.Trim);
        }

        // ブームを吊る支柱（Aフレーム）とタイバー。起伏式のブームは陸側脚の上の塔から引く。
        // 跳ね上げたときの先端に追従させるため、タイバーの海側端は実際の先端高さへ結ぶ。
        if (tall)
        {
            int pylonY = topY + girder;
            int pylonH = Math.Max(4, legH / 4);
            int px = width / 2;
            Fill(cells, px - legS / 2, px - legS / 2 + legS - 1, pylonY, pylonY + pylonH - 1,
                 landRail - legS / 2, landRail - legS / 2 + legS - 1, p.Body);

            int apex = pylonY + pylonH - 1;
            if (outr > 0)
            {
                int tipY = topY + (int)Math.Round(Math.Tan(raise * Math.PI / 180.0) * outr) + girder;
                TieBar(cells, px, apex, landRail, tipY, seaRail - outr, p.Trim);
            }
            if (back > 0)
                TieBar(cells, px, apex, landRail, topY + girder, zEnd, p.Trim);
        }

        // 機械室と運転室。機械室は陸側桁の上、運転室は海側脚の内側の桁下に吊る。
        if (mach)
        {
            int mz0 = landRail + 1, mz1 = Math.Min(zEnd, landRail + 6);
            if (mz1 > mz0)
                Box(cells, 1, width - 2, topY + girder, topY + girder + 3, mz0, mz1, p.Pave, p.Body);

            int cx = width / 2;
            int cz = seaRail - 2;
            if (cz >= 0)
                Box(cells, cx - 1, cx + 1, topY - 3, topY - 1, cz - 1, cz + 1, p.Fitting, p.Fitting);
        }
    }

    // 横行桁の 1 区間。下弦・上弦を面で張り、走行方向の両端に腹板を立てた箱桁にする。
    // drop>0 のときは下弦をその分だけ下へ延ばし、傾斜区間の段差を垂直材で埋める。
    private static void GirderSpan(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int x0, int x1, int baseY, int girder, int z0, int z1, int drop, int rise)
    {
        int lowY = baseY - Math.Max(0, drop);
        int topOfGirder = baseY + girder - 1 + Math.Max(0, rise);

        // 下弦。傾斜で生じた段差ぶんだけ下へ伸ばす。
        Fill(cells, x0, x1, lowY, baseY, z0, z1, p.Trim);
        // 腹板。走行方向の両端だけを閉じ、内側は開けてトラスらしく見せる。
        Fill(cells, x0, x0, baseY, topOfGirder, z0, z1, p.Trim);
        Fill(cells, x1, x1, baseY, topOfGirder, z0, z1, p.Trim);
        // 上弦。トロリの走る面。
        Fill(cells, x0, x1, topOfGirder, topOfGirder, z0, z1, p.Body);
    }

    // 2 点を結ぶ 1マス幅の斜材（タイバー・ステー）。x は固定、z-y 平面で引く。
    private static void TieBar(
        Dictionary<(int x, int y, int z), string> cells,
        int x, int y0, int z0, int y1, int z1, string block)
    {
        int steps = Math.Max(Math.Abs(z1 - z0), Math.Abs(y1 - y0));
        if (steps == 0) return;
        for (int i = 0; i <= steps; i++)
        {
            int z = z0 + (int)Math.Round((double)(z1 - z0) * i / steps);
            int y = y0 + (int)Math.Round((double)(y1 - y0) * i / steps);
            if (!cells.ContainsKey((x, y, z))) cells[(x, y, z)] = block;
        }
    }

    // ===== 係船柱（単体）=====
    // 岸壁の天端に据える金物。曲柱（bitt）は頭部が張り出したキノコ形、
    // 直柱（bollard）は上へ細くなる円柱。台座の上に載せる。
    private static void BuildBollard(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int dia = Clamp(spec.HarborBollardSize ?? 3, 1, 9);        // 柱径
        int h = Clamp(spec.HarborBollardHeight ?? 4, 2, 16);       // 柱の高さ
        int ped = Clamp(spec.HarborPedestal ?? (dia + 4), 0, 21);  // 台座の一辺
        bool bitt = string.Equals(spec.HarborBollardType, "bitt", StringComparison.OrdinalIgnoreCase);

        int span = Math.Max(ped, dia + 2);
        double c = (span - 1) / 2.0;
        int y = 0;

        // 台座。天端に埋め込まれる四角い基礎。
        if (ped > 0)
        {
            int lo = (int)Math.Round(c - (ped - 1) / 2.0);
            Fill(cells, lo, lo + ped - 1, 0, 0, lo, lo + ped - 1, p.Pave);
            y = 1;
        }

        // 柱身。直柱は上端で 2 マス細り、曲柱は太さを保つ。
        int top = y + h - 1;
        for (int k = 0; y <= top; y++, k++)
        {
            int d = (!bitt && h >= 4 && k >= h - 2 && dia > 1) ? dia - 2 : dia;
            Disc(cells, c, d, y, p.Fitting, filled: true);
        }

        // 頭部。曲柱は 2 マス張り出したキノコ形、直柱は 1 マスの笠。
        Disc(cells, c, dia + (bitt ? 2 : 1), y, p.Trim, filled: true);
        if (bitt) Disc(cells, c, dia, y + 1, p.Fitting, filled: true);
    }

    // ===== 共通部品 =====

    private static void Fill(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string block)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++) cells[(x, y, z)] = block;
    }

    // 外殻と中詰を分けた箱。ケーソンの側壁と中詰砂に使う。
    private static void Box(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string shell, string core)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || y == y0 || y == y1 || z == z0 || z == z1;
                    cells[(x, y, z)] = edge ? shell : core;
                }
    }

    // 0..span-1 の範囲に、両端を含めて step 以下の間隔で並ぶ位置を返す（杭の割付）。
    private static List<int> Grid(int span, int step)
    {
        var list = new List<int>();
        if (span <= 1) { list.Add(0); return list; }
        int n = Math.Max(2, (span - 1) / step + 1);
        for (int i = 0; i < n; i++)
            list.Add((int)Math.Round((double)(span - 1) * i / (n - 1)));
        return list.Distinct().ToList();
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 海側を z の小さい側（north）として組んだものを、指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string sea)
    {
        if (sea == "north" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = sea switch
            {
                "south" => (w - 1 - x, d - 1 - z),
                "east" => (d - 1 - z, x),
                "west" => (z, w - 1 - x),
                _ => (x, z)
            };
            dst[(nx, kv.Key.y, nz)] = kv.Value;
        }
        return dst;
    }

    private static List<GeneratedBlock> Normalize(Dictionary<(int x, int y, int z), string> cells)
    {
        if (cells.Count == 0) return new List<GeneratedBlock>();

        int minX = cells.Keys.Min(k => k.x);
        int minY = cells.Keys.Min(k => k.y);
        int minZ = cells.Keys.Min(k => k.z);

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x - minX,
                Y = kv.Key.y - minY,
                Z = kv.Key.z - minZ,
                Id = kv.Value
            })
            .ToList();
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
