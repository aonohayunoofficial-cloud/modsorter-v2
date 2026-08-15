using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 橋の横断面（車道・中央分離帯・区画線・地覆・歩道・高欄）と付帯設備の共通部品。
// 桁橋で確定した割り付けをそのまま吊り橋・アーチ橋・跳開橋でも使う。
//
// 区画線は専用の1マス列を持つ。1マス=1m では実物の線幅0.15mを車道から削ると
// 車線幅が指定どおりにならず、しかも左右非対称になるため、線に1マスずつ配り
// 全幅を線の本数ぶん広げる側へ丸める。
//
// 注意: BridgeExpander.Girder.cs は先に書いたため同じ割り付けを自前で持っている。
// 式を直すときは両方を同時に直すこと。
public static partial class BridgeExpander
{
    private sealed class Section
    {
        public int DeckW;              // 全幅
        public int RoadX0, RoadX1;     // 車道（区画線を含む）の範囲
        public int Edge;               // 片側の縁（地覆＋歩道）の幅
        public int Walk, Rail, Median;
        public int MedianX0 = -1;
        public bool Marks;
        public readonly List<int> SolidMarkX = new();   // 外側線（実線）
        public readonly List<int> DashMarkX = new();    // 車線境界線（破線）

        // 床版の y を基準にした相対高さ。
        public int TopDy => Walk > 0 ? 2 : 1;   // 高欄が載る面
        public int TotalDy => TopDy + Rail;     // 床版から高欄天端まで
    }

    private static Section MakeSection(StructureSpec spec)
    {
        var s = new Section();

        int lanes = Clamp(spec.BridgeLanes ?? 2, 1, 6);
        int laneW = Clamp(spec.BridgeLaneWidth ?? 3, 3, 4);
        int median = lanes < 2 ? 0 : Clamp(spec.BridgeMedian ?? 0, 0, 6);

        s.Walk = Clamp(spec.BridgeSidewalk ?? 2, 0, 6);
        s.Rail = Clamp(spec.BridgeRailing ?? 1, 0, 3);
        s.Marks = spec.BridgeLaneMark;
        s.Median = median;

        int markW = s.Marks ? 1 : 0;
        int lanesL = median > 0 ? (lanes + 1) / 2 : lanes;
        int lanesR = median > 0 ? lanes - lanesL : 0;

        int Carriage(int n) => n <= 0 ? 0 : n * laneW + (n - 1) * markW;
        int roadW = markW
                  + (median > 0 ? Carriage(lanesL) + median + Carriage(lanesR) : Carriage(lanes))
                  + markW;

        s.Edge = s.Walk > 0 ? s.Walk + 1 : 1;
        s.DeckW = roadW + s.Edge * 2;
        s.RoadX0 = s.Edge;
        s.RoadX1 = s.Edge + roadW - 1;

        // 左から順に「外側線・車線・境界線・…・分離帯・…・外側線」と詰める。
        int x = s.RoadX0;
        if (s.Marks) { s.SolidMarkX.Add(x); x += 1; }
        for (int i = 0; i < lanesL; i++)
        {
            x += laneW;
            if (i < lanesL - 1 && s.Marks) { s.DashMarkX.Add(x); x += 1; }
        }
        if (median > 0)
        {
            s.MedianX0 = x;
            x += median;
            for (int i = 0; i < lanesR; i++)
            {
                x += laneW;
                if (i < lanesR - 1 && s.Marks) { s.DashMarkX.Add(x); x += 1; }
            }
        }
        if (s.Marks) s.SolidMarkX.Add(x);   // ここが RoadX1 に一致する

        return s;
    }

    // 床版から上（路面・分離帯・区画線・地覆・歩道・高欄）を z0..z1 に敷く。
    // 可動桁のように1マスずつ高さを変えて敷く使い方もできるよう deckY を毎回受け取る。
    private static void BuildDeckSurface(
        Dictionary<(int x, int y, int z), string> cells, Palette p, Section s,
        int deckY, int z0, int z1, bool withRail = true)
    {
        if (z1 < z0) return;

        int surfY = deckY + 1;
        int walkY = deckY + 2;
        int topY = deckY + s.TopDy;

        Fill(cells, 0, s.DeckW - 1, deckY, deckY, z0, z1, p.Deck);
        Fill(cells, s.RoadX0, s.RoadX1, surfY, surfY, z0, z1, p.Pave);

        // 中央分離帯。路面から1マス立ち上げる（防護柵つき分離帯に相当）。
        if (s.Median > 0)
            Fill(cells, s.MedianX0, s.MedianX0 + s.Median - 1, surfY, surfY + 1, z0, z1, p.Curb);

        foreach (int mx in s.SolidMarkX)
            Fill(cells, mx, mx, surfY, surfY, z0, z1, p.Mark);

        // 車線境界線は実線5m・空白5mの破線。z の絶対値で刻むので分割して敷いても揃う。
        foreach (int mx in s.DashMarkX)
            for (int z = z0; z <= z1; z++)
                if (((z % 10) + 10) % 10 < 5) cells[(mx, surfY, z)] = p.Mark;

        if (s.Walk > 0)
        {
            int lc = s.RoadX0 - 1;
            int rc = s.RoadX1 + 1;
            Fill(cells, lc, lc, surfY, walkY, z0, z1, p.Curb);
            Fill(cells, rc, rc, surfY, walkY, z0, z1, p.Curb);
            Fill(cells, 0, lc - 1, surfY, walkY - 1, z0, z1, p.Curb);
            Fill(cells, 0, lc - 1, walkY, walkY, z0, z1, p.Walk);
            Fill(cells, rc + 1, s.DeckW - 1, surfY, walkY - 1, z0, z1, p.Curb);
            Fill(cells, rc + 1, s.DeckW - 1, walkY, walkY, z0, z1, p.Walk);
        }
        else
        {
            // 歩道なしのときは最外縁の1マスが地覆になる。
            Fill(cells, 0, 0, surfY, surfY, z0, z1, p.Curb);
            Fill(cells, s.DeckW - 1, s.DeckW - 1, surfY, surfY, z0, z1, p.Curb);
        }

        if (withRail && s.Rail > 0)
        {
            Fill(cells, 0, 0, topY + 1, topY + s.Rail, z0, z1, p.Rail);
            Fill(cells, s.DeckW - 1, s.DeckW - 1, topY + 1, topY + s.Rail, z0, z1, p.Rail);
        }
    }

    // 道路照明。灯具間隔30m級で片側交互（千鳥）に立てる。
    private static void BuildLights(
        Dictionary<(int x, int y, int z), string> cells, Palette p, Section s,
        int railTopY, int step, int z0, int z1)
    {
        if (step <= 0 || z1 < z0) return;
        const int PoleHeight = 4;
        bool left = true;
        for (int z = z0 + step / 2; z <= z1; z += step)
        {
            int x = left ? 0 : s.DeckW - 1;
            Fill(cells, x, x, railTopY + 1, railTopY + PoleHeight - 1, z, z, p.Rail);
            cells[(x, railTopY + PoleHeight, z)] = p.Light;
            left = !left;
        }
    }
}
