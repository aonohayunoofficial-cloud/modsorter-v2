using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// StructureSpec を確定的に座標へ展開する。
// 壁の外周リングは必ずここで生成するため、塊化や壁抜けは原理的に起きない。
public static class StructureExpander
{
    // 公開エントリ。volumes が指定されていれば各 Part を個別展開してオフセット合成する。
    // 空なら従来どおり単一の箱として ExpandCore に委譲する（後方互換）。
    public static List<GeneratedBlock> Expand(StructureSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // ===== 複数ボリューム合成（フェーズ2）=====
        if (spec.Volumes != null && spec.Volumes.Count > 0)
        {
            var merged = new Dictionary<(int x, int y, int z), string>();
            foreach (var vol in spec.Volumes)
            {
                if (vol?.Part == null) continue;

                // Part は単一の箱として展開する。Part 内にさらに volumes があっても
                // ExpandCore は volumes を参照しないので、再帰は1段で止まる（無限再帰防止）。
                var partBlocks = ExpandCore(vol.Part, allowedBlocks);

                // オフセットは絶対配置。負値は 0 にクランプ（宙抜け・負座標を防ぐ）。
                int ox = Math.Max(0, vol.OffsetX);
                int oy = Math.Max(0, vol.OffsetY);
                int oz = Math.Max(0, vol.OffsetZ);

                // 重なりは後勝ち（リストで後ろの Part が上書きする）。
                foreach (var b in partBlocks)
                    merged[(b.X + ox, b.Y + oy, b.Z + oz)] = b.Id;
            }

            return merged
                .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
                .Select(kv => new GeneratedBlock
                {
                    X = kv.Key.x,
                    Y = kv.Key.y,
                    Z = kv.Key.z,
                    Id = kv.Value
                })
                .ToList();
        }

        return ExpandCore(spec, allowedBlocks);
    }

    // 単一の箱を確定的に座標へ展開する（従来の Expand 本体をそのまま移設）。
    // このメソッドは spec.Volumes を一切参照しない。ゆえに Part 内に volumes があっても
    // 展開されず、フェーズ2の再帰は1段で止まる。
    private static List<GeneratedBlock> ExpandCore(StructureSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // 寸法の健全化（最低 2x2x2、極端な値は抑える）
        int w = Clamp(spec.Width, 2, 64);
        int d = Clamp(spec.Depth, 2, 64);
        int h = Clamp(spec.Height, 2, 64);

        // 素材決定（許可リスト外なら先頭ブロックにフォールバック）
        string fallback = allowedBlocks.Count > 0 ? allowedBlocks[0] : "minecraft:oak_planks";

        // 全体形状モード。"building"（既定）以外は床/壁/屋根/開口部を一切通さず、
        // 専用ビルダーが座標を確定する。早期リターンで通常ロジックを完全にバイパスする。
        string structureType = (spec.StructureType ?? "building").Trim().ToLowerInvariant();
        if (structureType == "ramp")
        {
            string rampBody = Pick(spec.WallBlock ?? spec.FloorBlock, allowedBlocks, fallback);
            string rampBase = Pick(spec.BaseBlock ?? spec.FloorBlock ?? spec.WallBlock, allowedBlocks, rampBody);
            return BuildRamp(w, d, h, rampBody, rampBase, spec.RidgeAxis);
        }
        if (structureType == "bridge")
        {
            string deckBlock = Pick(spec.WallBlock ?? spec.FloorBlock, allowedBlocks, fallback);
            string pierBlock = Pick(spec.BaseBlock ?? spec.WallBlock ?? spec.FloorBlock, allowedBlocks, deckBlock);
            return BuildBridge(w, d, h, deckBlock, pierBlock, spec.RidgeAxis);
        }
        if (structureType == "ship")
        {
            // 船は ShipExpander が船体・甲板・上部構造物・出入口をすべて確定的に作る。
            // 床/壁/屋根/開口部・入口保証は一切通さない（出入口は船種ごとに自動配置）。
            return ShipExpander.Build(spec, w, d, h, allowedBlocks, fallback);
        }
        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string floor = Pick(spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string roof = Pick(spec.RoofBlock ?? spec.WallBlock, allowedBlocks, wall);

        // 平面形状（フットプリント）。矩形以外を許すためのマスク。
        // w×d 確定後に一度だけ集約して作る（プリセット→add→sub の順、順序非依存）。
        // 未指定なら全面 true＝従来の矩形と完全一致（後方互換）。
        HashSet<(int x, int z)> foot = BuildFootprint(spec, w, d);
        // マスクが矩形一杯（全 w*d セル）かどうか。非矩形なら屋根・様式を安全側へ寄せる。
        bool rectangular = IsRectangular(foot, w, d);

        // 座標 -> ブロックID。後勝ち（開口部で上書きするため）。
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 床（y=0、マスク内のみ）
        foreach (var (x, z) in foot)
            cells[(x, 0, z)] = floor;

        // 土台段（base course）: y=0 のマスク縁一周を土台材に差し替える。
        // 未指定なら floor と同じ＝従来の見た目（差し替えても影響なし）。座標系は変えない。
        string baseBlock = Pick(spec.BaseBlock, allowedBlocks, floor);
        if (spec.HasBase)
        {
            foreach (var (x, z) in foot)
                if (IsEdge(foot, x, z))
                    cells[(x, 0, z)] = baseBlock;
        }

        // 屋根（roof_type で分岐）。非矩形フットプリントのときは gable/dome/pyramid が
        // 矩形前提のため崩れる。安全側として flat にフォールバックし、平屋根をマスクに沿わせる。
        string roofType = (spec.RoofType ?? "flat").Trim().ToLowerInvariant();
        if (!rectangular)
            roofType = "flat";
        if (roofType == "gable")
            BuildGableRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "gable_stairs")
            BuildGableStairsRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "shed")
            BuildShedRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "dome")
            BuildDomeRoof(cells, spec, w, d, h, roof);
        else if (roofType == "pyramid")
            BuildPyramidRoof(cells, w, d, h, roof);
        else
            BuildFlatRoof(cells, foot, h, roof);

        // 煙突。屋根生成の後に呼ぶ（各列の屋根の実際の最高yを見て、そこから上へ積むため）。
        // 本数0なら何もしない。素材は chimney_block → roof → wall の順で流用。
        if (spec.ChimneyCount > 0)
        {
            string chimney = Pick(spec.ChimneyBlock, allowedBlocks, roof);
            BuildChimney(cells, spec, w, d, h, chimney);
        }

        // 建物様式。colonnade/temple は矩形前提（柱の等間隔配置・柱廊）なので、
        // 非矩形フットプリントのときは walled（壁のリング）へフォールバックする。
        string buildingStyle = (spec.BuildingStyle ?? "walled").Trim().ToLowerInvariant();
        if (!rectangular)
            buildingStyle = "walled";

        if (buildingStyle == "colonnade")
        {
            // 開放型: 壁を立てず、外周の角＋等間隔の位置に円柱を立てる（神殿風）。
            BuildColonnade(cells, w, d, h, wall);
        }
        else if (buildingStyle == "temple")
        {
            // ファサード型: 指定面に柱廊、奥に壁の部屋。柱は範囲内に収める。
            string accentT = Pick(spec.AccentBlock, allowedBlocks, wall);
            BuildTemple(cells, w, d, h, wall, accentT, spec.FacadeFace ?? "south");
        }
        else
        {
            // アクセント材（柱型リズム用）。未指定なら wall と同じ＝従来の見た目。
            string accent = Pick(spec.AccentBlock, allowedBlocks, wall);
            // 柱なし(0)はそのまま尊重。柱ありの場合は最低4間隔を強制して密集を防ぐ。
            int pilasterStep = 0;
            if (spec.PilasterStep.HasValue && spec.PilasterStep.Value >= 2)
                pilasterStep = Math.Max(4, spec.PilasterStep.Value);

            // 壁（中間層 y=1..h-2 の外周リングのみ）。
            // マスクの縁(IsEdge)にだけ立てるので、L字・コの字でも内側角まで正しく回る。
            for (int y = 1; y <= h - 2; y++)
                foreach (var (x, z) in foot)
                {
                    if (!IsEdge(foot, x, z)) continue;

                    // 角判定・柱リズムは矩形のときだけ従来どおり適用する。
                    // 非矩形では角の定義が曖昧なので、縁は一律 wall（アクセントなし）にする。
                    bool useAccent = false;
                    if (rectangular)
                    {
                        bool isCorner = (x == 0 || x == w - 1) && (z == 0 || z == d - 1);
                        bool isPilaster = pilasterStep > 0 &&
                            ((x == 0 || x == w - 1) ? (z % pilasterStep == 0)
                                                    : (x % pilasterStep == 0));
                        useAccent = isCorner || isPilaster;
                    }
                    cells[(x, y, z)] = useAccent ? accent : wall;
                }
        }

        // 中間床（複数階）。指定された各 y にマスク内の全面の床を敷く。
        foreach (int fy in (spec.FloorLevels ?? new List<int>()).Distinct())
        {
            // 1階の床(0)・屋根の領域(h-1以上)とぶつかる指定は無視
            if (fy <= 0 || fy >= h - 1) continue;
            foreach (var (x, z) in foot)
                cells[(x, fy, z)] = floor;
        }

        // 開口部の適用（中間床より後。床に窓・ドアが指定されても壁セルのみ作用するので安全）
        // colonnade（開放型）は壁がないので開口部は適用しない。
        // 注意: 現状の ApplyOpening は矩形外周（x=0/w-1, z=0/d-1）を前提とするため、
        //       非矩形フットプリントでは開口が壁セルに当たらず無視されることがある。
        //       非矩形向けの開口スナップは次フェーズで対応する。
        if (buildingStyle != "colonnade")
        {
            var ops = spec.Openings ?? new List<Opening>();
            foreach (var op in ops)
                ApplyOpening(cells, op, w, d, h, allowedBlocks);

            // 入口の保証: door が1つも指定されていない場合、正面(facade_face、既定 south)の
            // 中央に自動でドアを1つ開ける。LLM がドアを出さなくても必ず入口ができる。
            bool hasDoor = ops.Any(o =>
                string.Equals((o.Kind ?? "").Trim(), "door", StringComparison.OrdinalIgnoreCase));
            if (!hasDoor)
            {
                string doorFace = (spec.FacadeFace ?? "south").Trim().ToLowerInvariant();
                if (doorFace != "north" && doorFace != "south" &&
                    doorFace != "east" && doorFace != "west")
                    doorFace = "south";
                // 面の中央を offset にする。south/north は x 方向、east/west は z 方向。
                int centerOffset = (doorFace == "south" || doorFace == "north")
                    ? w / 2 : d / 2;
                ApplyOpening(cells,
                    new Opening { Face = doorFace, Kind = "door", Offset = centerOffset, Level = 1 },
                    w, d, h, allowedBlocks);
            }
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }

    // ===== フットプリント（平面形状マスク）=====
    // spec の footprint 指定から、建てる平面(X-Z)のマスクを確定的に作る。
    // 手順: プリセット形状 → footprint_add をすべて OR → footprint_sub をすべて減算。
    // add をすべて足してから sub をすべて引くので、add 同士・sub 同士の順序は結果に影響しない。
    // 未指定（shape=null かつ add/sub 空）なら全面 true＝従来の矩形と完全一致。
    private static HashSet<(int x, int z)> BuildFootprint(StructureSpec spec, int w, int d)
    {
        var mask = new HashSet<(int x, int z)>();

        string shape = (spec.FootprintShape ?? "rect").Trim().ToLowerInvariant();
        int cutW = spec.FootprintParams?.CutW ?? 0;
        int cutD = spec.FootprintParams?.CutD ?? 0;
        // 未指定(0以下)なら幅・奥行のおよそ半分を既定の切り欠き量にする。
        if (cutW <= 0) cutW = Math.Max(1, w / 2);
        if (cutD <= 0) cutD = Math.Max(1, d / 2);
        // 切り欠きが全体を食い尽くさないよう上限を掛ける。
        cutW = Clamp(cutW, 1, Math.Max(1, w - 1));
        cutD = Clamp(cutD, 1, Math.Max(1, d - 1));

        // 1) プリセットで大枠を作る。
        switch (shape)
        {
            case "l":
                // L字: 右奥(x大・z大)の cutW×cutD の一角を削る。
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        if (!(x >= w - cutW && z >= d - cutD))
                            mask.Add((x, z));
                break;

            case "u":
                // コの字: 手前(z大側)の中央を幅 cutW・深さ cutD で削り込む。
                {
                    int lo = (w - cutW) / 2;
                    int hi = lo + cutW - 1;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                            if (!(x >= lo && x <= hi && z >= d - cutD))
                                mask.Add((x, z));
                }
                break;

            case "t":
                // T字: z 小側に横棒（全幅・厚み cutD）、中央に縦棒（幅 cutW・全奥行）。
                {
                    int lo = (w - cutW) / 2;
                    int hi = lo + cutW - 1;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                        {
                            bool bar = z < cutD;                 // 横棒
                            bool stem = (x >= lo && x <= hi);    // 縦棒
                            if (bar || stem) mask.Add((x, z));
                        }
                }
                break;

            case "plus":
                // 十字: 中央縦帯（幅 cutW）＋中央横帯（厚み cutD）。
                {
                    int xlo = (w - cutW) / 2, xhi = xlo + cutW - 1;
                    int zlo = (d - cutD) / 2, zhi = zlo + cutD - 1;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                        {
                            bool vBand = (x >= xlo && x <= xhi);
                            bool hBand = (z >= zlo && z <= zhi);
                            if (vBand || hBand) mask.Add((x, z));
                        }
                }
                break;

            default: // "rect" ほか未知の値は矩形一杯（従来互換）。
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        mask.Add((x, z));
                break;
        }

        // 2) footprint_add をすべて OR で足す（順序非依存）。
        foreach (var r in spec.FootprintAdd ?? new List<Rect>())
            AddRect(mask, r, w, d, add: true);

        // 3) footprint_sub をすべて減算する（add 完了後に一括、順序非依存）。
        foreach (var r in spec.FootprintSub ?? new List<Rect>())
            AddRect(mask, r, w, d, add: false);

        // 空マスク（全部削られた等）になったら安全側で矩形一杯へ戻す。宙抜け生成を防ぐ。
        if (mask.Count == 0)
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    mask.Add((x, z));

        return mask;
    }

    // 矩形 r を建物範囲(0..w-1, 0..d-1)にクランプして、マスクへ加算/減算する。
    private static void AddRect(HashSet<(int x, int z)> mask, Rect r, int w, int d, bool add)
    {
        int x0 = Clamp(r.X, 0, w - 1);
        int z0 = Clamp(r.Z, 0, d - 1);
        int x1 = Clamp(r.X + Math.Max(0, r.W) - 1, 0, w - 1);
        int z1 = Clamp(r.Z + Math.Max(0, r.D) - 1, 0, d - 1);
        if (r.W <= 0 || r.D <= 0) return;
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (add) mask.Add((x, z));
                else mask.Remove((x, z));
    }

    // マスクが矩形一杯（全 w*d セルが埋まっている）か。true なら従来の矩形と同一。
    private static bool IsRectangular(HashSet<(int x, int z)> mask, int w, int d)
        => mask.Count == w * d;

    // 指定セルがマスクの「縁」か。4近傍(±x, ±z)のいずれかがマスク外なら縁とみなす。
    // マスク外セルに対しては false。壁・土台をここで判定するので、L字の内側角も正しく回る。
    private static bool IsEdge(HashSet<(int x, int z)> mask, int x, int z)
    {
        if (!mask.Contains((x, z))) return false;
        return !mask.Contains((x + 1, z))
            || !mask.Contains((x - 1, z))
            || !mask.Contains((x, z + 1))
            || !mask.Contains((x, z - 1));
    }

    // 平屋根: 最上層 y=h-1 をマスク内で塞ぐ（フットプリントに沿う）。
    private static void BuildFlatRoof(
        Dictionary<(int x, int y, int z), string> cells, HashSet<(int x, int z)> foot, int h, string roof)
    {
        foreach (var (x, z) in foot)
            cells[(x, h - 1, z)] = roof;
    }

    // 煙突: 屋根の上に本数ぶん自動で等間隔に立てる。位置は寄せ方向(chimney_align)で決める。
    //   center（既定）… 建物の中心線上に、x軸に沿って等間隔で並ぶ。
    //   north/south   … その面寄り（z を端側へ）に寄せ、x軸に沿って並ぶ。
    //   east/west     … その面寄り（x を端側へ）に寄せ、z軸に沿って並ぶ。
    // 各煙突の(x,z)で、既に積まれた屋根の「その列の最大y」を調べ、そこから上へ
    // chimney_height マス積む。貫通ON(chimney_pierce)なら床上(y=1)から屋根を貫いて通す。
    private static void BuildChimney(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string chimney)
    {
        int count = spec.ChimneyCount;
        if (count <= 0) return;

        int stackH = spec.ChimneyHeight.HasValue && spec.ChimneyHeight.Value > 0
            ? spec.ChimneyHeight.Value : 2;

        string align = (spec.ChimneyAlign ?? "center").Trim().ToLowerInvariant();

        // 端から少し内側に寄せる余白（角に食い込ませない）。
        int margin = 1;

        // 並ぶ軸(along)と、寄せる固定座標を決める。
        // north/south は z を固定して x 方向に並ぶ。east/west は x を固定して z 方向に並ぶ。
        bool alongX; // true: x方向に並ぶ, false: z方向に並ぶ
        int fixedCoord; // 並ぶ軸に直交する側の固定値

        switch (align)
        {
            case "north": alongX = true; fixedCoord = margin; break;                 // z=手前寄り
            case "south": alongX = true; fixedCoord = d - 1 - margin; break;          // z=奥寄り
            case "west": alongX = false; fixedCoord = margin; break;                  // x=左寄り
            case "east": alongX = false; fixedCoord = w - 1 - margin; break;          // x=右寄り
            default: alongX = true; fixedCoord = (d - 1) / 2; break;                  // center: z中央、x方向
        }
        if (fixedCoord < 0) fixedCoord = 0;

        // 並ぶ軸の有効範囲（角を避けた内側）。
        int span = alongX ? w : d;
        int lo = margin, hi = span - 1 - margin;
        if (hi < lo) { lo = 0; hi = span - 1; }

        int n = Math.Min(count, Math.Max(1, hi - lo + 1));

        for (int i = 0; i < n; i++)
        {
            int p = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)System.Math.Round((double)(hi - lo) * i / (n - 1));

            int cx = alongX ? p : fixedCoord;
            int cz = alongX ? fixedCoord : p;
            if (cx < 0 || cx >= w || cz < 0 || cz >= d) continue;

            // その(x,z)列の屋根の最高y（既に cells に積まれた最大y）。無ければ壁上端 h-1。
            int topY = h - 1;
            foreach (var k in cells.Keys)
                if (k.x == cx && k.z == cz && k.y > topY) topY = k.y;

            // 積み始めのy。貫通ONは床上(y=1)から、OFFは屋根上端の1つ上から。
            int startY = spec.ChimneyPierce ? 1 : topY + 1;

            // 煙突頂上 = 屋根上端 + stackH。
            int endY = topY + stackH;

            for (int y = startY; y <= endY; y++)
                cells[(cx, y, cz)] = chimney;
        }
    }

    // ピラミッド屋根（四角錐）: 底面(w×d)を y=h-1 に全面で敷き、そこから上へ
    // 1段ごとに全周を1マスずつ内側へ絞りながら積む。頂点で1〜2マスに収束する。
    // pyramids（建物全体を四角錐にしたいとき）や塔・東洋風の屋根に使える。
    private static void BuildPyramidRoof(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int h, string roof)
    {
        // 底面（壁の最上層の上）を天井として全面塞ぐ。錐の足元の穴を防ぐ。
        int baseY = h - 1;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, baseY, z)] = roof;

        // 段ごとに内側へ絞る。step マスだけ各辺から内側に入った矩形リング（中身も塗る）。
        // 頂点まで積めるよう、絞り切るまで層を重ねる。
        int maxStep = (Math.Min(w, d) + 1) / 2; // これ以上絞ると矩形が消える
        for (int step = 1; step <= maxStep; step++)
        {
            int x0 = step, x1 = w - 1 - step;
            int z0 = step, z1 = d - 1 - step;
            if (x1 < x0 || z1 < z0) break; // 絞り切った（頂点に到達）

            int y = baseY + step;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, y, z)] = roof;
        }
    }

    // 切妻屋根: 棟の向き(ridge_axis)に沿って段々に三角を作る。
    // 屋根は本体の上(y=h から上)に積む。傾斜方向の端から中央へ向け段を上げる。
    // 勾配(spec.RoofPitch)は run 何マスにつき rise 1マス上げるか。
    // 1(既定)=1マスで1段=45°で従来と完全一致。2以上で緩勾配になる。
    private static void BuildGableRoof(
    Dictionary<(int x, int y, int z), string> cells,
    StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        // 棟がx軸に平行 → z方向に傾斜（zの端から中央へ高くなる）
        // 棟がz軸に平行 → x方向に傾斜（xの端から中央へ高くなる）
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w; // 傾斜する方向の長さ
        // 端から中央までの段数。中央で最も高い。
        int half = (slopeLen + 1) / 2;

        // 勾配。null/0/1 は 1（45°・従来どおり）。大きいほど緩い（1〜4に制限）。
        int pitch = spec.RoofPitch.HasValue && spec.RoofPitch.Value >= 1
            ? System.Math.Min(4, spec.RoofPitch.Value)
            : 1;

        for (int i = 0; i < slopeLen; i++)
        {
            // 端(0 と slopeLen-1)が低く、中央が高い。端からの距離を勾配で割って段数にする。
            // pitch=1 なら距離そのまま(=45°、従来と一致)。pitch が大きいほど段が緩くなる。
            int dist = System.Math.Min(i, slopeLen - 1 - i);
            int step = dist / pitch;
            int yLevel = (h - 1) + step; // 壁の最上層と同じ高さ(y=h-1)から積む

            if (ridgeAlongX)
            {
                // 屋根: z=i の列。棟方向(x)は全幅に渡って同じ高さ。
                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = roof;

                // 妻壁: 妻側の面(x=0 と x=w-1)で、壁の上端(y=h-1)から
                //       この列の屋根の手前(yLevel-1)までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // 屋根: x=i の列。棟方向(z)は全奥行きに渡って同じ高さ。
                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = roof;

                // 妻壁: 妻側の面(z=0 と z=d-1)で、壁の上端からこの列の屋根の手前までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }
    }

    // 片流れ屋根(shed/skillion): 一方の端から反対端へ一直線に上がる非対称の傾斜。
    // 棟の向き(ridge_axis)に直交する方向へ傾く。gable と同じ座標系・妻壁充填を流用する。
    // 勾配は RoofPitch(1..4)。pitch マスの水平移動につき1段上がる（pitch=1 で45度）。
    private static void BuildShedRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        // 棟がx軸に平行 → z方向に傾斜（z=0 が低く、z=d-1 へ向け高くなる）
        // 棟がz軸に平行 → x方向に傾斜（x=0 が低く、x=w-1 へ向け高くなる）
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w; // 傾斜する方向の長さ

        // 勾配: RoofPitch 未指定は1(=45度)。大きいほど緩い。1..4にクランプ。
        int pitch = spec.RoofPitch ?? 1;
        if (pitch < 1) pitch = 1;
        if (pitch > 4) pitch = 4;

        for (int i = 0; i < slopeLen; i++)
        {
            // 端(i=0)が最も低く、反対端(i=slopeLen-1)へ一直線に上がる。
            // gable の Min(i, slopeLen-1-i) と違い、距離 i をそのまま使うのが片流れ。
            int step = i / pitch;
            int yLevel = (h - 1) + step; // 壁の最上層(y=h-1)から積む

            if (ridgeAlongX)
            {
                // 屋根: z=i の列。棟方向(x)は全幅に渡って同じ高さ。
                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = roof;

                // 妻壁: 傾斜に直交する2面(x=0 と x=w-1)を、壁の上端(y=h-1)から
                //       この列の屋根の手前(yLevel-1)まで埋める。左右で高さが変わり階段状に閉じる。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // 屋根: x=i の列。棟方向(z)は全奥行きに渡って同じ高さ。
                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = roof;

                // 妻壁: 傾斜に直交する2面(z=0 と z=d-1)を埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }
    }

    // 切妻屋根（階段ブロック版）: 各段の屋根面を階段ブロックにし、
    // 傾斜の下り方向へ facing を向ける。棟（最上段）はフルブロックで尖らせる。
    // roof には階段ブロックID（例: minecraft:oak_stairs）が渡る想定。
    // 状態は id に "[facing=...,half=bottom]" を埋め込む（プレビューは baseId で判定）。
    private static void BuildGableStairsRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w;
        int half = (slopeLen + 1) / 2;

        // 棟の位置（傾斜方向の中央）。ここはフルブロックで尖らせる。
        int ridgeLo = (slopeLen - 1) / 2;
        int ridgeHi = slopeLen / 2;

        for (int i = 0; i < slopeLen; i++)
        {
            int step = System.Math.Min(i, slopeLen - 1 - i);
            int yLevel = (h - 1) + step;

            bool isRidge = (i == ridgeLo || i == ridgeHi);
            // 軒側へ下る向き。前半(i<中央)は一方向、後半は逆向き。
            bool lowerSide = (i < slopeLen / 2); // 端0側か

            if (ridgeAlongX)
            {
                // 階段の facing: z方向に傾斜。下り側を向く。
                // 端0側は south(z+方向へ下る)を向く＝facing=south、反対側は north。
                string facing = lowerSide ? "south" : "north";
                string block = isRidge ? roof : StairId(roof, facing);

                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = block;

                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // x方向に傾斜。端0側は east(x+方向へ下る)、反対側は west。
                string facing = lowerSide ? "east" : "west";
                string block = isRidge ? roof : StairId(roof, facing);

                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = block;

                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }
    }

    // 階段ブロックの id に向き状態を埋め込む。素材が階段でない場合もIDだけ付ける
    // （Minecraft側で無効なら無視されるだけ。基本は roof に *_stairs を選ばせる）。
    private static string StairId(string block, string facing)
        => $"{block}[facing={facing},half=bottom]";


    // ドーム屋根: 建物の上面(w×d)を底面とする半楕円体の殻を、y=h-1 から上に積む。
    // 半径 rx=w/2, rz=d/2。ドーム高さ ry は spec.DomeHeight（未指定なら控えめな既定）。
    // 殻だけ残す（中空）ことで屋根らしくする。底面 y=h-1 は全面塞いで天井とする。
    private static void BuildDomeRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof)
    {
        double rx = w / 2.0;
        double rz = d / 2.0;
        // 中心（底面）。整数格子の中央。
        double cx = (w - 1) / 2.0;
        double cz = (d - 1) / 2.0;

        // ドームの高さ。未指定なら水平半径の小さい方に合わせる（半球に近い）。
        int ry = spec.DomeHeight.HasValue && spec.DomeHeight.Value >= 1
            ? spec.DomeHeight.Value
            : Math.Max(2, (int)Math.Round(Math.Min(rx, rz)));

        int baseY = h - 1; // ドームの底面（壁の最上層の上）

        // まず底面を天井として全面塞ぐ（ドームの足元の穴を防ぐ）
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, baseY, z)] = roof;

        // 半楕円体の殻。yLayer=0..ry の各層で、その高さの輪郭リングを置く。
        for (int yi = 0; yi <= ry; yi++)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                {
                    double nx = (x - cx) / (rx <= 0 ? 1 : rx);
                    double nz = (z - cz) / (rz <= 0 ? 1 : rz);
                    double ny = (double)yi / (ry <= 0 ? 1 : ry);
                    double v = nx * nx + nz * nz + ny * ny;
                    if (v > 1.0) continue; // 楕円体の外

                    // 殻判定: 隣接が外側になるセルだけ残す（表面）
                    bool surface =
                        Outside(x + 1, cx, rx, z, cz, rz, yi, ry) ||
                        Outside(x - 1, cx, rx, z, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z + 1, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z - 1, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z, cz, rz, yi + 1, ry);
                    if (!surface) continue;

                    cells[(x, baseY + yi, z)] = roof;
                }
        }
    }

    // 円柱を1本立てる純粋な部品。中心(cx,cz)、半径r、y=yFrom..yTo に各層 半径rの円を置く。
    private static void BuildColumn(
        Dictionary<(int x, int y, int z), string> cells,
        int cx, int cz, int r, int yFrom, int yTo, string block, int w, int d)
    {
        for (int y = yFrom; y <= yTo; y++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    // 円の内側か（半径rの塗りつぶし円）
                    if (dx * dx + dz * dz > r * r) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || x >= w || z < 0 || z >= d) continue; // 建物範囲内のみ
                    cells[(x, y, z)] = block;
                }
    }

    // 開放型（列柱）: 外周の角＋等間隔の位置に円柱を立てる。
    // 柱の太さ(半径)は高さで決め、建物の幅・奥行きが小さければ抑える。
    private static void BuildColonnade(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int h, string col)
    {
        // 柱の太さ: 高さで段階的に。小さい建物には太すぎないよう幅奥行の1/4で上限。
        int rByHeight = h < 10 ? 1 : (h < 18 ? 2 : 3);
        int rByFootprint = Math.Max(1, Math.Min(w, d) / 5);
        int r = Math.Min(rByHeight, rByFootprint);

        // 柱の中心が建物範囲に収まるよう、端からr内側に置く。
        int lo = r, hiX = w - 1 - r, hiZ = d - 1 - r;
        if (hiX < lo) hiX = lo;
        if (hiZ < lo) hiZ = lo;

        // 柱を立てる高さ範囲（床のすぐ上〜屋根の下）。
        int yTop = h - 2;
        if (yTop < 1) yTop = 1;

        // 柱の間隔: 寸法から自動（柱の直径＋2マスの隙間を目安）。最低3。
        int step = Math.Max(4, r * 2 + 3);

        // 柱を立てる位置（x座標群・z座標群）を等間隔で集める。両端は必ず含む。
        var xs = AxisPositions(lo, hiX, step);
        var zs = AxisPositions(lo, hiZ, step);

        // 外周（最初と最後のxまたはz）にあたる位置にだけ柱を立てる。
        foreach (int cxp in xs)
            foreach (int czp in zs)
            {
                bool onPerimeter =
                    cxp == xs.First() || cxp == xs.Last() ||
                    czp == zs.First() || czp == zs.Last();
                if (!onPerimeter) continue;
                BuildColumn(cells, cxp, czp, r, 1, yTop, col, w, d);
            }
    }

    // ファサード型（temple）: 指定された面(facadeFace)に柱廊、その奥に壁の部屋。
    // 柱は建物範囲内に収める（張り出さない）。柱廊と部屋の間に gap マスの空きを設け、
    // 柱廊側の壁の中央に縦2マスの入口を空ける。
    private static void BuildTemple(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, string wall, string col, string facadeFace)
    {
        int rByHeight = h < 10 ? 1 : (h < 18 ? 2 : 3);
        int r = Math.Max(1, rByHeight);

        int yTop = h - 2;
        if (yTop < 1) yTop = 1;
        int step = Math.Max(4, r * 2 + 3);

        int gap = r * 2 + 1;

        string face = (facadeFace ?? "south").Trim().ToLowerInvariant();
        bool frontAlongX = (face == "south" || face == "north");

        if (frontAlongX)
        {
            r = Math.Min(r, Math.Max(1, w / 5));
            int lo = r, hi = w - 1 - r;
            if (hi < lo) hi = lo;

            int rzLo, rzHi, doorZ;
            if (face == "south")
            {
                rzLo = 0;
                rzHi = Math.Max(rzLo + 1, d - 1 - gap - 1);
                doorZ = rzHi; // 柱廊に面した壁
            }
            else
            {
                rzHi = d - 1;
                rzLo = Math.Min(rzHi - 1, gap + 1);
                doorZ = rzLo; // 柱廊に面した壁
            }

            for (int y = 1; y <= h - 2; y++)
                for (int x = 0; x < w; x++)
                    for (int z = rzLo; z <= rzHi; z++)
                        if (x == 0 || x == w - 1 || z == rzLo || z == rzHi)
                            cells[(x, y, z)] = wall;

            // 入口: 柱廊側の壁(z=doorZ)の中央に縦2マスの開口。
            int doorX = w / 2;
            cells.Remove((doorX, 1, doorZ));
            if (h - 2 >= 2) cells.Remove((doorX, 2, doorZ));

            int frontZ = (face == "south") ? d - 1 - r : r;
            if (frontZ < 0) frontZ = 0;
            if (frontZ > d - 1) frontZ = d - 1;

            foreach (int cxp in AxisPositions(lo, hi, step))
                BuildColumn(cells, cxp, frontZ, r, 1, yTop, col, w, d);
        }
        else
        {
            r = Math.Min(r, Math.Max(1, d / 5));
            int lo = r, hi = d - 1 - r;
            if (hi < lo) hi = lo;

            int rxLo, rxHi, doorX2;
            if (face == "east")
            {
                rxLo = 0;
                rxHi = Math.Max(rxLo + 1, w - 1 - gap - 1);
                doorX2 = rxHi;
            }
            else
            {
                rxHi = w - 1;
                rxLo = Math.Min(rxHi - 1, gap + 1);
                doorX2 = rxLo;
            }

            for (int y = 1; y <= h - 2; y++)
                for (int x = rxLo; x <= rxHi; x++)
                    for (int z = 0; z < d; z++)
                        if (z == 0 || z == d - 1 || x == rxLo || x == rxHi)
                            cells[(x, y, z)] = wall;

            // 入口: 柱廊側の壁(x=doorX2)の中央に縦2マスの開口。
            int doorZ2 = d / 2;
            cells.Remove((doorX2, 1, doorZ2));
            if (h - 2 >= 2) cells.Remove((doorX2, 2, doorZ2));

            int frontX = (face == "east") ? w - 1 - r : r;
            if (frontX < 0) frontX = 0;
            if (frontX > w - 1) frontX = w - 1;

            foreach (int czp in AxisPositions(lo, hi, step))
                BuildColumn(cells, frontX, czp, r, 1, yTop, col, w, d);
        }
    }
    // lo..hi を step 間隔で並べた位置リスト（両端を必ず含む）。
    // lo..hi に柱を均等配置する。両端を必ず含み、柱同士が step 以上離れるよう
    // 本数を決めてから均等割りするので、端数で最後の柱が詰まることがない。
    private static List<int> AxisPositions(int lo, int hi, int step)
    {
        var list = new List<int>();
        if (hi <= lo) { list.Add(lo); return list; }

        int span = hi - lo;
        // 端から端までに入る「区間数」。step 以上の間隔を保てる最大数。
        int segments = Math.Max(1, span / step);
        // segments+1 本の柱を等間隔に置く（両端含む）。
        for (int i = 0; i <= segments; i++)
        {
            int v = lo + (int)Math.Round((double)span * i / segments);
            if (list.Count == 0 || list.Last() != v) list.Add(v);
        }
        return list;
    }

    // 指定セルが半楕円体の外側か（殻判定用）
    private static bool Outside(int x, double cx, double rx, int z, double cz, double rz, int yi, int ry)
    {
        double nx = (x - cx) / (rx <= 0 ? 1 : rx);
        double nz = (z - cz) / (rz <= 0 ? 1 : rz);
        double ny = (double)yi / (ry <= 0 ? 1 : ry);
        return (nx * nx + nz * nz + ny * ny) > 1.0;
    }

    private static void ApplyOpening(
        Dictionary<(int x, int y, int z), string> cells,
        Opening op, int w, int d, int h, IReadOnlyList<string> allowedBlocks)
    {
        string face = (op.Face ?? "").Trim().ToLowerInvariant();
        string kind = (op.Kind ?? "").Trim().ToLowerInvariant();
        bool isDoor = kind == "door";
        bool isArch = kind == "arch";
        bool isWindow = !isDoor && !isArch;

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
            // アーチ: 中央列を高く抜き、左右1マスずつは1段低く抜いて上端を丸める。
            //   中央列: y=1 .. archTop を開口
            //   左右列: y=1 .. archTop-1 を開口（上端を内側へ詰めて曲線風に）
            // archTop は壁の高さに収める（最上段=屋根の手前 h-2 まで）。
            int wallTop = Math.Max(1, h - 2);
            int archTop = Math.Min(wallTop, 3); // 標準的なアーチ高（3段）。低い壁では自動で縮む。
            int cx = target2.Value.x, cz = target2.Value.z;

            // 中央列を抜く。
            for (int yy = 1; yy <= archTop; yy++)
                cells.Remove((cx, yy, cz));

            // 左右の列（offset±1）を1段低く抜く。壁セルのときのみ。
            for (int side = -1; side <= 1; side += 2)
            {
                int sx = alongX ? cx + side : cx;
                int sz = alongX ? cz : cz + side;
                // 開口が壁の外周面からはみ出さないよう、その面上の有効範囲かを確認する。
                bool inRange = alongX ? (sx >= 0 && sx < w) : (sz >= 0 && sz < d);
                if (!inRange) continue;
                for (int yy = 1; yy <= Math.Max(1, archTop - 1); yy++)
                {
                    var sk = (sx, yy, sz);
                    if (cells.ContainsKey(sk)) cells.Remove(sk);
                }
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

    // スロープ（坂道）: ridge_axis で傾斜方向を選ぶ。
    //   ridge_axis="x"（既定）→ x方向に進むほど高くなる（z方向に幅）。
    //   ridge_axis="z"        → z方向に進むほど高くなる（x方向に幅）。
    // 進行方向の各位置で、床から「その位置の目標高さ」までを body で満たす中実スロープ。
    // 下を base、踏面（各段の最上面）も含めて中身を詰めるので、宙に浮かず歩いて登れる。
    // 高さは進行方向の長さに合わせて h-1 段まで一定割合で上げる。
    private static List<GeneratedBlock> BuildRamp(
        int w, int d, int h, string body, string baseBlock, string? ridgeAxis)
    {
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 進行方向（傾斜が上がる向き）。"z" 指定時のみ z 方向、それ以外は x 方向。
        bool runAlongX = (ridgeAxis ?? "x").Trim().ToLowerInvariant() != "z";

        int runLen = runAlongX ? w : d; // 傾斜方向の長さ
        int crossLen = runAlongX ? d : w; // 幅方向の長さ
        int topY = h - 1; // 最大の高さ（最上段の y）

        for (int i = 0; i < runLen; i++)
        {
            // 進行位置 i に対する目標高さ。i=0 で 0 段、i=runLen-1 で topY 段。
            // 1マス進むごとに段が上がる比率を、長さと高さから線形に決める。
            int levelY = (runLen <= 1)
                ? topY
                : (int)System.Math.Round((double)topY * i / (runLen - 1));

            for (int c = 0; c < crossLen; c++)
            {
                int x = runAlongX ? i : c;
                int z = runAlongX ? c : i;

                // 床から levelY までを中実に満たす。最下段(y=0)は base、上は body。
                for (int y = 0; y <= levelY; y++)
                    cells[(x, y, z)] = (y == 0) ? baseBlock : body;
            }
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }

    // 橋（桁橋＋橋脚＋欄干）: ridge_axis で渡す向きを選ぶ。
    //   ridge_axis="x"（既定）→ 橋は x 方向に渡る（z 方向に幅）。
    //   ridge_axis="z"        → 橋は z 方向に渡る（x 方向に幅）。
    // 構成:
    //   ・路面(deck): 高さ deckY に進行方向いっぱいの水平面を敷く。歩いて渡れる。
    //   ・橋脚(pier): 進行方向に等間隔の数か所で、路面の下を地面(y=0)まで柱で支える。
    //   ・欄干(rail): 路面の両縁(幅方向の端)に高さ1の手すりを立てる。橋らしさを出す。
    // deckY は h-1 とし、橋脚が地面から路面まで届く。幅が2未満なら欄干は省く。
    private static List<GeneratedBlock> BuildBridge(
        int w, int d, int h, string deck, string pier, string? ridgeAxis)
    {
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 渡る向き。"z" 指定時のみ z 方向、それ以外は x 方向。
        bool runAlongX = (ridgeAxis ?? "x").Trim().ToLowerInvariant() != "z";

        int runLen = runAlongX ? w : d;   // 渡る方向の長さ（スパン）
        int crossLen = runAlongX ? d : w; // 幅方向の長さ
        int deckY = h - 1;                // 路面の高さ（最上段）

        // 進行方向 i・幅方向 c を実座標(x,z)へ変換するローカル関数。
        (int x, int z) ToXz(int i, int c) => runAlongX ? (i, c) : (c, i);

        // 路面: deckY に幅いっぱいの水平面。
        for (int i = 0; i < runLen; i++)
            for (int c = 0; c < crossLen; c++)
            {
                var (x, z) = ToXz(i, c);
                cells[(x, deckY, z)] = deck;
            }

        // 橋脚: 進行方向に等間隔の数か所で、路面の下(y=0..deckY-1)を柱で支える。
        // 本数は概ね4マスごと。両端は必ず脚を置いて橋台にする。
        int pierStep = Math.Max(4, runLen / 4);
        var pierPositions = AxisPositions(0, runLen - 1, pierStep);
        foreach (int i in pierPositions)
            for (int c = 0; c < crossLen; c++)
            {
                var (x, z) = ToXz(i, c);
                for (int y = 0; y < deckY; y++)
                    cells[(x, y, z)] = pier;
            }

        // 欄干: 路面の両縁(幅方向の端 c=0 と c=crossLen-1)に高さ1の手すり。
        // 幅が2未満なら省略（路面と重なってしまうため）。
        if (crossLen >= 2)
        {
            for (int i = 0; i < runLen; i++)
            {
                var (xa, za) = ToXz(i, 0);
                var (xb, zb) = ToXz(i, crossLen - 1);
                cells[(xa, deckY + 1, za)] = pier;
                cells[(xb, deckY + 1, zb)] = pier;
            }
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }
}
