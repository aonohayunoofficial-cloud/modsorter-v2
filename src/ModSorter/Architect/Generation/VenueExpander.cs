using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 屋外イベント会場（structure_type="venue"）の座標生成。
// ExpandCore の床/壁/屋根/開口部・入口保証・フットプリントマスクは一切通らない。
// ship と同じ早期リターン方式なので、既存の中分類には影響しない。
//
//   arena    … コロッセウム。外形188×156m / アリーナ87×55m（長径の46%・短径の35%）/
//               高さ48m / アリーナと最前列の間に5mのポディウム壁 / 外周は約6.8m間隔の
//               アーチ列を3層 / 屋根は無く可動日除け(velarium)のみ。
//   stadium  … 近代スタジアム。矩形ピッチを角丸の連続ボウルが四周囲む。
//               片面スタンド単体（背面棟＋妻壁＋持ち出し屋根）も同じ経路で作る。
//   bandshell… エピダウロス劇場（円形オルケストラ＋210°の扇形カヴェア＋ディアゾマ）に
//               ハリウッドボウルの同心円シェル（半ドーム）を合わせたもの。
//   stage    … 櫓ステージ。屋根は4隅の柱と桁で支え、妻面も塞ぐ。
//   tents    … 切妻テントの列。地面は既定で敷かない（床とテント床の二重を作らない）。
//
// すべて「正面が南（+z 側）」で組み、最後に Rotate で向きを回す。
public static class VenueExpander
{
    private sealed class Palette
    {
        public readonly string Structure, Seat, Field, Roof, Accent;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Structure = Pick(spec.WallBlock, allowed, fallback);
            Seat = Pick(spec.SeatBlock ?? spec.AccentBlock, allowed, Structure);
            Field = Pick(spec.FloorBlock, allowed, Structure);
            Roof = Pick(spec.RoofBlock, allowed, Structure);
            Accent = Pick(spec.AccentBlock, allowed, Structure);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        string kind = (spec.VenueKind ?? "arena").Trim().ToLowerInvariant();
        switch (kind)
        {
            case "stadium": BuildStadium(cells, spec, p); break;
            case "bandshell": BuildBandshell(cells, spec, p); break;
            case "stage": BuildStage(cells, spec, p); break;
            case "tents": BuildTents(cells, spec, p); break;
            default: BuildArena(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== 円形闘技場（コロッセウム）=====
    private static void BuildArena(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 15, 63);
        int d = Clamp(spec.Depth, 15, 63);
        int rows = Clamp(spec.VenueRows ?? 5, 1, 20);
        int run = Clamp(spec.VenueRun ?? 3, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 2, 1, 3);
        int podiumH = Clamp(spec.VenuePodium ?? 5, 0, 12);
        int wallH = Clamp(spec.VenueWall ?? 4, 0, 16);
        const int podiumT = 2;   // ポディウム壁の厚み。本物の壁は厚い。

        int topSeat = BuildBowl(cells, w, d, 2.0, ref rows, run, rise,
            podiumH, podiumT, p, out int ring);

        var perim = Perimeter(w, d, 2.0);
        if (wallH > 0) RaiseFacade(cells, perim, topSeat + 1, topSeat + wallH, 1, p.Accent);

        int facadeTop = topSeat + wallH;
        // 外周アーチ。7マスに1連＝外周545mに80連（約6.8m間隔）と同じ密度。
        CarveArcade(cells, perim, facadeTop, 7, 4, Math.Max(1, (facadeTop - 1) / 6));

        // 入場路（vomitoria）。客席の下をくぐってアリーナへ抜ける。上は中実のまま。
        if (spec.VenueGates) CarveTunnels(cells, w, d, 4, 1);

        // 本物に屋根は無い。任意で日除け（velarium）だけを客席の上に張る。
        if (spec.VenueRoof) BuildAwning(cells, w, d, 2.0, ring, podiumT, facadeTop + 1, p.Roof);
    }

    // ===== 競技場 =====
    private static void BuildStadium(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        if ((spec.VenueSides ?? "bowl").Trim().ToLowerInvariant() == "single")
        {
            BuildSingleStand(cells, spec, p);
            return;
        }

        int w = Clamp(spec.Width, 21, 63);
        int d = Clamp(spec.Depth, 21, 63);
        int rows = Clamp(spec.VenueRows ?? 7, 1, 20);
        int run = Clamp(spec.VenueRun ?? 2, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 1, 1, 3);
        int podiumH = Clamp(spec.VenuePodium ?? 2, 0, 8);
        int wallH = Clamp(spec.VenueWall ?? 3, 0, 16);
        const int podiumT = 1;

        // pow=4 の超楕円＝角の丸い矩形。ピッチは矩形、外周は連続したボウルになる。
        int topSeat = BuildBowl(cells, w, d, 4.0, ref rows, run, rise,
            podiumH, podiumT, p, out int ring);

        var perim = Perimeter(w, d, 4.0);
        if (wallH > 0) RaiseFacade(cells, perim, topSeat + 1, topSeat + wallH, 1, p.Accent);

        int facadeTop = topSeat + wallH;
        // 外装のコンコース開口。5マスに1つ、高さ3。
        CarveArcade(cells, perim, facadeTop, 5, 3, Math.Max(1, (facadeTop - 1) / 5));
        if (spec.VenueGates) CarveTunnels(cells, w, d, 4, 1);

        // 屋根はスタンドの上だけ。ピッチの上は開ける。外周に柱を立てて支える。
        if (spec.VenueRoof)
        {
            int lift = Clamp(spec.VenueRoofHeight ?? 4, 1, 12);
            int roofY = facadeTop + lift;
            RaiseFacade(cells, perim, facadeTop + 1, roofY - 1, 4, p.Structure);
            BuildAwning(cells, w, d, 4.0, ring, podiumT, roofY, p.Roof);
        }
    }

    // 片面スタンド単体。背面のコンコース棟・妻壁・持ち出し屋根まで作って単独で完結させる。
    // 参考: 競馬場／陸上競技場のメインスタンド。
    private static void BuildSingleStand(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 7, 63);
        int rows = Clamp(spec.VenueRows ?? 8, 1, 20);
        int run = Clamp(spec.VenueRun ?? 2, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 1, 1, 3);
        int podiumH = Clamp(spec.VenuePodium ?? 2, 0, 8);
        int wallH = Clamp(spec.VenueWall ?? 4, 1, 16);
        const int concourse = 3;

        int seatD = rows * run;
        int d = seatD + concourse;
        int topSeat = podiumH + (rows - 1) * rise;
        int roofY = topSeat + wallH + 1;

        int LocalTop(int z)
        {
            if (z < concourse) return topSeat;
            int k = d - 1 - z;
            return podiumH + (k / run) * rise;
        }

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < d; z++)
            {
                int top = LocalTop(z);
                for (int y = 0; y < top; y++) cells[(x, y, z)] = p.Structure;
                cells[(x, top, z)] = z < concourse ? p.Field : p.Seat;
            }
            // フィールド側の立ち上がり（ポディウム）。
            for (int y = 0; y < podiumH; y++) cells[(x, y, d - 1)] = p.Accent;
            // 背面の壁。
            for (int y = topSeat + 1; y < roofY; y++) cells[(x, y, 0)] = p.Accent;
        }

        // 妻壁。段の輪郭に沿わせて屋根まで立ち上げる。
        foreach (int gx in new[] { 0, w - 1 })
            for (int z = 0; z < d; z++)
                for (int y = LocalTop(z) + 1; y < roofY; y++)
                    cells[(gx, y, z)] = p.Accent;

        // 屋根。背面の壁と妻壁で支えるので浮かない。
        if (spec.VenueRoof)
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    cells[(x, roofY, z)] = p.Roof;
    }

    // ===== 野外音楽堂（エピダウロス＋ハリウッドボウル）=====
    private static void BuildBandshell(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int rO = Clamp(spec.VenueOrchestra ?? 6, 3, 20);
        int rows = Clamp(spec.VenueRows ?? 12, 1, 30);
        int run = Clamp(spec.VenueRun ?? 2, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 1, 1, 3);
        int shellR = Clamp(spec.VenueShellRadius ?? 9, 4, 24);
        int shellH = Clamp(spec.VenueShellHeight ?? 12, 4, 32);
        int stageH = Clamp(spec.VenueStage ?? 2, 0, 6);

        while (rows > 1 && (rO + 1 + rows * run) * 2 + 1 > 63) rows--;

        // 帯の表。エピダウロスは55段を下34段・上21段に分ける水平通路を持つ。
        var bands = new List<(int Width, int Rise, bool Walk)>();
        int diazomaAfter = rows >= 6 ? Math.Max(2, rows * 34 / 55) : -1;
        for (int r = 0; r < rows; r++)
        {
            bands.Add((run, r == 0 ? 0 : rise, false));
            if (r + 1 == diazomaAfter) bands.Add((2, 0, true));
        }

        var table = new List<(int From, int To, int Y, bool Walk)>();
        int rad = rO + 1, yy = 1;
        foreach (var (bw, br, walk) in bands)
        {
            yy += br;
            table.Add((rad, rad + bw - 1, yy, walk));
            rad += bw;
        }
        int caveaEdge = rad - 1;

        int cx = caveaEdge;
        int cz0 = shellR + 2;

        const double half = 105.0 * Math.PI / 180.0;   // カヴェアは210°
        double axis = Math.PI / 2.0;                   // 客席は +z 側
        int stairs = 9;

        for (int x = cx - caveaEdge; x <= cx + caveaEdge; x++)
            for (int z = cz0 - caveaEdge; z <= cz0 + caveaEdge; z++)
            {
                double dx = x - cx, dz = z - cz0;
                double dist = Math.Sqrt(dx * dx + dz * dz);

                if (dist <= rO + 0.5)
                {
                    cells[(x, 0, z)] = p.Field;   // 円形のオルケストラ
                    continue;
                }
                if (dist > caveaEdge + 0.5) continue;

                double ang = Math.Atan2(dz, dx);
                if (Math.Abs(Norm(ang - axis)) > half) continue;

                int band = -1;
                for (int i = 0; i < table.Count; i++)
                    if (dist >= table[i].From - 0.5 && dist < table[i].To + 0.5) { band = i; break; }
                if (band < 0) continue;

                int top = table[band].Y;
                for (int y = 0; y < top; y++) cells[(x, y, z)] = p.Structure;

                string cap = table[band].Walk ? p.Field : p.Seat;
                // 放射状の階段（クリマケス）。
                for (int s = 0; s < stairs; s++)
                {
                    double ray = axis - half + (2 * half) * (s + 0.5) / stairs;
                    if (Math.Abs(Norm(ang - ray)) * dist < 0.7) { cap = p.Accent; break; }
                }
                cells[(x, top, z)] = cap;
            }

        // 舞台。オルケストラの奥側に半円の台を置く。
        int stageR = Math.Max(3, shellR - 2);
        for (int x = cx - stageR; x <= cx + stageR; x++)
            for (int z = cz0 - stageR; z <= cz0; z++)
            {
                double dx = x - cx, dz = z - cz0;
                if (dx * dx + dz * dz > (stageR + 0.5) * (stageR + 0.5)) continue;
                for (int y = 0; y < stageH; y++) cells[(x, y, z)] = p.Structure;
                cells[(x, stageH, z)] = p.Accent;
            }

        // シェル。半円筒の壁の上に四分の一球を載せた貝殻。開口は客席側だけ。
        int springY = stageH + Math.Max(0, shellH - shellR);
        for (int x = cx - shellR; x <= cx + shellR; x++)
            for (int z = cz0 - shellR; z <= cz0; z++)
            {
                double dx = x - cx, dz = z - cz0;
                double flat = Math.Sqrt(dx * dx + dz * dz);

                if (flat >= shellR - 0.5 && flat <= shellR + 0.5)
                    for (int y = 0; y < springY; y++)
                        cells[(x, y, z)] = Band(y) ? p.Accent : p.Roof;

                for (int y = springY; y <= springY + shellR; y++)
                {
                    double dy = y - springY;
                    double r3 = Math.Sqrt(dx * dx + dz * dz + dy * dy);
                    if (r3 >= shellR - 0.5 && r3 <= shellR + 0.5)
                        cells[(x, y, z)] = Band((int)Math.Round(dy)) ? p.Accent : p.Roof;
                }
            }
    }

    // 同心円バンド（ハリウッドボウルの縞）。3リングごとに装飾材へ替える。
    private static bool Band(int k) => (k / 3) % 2 == 1;

    // ===== ステージ =====
    private static void BuildStage(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 5, 63);
        int d = Clamp(spec.Depth, 5, 63);
        int deckH = Clamp(spec.VenueStage ?? 3, 1, 10);
        int backH = Clamp(spec.VenueWall ?? 8, 0, 20);
        int postH = Clamp(spec.VenueRoofHeight ?? 6, 0, 20);
        bool gable = (spec.RoofType ?? "gable").Trim().ToLowerInvariant() != "flat";

        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                bool edge = x == 0 || z == 0 || x == w - 1 || z == d - 1;
                for (int y = 0; y < deckH; y++) cells[(x, y, z)] = edge ? p.Accent : p.Structure;
                cells[(x, deckH, z)] = p.Field;
            }

        // 背面の幕（正面の反対＝z=0 側）。
        for (int x = 0; x < w && backH > 0; x++)
            for (int y = deckH + 1; y <= deckH + backH; y++)
                cells[(x, y, 0)] = p.Accent;

        if (postH <= 0) return;

        int eave = deckH + postH;
        foreach (var (px, pz) in new[] { (0, 0), (w - 1, 0), (0, d - 1), (w - 1, d - 1) })
            for (int y = deckH + 1; y <= eave; y++) cells[(px, y, pz)] = p.Structure;

        for (int x = 0; x < w; x++) { cells[(x, eave, 0)] = p.Structure; cells[(x, eave, d - 1)] = p.Structure; }
        for (int z = 0; z < d; z++) { cells[(0, eave, z)] = p.Structure; cells[(w - 1, eave, z)] = p.Structure; }

        if (!gable)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    cells[(x, eave + 1, z)] = p.Roof;
            return;
        }

        // 切妻。棟は x 軸に平行（客席から見て軒が正面に来る）。
        for (int z = 0; z < d; z++)
        {
            int k = Math.Min(z, d - 1 - z);
            for (int x = 0; x < w; x++) cells[(x, eave + 1 + k, z)] = p.Roof;
        }
        // 妻面を塞ぐ。
        foreach (int gx in new[] { 0, w - 1 })
            for (int z = 0; z < d; z++)
            {
                int k = Math.Min(z, d - 1 - z);
                for (int y = eave + 1; y < eave + 1 + k; y++) cells[(gx, y, z)] = p.Accent;
            }
    }

    // ===== テント広場 =====
    private static void BuildTents(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int count = Clamp(spec.VenueTentCount ?? 4, 1, 12);
        int tw = Clamp(spec.VenueTentWidth ?? 7, 3, 21) | 1;   // 奇数化して棟を中央に通す
        int td = Clamp(spec.VenueTentDepth ?? 9, 3, 31);
        int eaveH = Clamp(spec.VenueTentEave ?? 3, 2, 8);
        int gap = Clamp(spec.VenueTentGap ?? 3, 1, 12);
        int aisle = Clamp(spec.VenueTentAisle ?? 6, 2, 16);
        int rowsOfTents = (spec.VenueTentRows ?? 1) >= 2 ? 2 : 1;
        bool closed = spec.VenueTentClosed;
        bool pave = spec.VenueTentPave;

        int perRow = (count + rowsOfTents - 1) / rowsOfTents;
        int half = (tw - 1) / 2;
        int placed = 0;

        for (int r = 0; r < rowsOfTents; r++)
        {
            int z0 = r * (td + aisle);
            for (int i = 0; i < perRow && placed < count; i++, placed++)
            {
                int x0 = i * (tw + gap);

                // 地面は既定で敷かない。敷く指定のときだけテントの下に1層。
                if (pave)
                    for (int x = 0; x < tw; x++)
                        for (int z = 0; z < td; z++)
                            cells[(x0 + x, 0, z0 + z)] = p.Field;

                // 柱（開放）または壁（閉鎖）。
                for (int x = 0; x < tw; x++)
                    for (int z = 0; z < td; z++)
                    {
                        bool corner = (x == 0 || x == tw - 1) && (z == 0 || z == td - 1);
                        bool edge = x == 0 || x == tw - 1 || z == 0 || z == td - 1;
                        if (!(closed ? edge : corner)) continue;
                        for (int y = 1; y <= eaveH; y++)
                            cells[(x0 + x, y, z0 + z)] = corner ? p.Structure : p.Accent;
                    }

                // 切妻の天幕。棟は z 軸に平行。
                for (int k = 0; k <= half; k++)
                {
                    int y = eaveH + 1 + k;
                    for (int z = 0; z < td; z++)
                    {
                        cells[(x0 + k, y, z0 + z)] = p.Roof;
                        cells[(x0 + tw - 1 - k, y, z0 + z)] = p.Roof;
                    }
                }
                // 妻面（前後の三角）は天幕の一部として塞ぐ。
                foreach (int gz in new[] { 0, td - 1 })
                    for (int x = 0; x < tw; x++)
                    {
                        int k = Math.Min(x, tw - 1 - x);
                        for (int y = eaveH + 1; y < eaveH + 1 + k; y++)
                            cells[(x0 + x, y, z0 + gz)] = p.Roof;
                    }
            }
        }
    }

    // ===== ボウル（段状客席）の共通生成 =====
    // pow=2 で楕円、pow=4 で角の丸い矩形。内側から podium 帯 → 客席帯 → 外周帯。
    // 各帯は y=0 から天面まで中実に埋めるので段が浮くことは起きない。
    private static int BuildBowl(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, double pow,
        ref int rows, int run, int rise, int podiumH, int podiumT, Palette p, out int ring)
    {
        double a = w / 2.0, b = d / 2.0;
        double cx = (w - 1) / 2.0, cz = (d - 1) / 2.0;

        ring = podiumT + rows * run;
        while (rows > 1 && Math.Min(a, b) - ring < 4.0)
        {
            rows--;
            ring = podiumT + rows * run;
        }

        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                double dx = x - cx, dz = z - cz;
                if (!Inside(dx, dz, a, b, pow)) continue;

                int m = 0;
                while (m < ring && Inside(dx, dz, a - (m + 1), b - (m + 1), pow)) m++;

                if (m >= ring)
                {
                    cells[(x, 0, z)] = p.Field;
                    continue;
                }

                int top;
                string cap;
                if (m >= ring - podiumT)
                {
                    top = podiumH;                              // ポディウム壁
                    cap = p.Accent;
                }
                else
                {
                    int j = (ring - podiumT - 1) - m;           // 内側の客席帯から数えた番号
                    top = podiumH + (j / run) * rise;
                    cap = p.Seat;
                }

                for (int y = 0; y < top; y++) cells[(x, y, z)] = p.Structure;
                cells[(x, top, z)] = cap;
            }

        return podiumH + (rows - 1) * rise;
    }

    // 外形の縁1マス分を角度順に並べたもの。アーチ列の割り付けに使う。
    private static List<(int X, int Z)> Perimeter(int w, int d, double pow)
    {
        double a = w / 2.0, b = d / 2.0, cx = (w - 1) / 2.0, cz = (d - 1) / 2.0;
        var list = new List<(int X, int Z, double Ang)>();
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                double dx = x - cx, dz = z - cz;
                if (!Inside(dx, dz, a, b, pow)) continue;
                if (Inside(dx, dz, a - 1, b - 1, pow)) continue;
                list.Add((x, z, Math.Atan2(dz, dx)));
            }
        return list.OrderBy(t => t.Ang).Select(t => (t.X, t.Z)).ToList();
    }

    // 外周の立ち上がり。step が2以上なら柱列（間引き）になる。
    private static void RaiseFacade(
        Dictionary<(int x, int y, int z), string> cells,
        IReadOnlyList<(int X, int Z)> perim, int fromY, int toY, int step, string block)
    {
        for (int i = 0; i < perim.Count; i++)
        {
            if (step > 1 && i % step != 0) continue;
            for (int y = fromY; y <= toY; y++) cells[(perim[i].X, y, perim[i].Z)] = block;
        }
    }

    // 外周にアーチ列を抜く。bay マスに1連、幅3、最上段だけ1マス狭めて迫り上がりに見せる。
    private static void CarveArcade(
        Dictionary<(int x, int y, int z), string> cells,
        IReadOnlyList<(int X, int Z)> perim, int topY, int bay, int archH, int levels)
    {
        int total = perim.Count;
        if (total < bay * 2) return;

        for (int level = 0; level < levels; level++)
        {
            int baseY = 1 + level * (archH + 2);
            if (baseY + archH > topY) break;

            for (int i = 0; i < total; i++)
            {
                int off = Math.Abs(i % bay - bay / 2);
                if (off > 1) continue;
                for (int y = baseY; y < baseY + archH; y++)
                {
                    if (off == 1 && y == baseY + archH - 1) continue;
                    cells.Remove((perim[i].X, y, perim[i].Z));
                }
            }
        }
    }

    // 入場路。長軸・短軸に沿って客席の下を貫く。上の帯は中実のまま残るので天井が付く。
    private static void CarveTunnels(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int height, int halfWidth)
    {
        int cx = (w - 1) / 2, cz = (d - 1) / 2;
        for (int y = 1; y <= height; y++)
        {
            for (int x = 0; x < w; x++)
                for (int t = -halfWidth; t <= halfWidth; t++) cells.Remove((x, y, cz + t));
            for (int z = 0; z < d; z++)
                for (int t = -halfWidth; t <= halfWidth; t++) cells.Remove((cx + t, y, z));
        }
    }

    // 客席の上だけを覆う日除け／屋根。中央（アリーナ・ピッチ）の上は開けたまま。
    private static void BuildAwning(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, double pow,
        int ring, int podiumT, int y, string block)
    {
        double a = w / 2.0, b = d / 2.0, cx = (w - 1) / 2.0, cz = (d - 1) / 2.0;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                double dx = x - cx, dz = z - cz;
                if (!Inside(dx, dz, a, b, pow)) continue;
                int m = 0;
                while (m < ring && Inside(dx, dz, a - (m + 1), b - (m + 1), pow)) m++;
                if (m < ring - podiumT) cells[(x, y, z)] = block;
            }
    }

    // ===== 共通小物 =====
    private static bool Inside(double dx, double dz, double a, double b, double pow)
    {
        if (a <= 0.0 || b <= 0.0) return false;
        double u = Math.Abs(dx) / a, v = Math.Abs(dz) / b;
        if (pow == 2.0) return u * u + v * v <= 1.0 + 1e-9;
        return Math.Pow(u, pow) + Math.Pow(v, pow) <= 1.0 + 1e-9;
    }

    private static double Norm(double a)
    {
        while (a > Math.PI) a -= 2 * Math.PI;
        while (a < -Math.PI) a += 2 * Math.PI;
        return a;
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 南向きで組んだものを指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string front)
    {
        if (front == "south" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = front switch
            {
                "north" => (w - 1 - x, d - 1 - z),
                "east" => (z, w - 1 - x),
                "west" => (d - 1 - z, x),
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
