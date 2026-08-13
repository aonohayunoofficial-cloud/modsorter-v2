using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 駅舎。線路が z 方向に走る向きで組む（他の鉄道の小分類と同じ）。
//
// 地平・高架下 … x=0 がホーム側の連絡口、x=width-1 が駅前側の出入口。y=0 が床。
//                改札内は x<gx（ホーム側）。高架下は床版が屋根を兼ねるので、
//                屋根形状は flat 扱いにして壁を床版の直下まで立ち上げる。
// 橋上         … x が線路を横切る向き。y=0 がレール面、height が桁下高さ。
//                出入口は x=0 の1箇所（片側橋上駅舎）。改札内は x>gx で、
//                そこからホーム連絡階段を z 方向へ落とす。x=span-1 は閉じた妻壁。
//
// ===== 実寸の出典 =====
//   コンコース天井高 … みなし規定で3.0m以上。橋上駅舎の実施例は5.5〜6.8m。
//   自由通路の幅員   … 橋上化事業の実施例で4m。通路の有効幅は最低90cm、
//                      人と車いすがすれ違うには140cm以上。
//   自動改札機       … 本体の長さは1.8m級。通路幅は標準550mm／590mm、幅広900mm。
//                      移動等円滑化基準は有効幅90cm以上。1マス=1m では
//                      機械2マス（通行方向）＋通路1マス（幅広2マス）で並べる。
//   エレベーター     … かご内法は幅140cm以上・奥行135cm以上（11人乗り以上）。
//                      壁を含めて3マス角のシャフトになる。
//   階段             … 誘導基準は幅140cm以上・蹴上げ16cm以下・踏面30cm以上（約28度）。
//                      蹴上げ1マス固定なので踏面2マス＝26.6度で合わせる。
//   桁下高さ         … 建築限界は直流電化で5700mm。橋上駅舎は6以上。
//   柱スパン         … 壁柱は上屋と同じ5m級。高架橋の柱スパンは8〜10m級。
public static partial class RailwayExpander
{
    // 改札機の本体長さ（通行方向のマス数）。実寸1.8m級。
    private const int GateBody = 2;

    // 壁柱の間隔と、高架の柱の間隔（マス）。
    private const int StationColumnStep = 5;
    private const int StationPierStep = 10;

    private static string StationTypeOf(string? v) => (v ?? "ground").Trim().ToLowerInvariant() switch
    {
        "bridge" => "bridge",
        "elevated" => "elevated",
        _ => "ground",
    };

    private static void BuildStation(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        if (StationTypeOf(spec.RailStationType) == "bridge") BuildStationBridge(cells, spec, p);
        else BuildStationGround(cells, spec, p);
    }

    // ===== 地平駅舎・高架下駅舎 =====
    private static void BuildStationGround(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        bool elevated = StationTypeOf(spec.RailStationType) == "elevated";
        int w = Clamp(spec.Width, 10, 48);
        int len = Clamp(spec.Depth, 10, 64);
        int ch = Clamp(spec.RailConcourse ?? 4, 3, 8);
        int passage = Clamp(spec.RailPassage ?? 4, 2, 12);
        int lightStep = Clamp(spec.RailLightStep ?? 8, 0, 32);
        int canopy = Clamp(spec.RailEntranceCanopy ?? 0, 0, 8);
        string shape = RoofShapeOf(spec.RailCanopyRoof);
        int pitch = Clamp(spec.RailRoofPitch ?? 4, 1, 8);

        int top = ch;
        int gx = GatePosition(w);

        // 高架下は床版が屋根を兼ねる。壁はその直下まで立ち上げて帯状の空きを作らない。
        int via = elevated ? Clamp(spec.RailViaduct ?? (ch + 3), ch + 2, 32) : 0;
        string roofShape = elevated ? "flat" : shape;
        int roofY = elevated ? via : top + 1;

        StationBox(cells, p, w, len, 0, ch, passage, true, true, roofShape, pitch, roofY);
        GateLine(cells, spec, p, gx, 0, ch, len);
        StationRooms(cells, spec, p, gx, w, len, 0, ch, true);
        TicketMachines(cells, spec, p, w - 2, len, 0);
        StationLights(cells, p, lightStep, w, len, 0, ch);

        if (elevated)
        {
            Fill(cells, 0, w - 1, via, via, 0, len - 1, p.Girder);
            for (int z = 0; z < len; z += StationPierStep)
                Fill(cells, 0, w - 1, via - 1, via - 1, z, z, p.Girder);
        }
        else
        {
            for (int z = 0; z < len; z += StationColumnStep)
                Fill(cells, 0, w - 1, top, top, z, z, p.Body);
            RoofSheet(cells, shape, true, 0, w - 1, roofY, pitch, 0, len - 1, p.Girder);
        }

        // 車寄せの庇。軒と同じ高さで駅前側へ張り出し、先端に柱を落とす。
        if (canopy > 0)
        {
            int e0 = (len - passage) / 2;
            int cz0 = Math.Max(0, e0 - 2);
            int cz1 = Math.Min(len - 1, e0 + passage + 1);
            int cy = elevated ? top + 1 : RoofYAt(shape, w - 1, 0, w - 1, roofY, pitch);
            Fill(cells, w, w + canopy - 1, cy, cy, cz0, cz1, p.Girder);
            for (int z = cz0; z <= cz1; z += StationColumnStep)
                Fill(cells, w + canopy - 1, w + canopy - 1, 0, cy - 1, z, z, p.Body);
        }
    }

    // ===== 橋上駅舎 =====
    private static void BuildStationBridge(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int span = Clamp(spec.RailSpan ?? 24, 14, 128);
        int len = Clamp(spec.Depth, 10, 48);
        int deck = Clamp(spec.Height, 6, 24);
        int ch = Clamp(spec.RailConcourse ?? 4, 3, 8);
        int passage = Clamp(spec.RailPassage ?? 4, 2, 12);
        int run = Clamp(spec.RailStairRun ?? 2, 1, 4);
        int sw = Clamp(spec.RailStairWidth ?? 3, 2, 12);
        int wallH = Clamp(spec.RailWallHeight ?? 2, 1, 4);
        int pierStep = Clamp(spec.RailPierStep ?? 0, 0, 64);
        int lightStep = Clamp(spec.RailLightStep ?? 8, 0, 32);
        string shape = RoofShapeOf(spec.RailCanopyRoof);
        int pitch = Clamp(spec.RailRoofPitch ?? 3, 1, 8);
        bool streetStair = StairOf(spec.RailStair) != "none";
        if (sw > len) sw = len;

        int top = deck + ch;
        int gx = GatePosition(span);
        int roofY = top + 1;

        StationBox(cells, p, span, len, deck, ch, passage, true, false, shape, pitch, roofY);
        GateLine(cells, spec, p, gx, deck, ch, len);
        StationRooms(cells, spec, p, gx, span, len, deck, ch, false);
        TicketMachines(cells, spec, p, 1, len, deck);
        StationLights(cells, p, lightStep, span, len, deck, ch);

        for (int z = 0; z < len; z += StationColumnStep)
            Fill(cells, 0, span - 1, top, top, z, z, p.Body);
        RoofSheet(cells, shape, true, 0, span - 1, roofY, pitch, 0, len - 1, p.Girder);

        // 橋台と中間橋脚。地上階段が付く側は階段躯体が支えるので橋台は立てない。
        if (!streetStair) Abutment(cells, 0, len, deck, p);
        Abutment(cells, span - 1, len, deck, p);
        if (pierStep > 0)
            for (int x = pierStep; x < span - 1; x += pierStep)
                Abutment(cells, x, len, deck, p);

        if (streetStair) Stair(cells, -1, 0, span, len, sw, deck, run, wallH, p);

        // ホーム連絡階段。改札内（x>gx）から床版に開口を空けて z 方向へ下る。
        int need = 2 + (deck - 1) * run;
        if (need <= len - 2)
        {
            int c0 = gx + GateBody + 6;
            int c1 = span - 3;
            if (c1 >= c0)
            {
                int n = (c1 - c0 >= 8) ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    int c = n == 1 ? (c0 + c1) / 2 : c0 + i * (c1 - c0);
                    PlatformStair(cells, p, c - 1, c + 1, deck, run, wallH);
                }
            }
        }
    }

    // ===== 共通部品 =====

    // 改札ラッチの x。ホーム側からおよそ1/3の位置に置き、両側に通路を残す。
    private static int GatePosition(int w)
    {
        int gx = Math.Max(2, w / 3);
        if (gx + GateBody > w - 5) gx = Math.Max(2, w - 5 - GateBody);
        return gx;
    }

    // 外周。出入口は x の両端の中央を有効幅ぶん抜く。
    // baseY は床の高さ。壁は屋根の下端（RoofYAt-1）まで立ち上げるので、
    // 切妻・片流れの三角部分も高架下の床版までの帯もここで塞がる。
    private static void StationBox(Dictionary<(int x, int y, int z), string> cells, Palette p,
        int w, int len, int baseY, int ch, int passage, bool openNear, bool openFar,
        string shape, int pitch, int roofBaseY)
    {
        int top = baseY + ch;
        int e0 = (len - passage) / 2;
        int e1 = e0 + passage - 1;

        Fill(cells, 0, w - 1, baseY, baseY, 0, len - 1, p.Pave);

        // 桁行き方向の壁。屋根が勾配方向（x）なので、この2面は軒の高さで揃う。
        foreach (int xw in new[] { 0, w - 1 })
        {
            int wallTop = RoofYAt(shape, xw, 0, w - 1, roofBaseY, pitch) - 1;
            for (int z = 0; z < len; z++)
            {
                bool open = z >= e0 && z <= e1 && (xw == 0 ? openNear : openFar);
                int y0 = open ? top : baseY + 1;   // 開口の上は無目から屋根まで塞ぐ
                if (z % StationColumnStep == 0) Fill(cells, xw, xw, y0, wallTop, z, z, p.Body);
                else
                {
                    Fill(cells, xw, xw, y0, wallTop, z, z, p.Tactile);
                    if (!open) cells[(xw, top - 1, z)] = p.Glass;
                }
            }
        }

        // 妻壁。x ごとに屋根の下端まで立ち上げる。
        for (int x = 0; x < w; x++)
        {
            int wallTop = RoofYAt(shape, x, 0, w - 1, roofBaseY, pitch) - 1;
            foreach (int zw in new[] { 0, len - 1 })
            {
                if (x % StationColumnStep == 0) Fill(cells, x, x, baseY + 1, wallTop, zw, zw, p.Body);
                else
                {
                    Fill(cells, x, x, baseY + 1, wallTop, zw, zw, p.Tactile);
                    cells[(x, top - 1, zw)] = p.Glass;
                }
            }
        }
    }

    // 改札ラッチ。機械（長さ GateBody・高さ1）と通路を交互に並べ、
    // ラッチの外側はコンコース天井まで仕切りで塞ぐ。
    private static void GateLine(Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, Palette p, int gx, int baseY, int ch, int len)
    {
        int lane = spec.RailGateWide ? 2 : 1;
        int gates = Clamp(spec.RailGates ?? 4, 1, 16);
        int need = gates * (lane + 1) + 1;
        if (need > len - 2)
        {
            gates = Math.Max(1, (len - 3) / (lane + 1));
            need = gates * (lane + 1) + 1;
        }

        int start = (len - need) / 2;
        var lanes = new HashSet<int>();
        int cur = start + 1;
        for (int i = 0; i < gates; i++)
        {
            for (int k = 0; k < lane; k++) lanes.Add(cur + k);
            cur += lane + 1;
        }

        for (int z = 0; z < len; z++)
        {
            if (lanes.Contains(z)) continue;
            if (z >= start && z < start + need)
                Fill(cells, gx, gx + GateBody - 1, baseY + 1, baseY + 1, z, z, p.Edge);
            else
                Fill(cells, gx, gx + GateBody - 1, baseY + 1, baseY + ch - 1, z, z, p.Glass);
        }
    }

    // 諸室。paidLow=true なら改札内が x<gx（地平・高架下）、false なら x>gx（橋上）。
    private static void StationRooms(Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, Palette p, int gx, int w, int len, int baseY, int ch, bool paidLow)
    {
        int h = Math.Min(ch - 1, 3);
        if (h < 2 || len < 10) return;

        int waiting = Clamp(spec.RailWaiting ?? 0, 0, 24);
        int office = Clamp(spec.RailOffice ?? 0, 0, 24);

        int paidEdge = paidLow ? 1 : gx + GateBody;         // 改札内の、改札に近い側
        int freeEdge = paidLow ? gx + GateBody : gx - 4;    // 改札外の、改札に近い側
        int farEdge = paidLow ? w - 5 : 1;                  // 改札外の、出入口に近い側

        bool Fits(int x0, int x1) => x0 >= 1 && x1 <= w - 2;

        if (waiting >= 3 && Fits(paidEdge, paidEdge + 3))
            StationRoom(cells, p, paidEdge, paidEdge + 3, 1,
                Math.Min(len - 2, waiting), baseY, h, p.Glass);

        if (office >= 3 && Fits(freeEdge, freeEdge + 3))
            StationRoom(cells, p, freeEdge, freeEdge + 3, 1,
                Math.Min(len - 8, office), baseY, h, p.Tactile);

        if (spec.RailToilet && Fits(freeEdge, freeEdge + 3))
            StationRoom(cells, p, freeEdge, freeEdge + 3, len - 5, len - 2, baseY, h, p.Tactile);

        // エレベーター。かご内法1400×1350mm＋壁で3マス角。天井まで通す。
        if (spec.RailElevator && Fits(farEdge, farEdge + 2))
            StationRoom(cells, p, farEdge, farEdge + 2, len - 5, len - 3, baseY, ch - 1, p.Glass);
    }

    // 1室。天井を張り四周を囲い、駅前側（x1）の中央に有効2マスの出入口を空ける。
    private static void StationRoom(Dictionary<(int x, int y, int z), string> cells, Palette p,
        int x0, int x1, int z0, int z1, int baseY, int h, string wall)
    {
        if (x1 <= x0 || z1 <= z0 || h < 2) return;

        int top = baseY + h;
        Fill(cells, x0, x1, top, top, z0, z1, p.Girder);
        Fill(cells, x0, x1, baseY + 1, top - 1, z0, z0, wall);
        Fill(cells, x0, x1, baseY + 1, top - 1, z1, z1, wall);
        Fill(cells, x0, x0, baseY + 1, top - 1, z0, z1, wall);
        Fill(cells, x1, x1, baseY + 1, top - 1, z0, z1, wall);

        int dz = (z0 + z1) / 2;
        ClearCells(cells, x1, x1, baseY + 1, baseY + 2, dz, Math.Min(z1 - 1, dz + 1));
    }

    // 券売機。改札外の壁沿いに1マス飛ばしで並べる。
    private static void TicketMachines(Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, Palette p, int x, int len, int baseY)
    {
        int n = Clamp(spec.RailTicket ?? 0, 0, 16);
        for (int i = 0; i < n; i++)
        {
            int z = 2 + i * 2;
            if (z > len - 7) break;
            Fill(cells, x, x, baseY + 1, baseY + 2, z, z, p.Edge);
        }
    }

    private static void StationLights(Dictionary<(int x, int y, int z), string> cells,
        Palette p, int step, int w, int len, int baseY, int ch)
    {
        if (step <= 0) return;
        int y = baseY + ch - 1;
        for (int z = step / 2; z < len; z += step)
            for (int x = step / 2; x < w; x += step)
                cells[(x, y, z)] = p.Trim;
    }

    // ホーム連絡階段。床版に開口を空け、z の増加方向へ1段=1mで下る。
    // 着地はホーム天端（レール面上1マス）。
    private static void PlatformStair(Dictionary<(int x, int y, int z), string> cells,
        Palette p, int x0, int x1, int deckY, int run, int wallH)
    {
        const int Bottom = 1;
        const int ZStart = 2;

        for (int k = 1; k <= deckY - Bottom; k++)
        {
            int y = deckY - k;
            int a = ZStart + (k - 1) * run;
            int b = ZStart + k * run - 1;

            ClearCells(cells, x0, x1, y + 1, deckY, a, b);
            Fill(cells, x0, x1, y, y, a, b, p.Pave);
            Fill(cells, x0 - 1, x0 - 1, y + 1, y + wallH, a, b, p.Fence);
            Fill(cells, x1 + 1, x1 + 1, y + 1, y + wallH, a, b, p.Fence);
        }
    }

    private static void ClearCells(Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    cells.Remove((x, y, z));
    }
}
