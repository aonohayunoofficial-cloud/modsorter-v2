using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// StructureSpec を確定的に座標へ展開する。
// 壁の外周リングは必ずここで生成するため、塊化や壁抜けは原理的に起きない。
//
// このファイルには生成の入口(Expand)と生成順序(ExpandCore)だけを置く。
// 「どの順で何を上書きするか」が過去の不具合（越屋根の隙間・塔と開口の衝突・
// 軒の高さ）の主因だったため、順序を1ファイルに集約して見通しを保つ。
// 個々の部品は partial の別ファイルに分けてある。
//   StructureExpander.Roof.Basic.cs      平屋根・切妻・階段切妻・片流れ
//   StructureExpander.Roof.Industrial.cs 鋸屋根・越屋根
//   StructureExpander.Roof.Cap.cs        ドーム・四角錐・尖塔
//   StructureExpander.Openings.cs        開口部の適用とスナップ
//   StructureExpander.Parts.cs           軒・縁側・煙突・塔・柱・柱廊・神殿
//   StructureExpander.Footprint.cs       平面マスクと共通小物(Clamp/Pick)
//   StructureExpander.Civil.cs           スロープ・橋（座標を直接返す別系統）
public static partial class StructureExpander
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
        if (structureType == "venue")
        {
            // 屋外会場は VenueExpander が観客席・フィールド・シェル・テントを確定的に作る。
            // 高さ1の板も段状の客席も VenueExpander 側で直接置くので、
            // Clamp(height,2,64) や平屋根の生成には一切かからない。
            return VenueExpander.Build(spec, allowedBlocks, fallback);
        }
        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string floor = Pick(spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string roof = Pick(spec.RoofBlock ?? spec.WallBlock, allowedBlocks, wall);
        // 採光面（鋸屋根・モニター屋根の垂直窓）。未指定ならガラス。
        string glazing = Pick(spec.GlazingBlock ?? "minecraft:glass", allowedBlocks, "minecraft:glass");

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

        // 屋根（roof_type で分岐）。非矩形フットプリントでは棟・軒が矩形前提の屋根
        //（gable / gable_stairs / shed / sawtooth / monitor）が崩れるので flat へ寄せる。
        // 頂冠形（dome / pyramid / spire）はマスクに沿って絞れるため、円形平面のときだけ許す。
        // 円形と認めるのは footprint_shape="circle" かつ add/sub 無指定のときに限る。
        // 欠けた円は輪郭が読めず、ドームが平面の外へ張り出して宙に浮くおそれがある。
        string footShape = (spec.FootprintShape ?? "rect").Trim().ToLowerInvariant();
        bool roundPlan = footShape == "circle"
            && (spec.FootprintAdd == null || spec.FootprintAdd.Count == 0)
            && (spec.FootprintSub == null || spec.FootprintSub.Count == 0);

        string roofType = (spec.RoofType ?? "flat").Trim().ToLowerInvariant();
        bool capRoof = roofType == "dome" || roofType == "pyramid" || roofType == "spire";
        if (!rectangular && !(roundPlan && capRoof))
            roofType = "flat";
        if (roofType == "gable")
            BuildGableRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "gable_stairs")
            BuildGableStairsRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "shed")
            BuildShedRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "sawtooth")
            BuildSawtoothRoof(cells, spec, w, d, h, roof, wall, glazing);
        else if (roofType == "monitor")
            BuildMonitorRoof(cells, spec, w, d, h, roof, wall, glazing);
        else if (roofType == "dome")
            BuildDomeRoof(cells, foot, spec, w, d, h, roof);
        else if (roofType == "pyramid")
            BuildPyramidRoof(cells, foot, h, roof);
        else if (roofType == "spire")
            BuildSpireRoof(cells, foot, spec, h, roof);
        else
            BuildFlatRoof(cells, foot, h, roof);

        // パラペット（陸屋根の立ち上がり）。平屋根のときだけ、屋根面(y=h-1)の外周を
        // その上へ立ち上げる。研究所・倉庫・オフィスなど陸屋根の建物の輪郭を作る。
        // マスクの縁(IsEdge)に沿って回すので、L字・コの字の平面でも内側角まで正しく続く。
        // 勾配屋根では軒先と衝突して破綻するため flat 以外では作らない。
        // parapet_crenel が true なら最上段だけを周期的に抜いて狭間（城の胸壁）にする。
        // 抜くのは最上段のみなので、下に必ず1段以上の環が残り屋根面は外から見えない。
        int parapet = Clamp(spec.ParapetHeight ?? 0, 0, 4);
        if (parapet > 0 && roofType == "flat")
        {
            string parapetBlock = Pick(spec.ParapetBlock, allowedBlocks, wall);
            int crenelStep = spec.ParapetCrenel ? Clamp(spec.ParapetCrenelStep ?? 3, 2, 6) : 0;
            foreach (var (x, z) in foot)
            {
                if (!IsEdge(foot, x, z)) continue;
                for (int py = 1; py <= parapet; py++)
                {
                    if (crenelStep > 0 && py == parapet && IsCrenelGap(foot, x, z, crenelStep))
                        continue;
                    cells[(x, h - 1 + py, z)] = parapetBlock;
                }
            }
        }

        // 塔屋（屋上の機械室・階段室）。平屋根のときだけ、屋根面に壁と天面を持つ
        // 小さな箱を載せる。下の屋根面がそのまま塔屋の床になるので床は作らない。
        // 位置は penthouse_align で決める。x 方向と z 方向の寄せを独立に見るので、
        // "northeast" のような複合指定で4隅寄せになる。
        //   center（既定）… 平面の中央。
        //   north / south … z 方向の端寄せ（north = z 小側、south = z 大側）。
        //   west / east   … x 方向の端寄せ（west = x 小側、east = x 大側）。
        // 寄せたときはパラペットがあるぶん1マス内側に置き、パラペットの環を切らない。
        // 勾配屋根では軒・棟と干渉するため作らない。
        int phH = Clamp(spec.PenthouseHeight ?? 0, 0, 12);
        int phW = Clamp(spec.PenthouseWidth ?? 0, 0, w);
        int phD = Clamp(spec.PenthouseDepth ?? 0, 0, d);
        if (phH > 0 && phW >= 3 && phD >= 3 && roofType == "flat")
        {
            string phBlock = Pick(spec.PenthouseBlock, allowedBlocks, wall);
            string phAlign = (spec.PenthouseAlign ?? "center").Trim().ToLowerInvariant();
            int inset = parapet > 0 ? 1 : 0;

            // 含まれる方角で x・z を別々に決める。両方含めば角寄せ、無ければ中央。
            int px0 = phAlign.Contains("west") ? inset
                    : phAlign.Contains("east") ? w - phW - inset
                    : (w - phW) / 2;
            int pz0 = phAlign.Contains("north") ? inset
                    : phAlign.Contains("south") ? d - phD - inset
                    : (d - phD) / 2;

            // 寄せた結果が平面から出ないよう最後にクランプする。
            px0 = Clamp(px0, 0, Math.Max(0, w - phW));
            pz0 = Clamp(pz0, 0, Math.Max(0, d - phD));

            for (int x = px0; x < px0 + phW; x++)
                for (int z = pz0; z < pz0 + phD; z++)
                {
                    // 非矩形平面では屋根が無い位置に浮かせないよう、マスク内だけに置く。
                    if (!foot.Contains((x, z))) continue;

                    bool edge = x == px0 || x == px0 + phW - 1 ||
                                z == pz0 || z == pz0 + phD - 1;
                    if (edge)
                        for (int py = 1; py < phH; py++)
                            cells[(x, h - 1 + py, z)] = phBlock;

                    cells[(x, h - 1 + phH, z)] = roof;
                }
        }

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
            // no_entrance=true のときは通さない。記念碑・オベリスク・台座のように
            // 穴を開けてはいけない塊で、勝手に壁が抜けるのを防ぐ。
            bool hasDoor = ops.Any(o =>
                string.Equals((o.Kind ?? "").Trim(), "door", StringComparison.OrdinalIgnoreCase));
            if (!hasDoor && !spec.NoEntrance)
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

        // 軒の出（eaves）。flat/gable/shed のときだけ、屋根の軒先を外側へ水平に伸ばす。
        // ここでは負座標(x=-1 等)も一時的に許し、直後の一括シフトで 0 以上へ寄せる。
        int eave = Math.Clamp(spec.EaveOverhang ?? 0, 0, 8);
        if (eave > 0 && rectangular &&
            (roofType == "flat" || roofType == "gable" || roofType == "shed"))
        {
            BuildEaves(cells, foot, spec, w, d, h, roof, roofType, eave);
        }

        // 縁側／基壇の縁（veranda）。平面の外側へ y=0 の床を敷き足す。
        // 深い軒の下に回り縁ができ、寺社の「軒下に縁がある」輪郭になる。
        // 軒と同じく負座標を一時的に許し、直後の一括シフトで 0 以上へ寄せる。
        int veranda = Math.Clamp(spec.VerandaWidth ?? 0, 0, 4);
        if (veranda > 0)
        {
            string verandaBlock = Pick(
                spec.VerandaBlock ?? spec.BaseBlock ?? spec.FloorBlock, allowedBlocks, wall);
            BuildVeranda(cells, foot, w, d, veranda, verandaBlock);
        }

        // 塔（鐘塔・尖塔・ミナレット）。平面内に正方形の塔を立て、屋根より上へ突き出す。
        // 屋根形状を問わないので、切妻の教会・ドームのモスク・陸屋根のどれにも載る。
        // 必ず軒の後に呼ぶ。軒は「その列の屋根の実際の最高y」を走査して高さを決めるため、
        // 先に塔を立てると塔の頂部の高さで軒が張り出して破綻する。
        if ((spec.TowerWidth ?? 0) >= 3 && (spec.TowerHeight ?? 0) >= 1)
        {
            string towerBlock = Pick(spec.TowerBlock ?? spec.WallBlock, allowedBlocks, wall);
            string towerRoofBlock = Pick(spec.TowerRoofBlock ?? spec.RoofBlock, allowedBlocks, roof);
            BuildTower(cells, foot, spec, w, d, h, towerBlock, towerRoofBlock);
        }

        // 全ブロックの最小座標を求め、負のぶんだけ全体をシフトして 0 起点に正規化する。
        // 軒で x=-1/z=-1 が出ても、ここで +eave 相当のシフトがかかり負座標は消える。
        // StructureNbtWriter は負座標を書けないため、この正規化は必須。
        int minX = 0, minZ = 0;
        foreach (var k in cells.Keys)
        {
            if (k.x < minX) minX = k.x;
            if (k.z < minZ) minZ = k.z;
        }
        int shiftX = -minX, shiftZ = -minZ;

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x + shiftX,
                Y = kv.Key.y,
                Z = kv.Key.z + shiftZ,
                Id = kv.Value
            })
            .ToList();
    }
}
