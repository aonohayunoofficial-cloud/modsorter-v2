using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 車庫（検車庫）。線路が z 方向に走る。y=0 は土間（作業床）。
//
// ===== 実寸の出典 =====
//   線路長     … 8両編成対応で留置線・検車ピット線とも180m。臨修線は台車1台分、
//                改造工事線は4両分。試運転線は700m。
//   ピット     … 検車エリア全体の床を線路より1段低くし、ピット間を横断できるようにする。
//                深さは1.2m級。ここでは軌道中心±1マスを掘り下げて表す。
//   有効高さ   … 電車線高さ標準5.00m＋懸吊装置500mm＋余裕200mm＝限界5.70m。
//                屋根上作業を見込むと庫内は8m級。
//   屋上点検台 … 車両屋根上（およそ3.6m）の高さに点検ホームを回す。
//   線路間隔   … 車両基地は作業通路を取るため5m級（本線の4.0mより広い）。
//   建物       … 車両検査棟は鉄骨平屋建。事務所は別棟（鉄筋コンクリート）。
public static partial class RailwayExpander
{
    private static void BuildDepot(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int n = Clamp(spec.RailTracks ?? 2, 1, 8);
        int pitch = Clamp(spec.RailTrackPitch ?? 5, 4, 12);
        int len = Clamp(spec.Depth, 20, 256);
        int h = Clamp(spec.Height, 6, 20);
        int pit = Clamp(spec.RailPit ?? 1, 0, 3);
        bool walk = spec.RailRoofWalk;
        bool shutter = spec.RailShutter;
        int annex = Clamp(spec.RailAnnex ?? 0, 0, 16);
        int lightStep = Clamp(spec.RailLightStep ?? 12, 0, 32);
        string shape = RoofShapeOf(spec.RailCanopyRoof);
        int pitchRoof = Clamp(spec.RailRoofPitch ?? 4, 1, 8);

        const int Margin = 3;   // 壁から一番外の線路までの作業通路
        int xMax = 2 * Margin + (n - 1) * pitch;

        var tracks = new List<int>();
        for (int i = 0; i < n; i++) tracks.Add(Margin + i * pitch);

        // 土間。ピットの帯（軌道中心±1）は張らずに空けておく＝掘り下げになる。
        for (int x = 0; x <= xMax; x++)
        {
            if (pit > 0 && tracks.Any(c => Math.Abs(x - c) <= 1)) continue;
            Fill(cells, x, x, 0, 0, 0, len - 1, p.Pave);
        }

        // ピットの底と側壁。ピットなしなら道床を置く。
        foreach (int c in tracks)
        {
            if (pit > 0)
            {
                Fill(cells, c - 1, c + 1, -pit, -pit, 0, len - 1, p.Body);
                Fill(cells, c - 2, c - 2, -pit, -1, 0, len - 1, p.Body);
                Fill(cells, c + 2, c + 2, -pit, -1, 0, len - 1, p.Body);
            }
            else
            {
                Fill(cells, c - 1, c + 1, 0, 0, 0, len - 1, p.Ballast);
            }
        }

        // 側壁。事務所棟があるほうには連絡口を空ける。
        int doorZ = annex >= 4 ? Math.Min(len, 30) / 2 : -1;
        foreach (int xw in new[] { 0, xMax })
        {
            for (int z = 0; z < len; z++)
            {
                bool gap = xw == xMax && doorZ >= 0 && Math.Abs(z - doorZ) <= 1;
                int y0 = gap ? 3 : 1;
                Fill(cells, xw, xw, y0, h, z, z, p.Tactile);
                if (z % 5 == 0) Fill(cells, xw, xw, y0, h, z, z, p.Body);
                else if (!gap && z > 0 && z < len - 1) cells[(xw, h - 2, z)] = p.Glass;
            }
        }

        // 妻壁と扉。開口は線路ごとに幅を取る。
        int doorHalf = Math.Min(2, Math.Max(1, (pitch - 1) / 2));
        foreach (int zEnd in new[] { 0, len - 1 })
        {
            for (int x = 0; x <= xMax; x++)
            {
                int top = RoofYAt(shape, x, 0, xMax, h + 1, pitchRoof) - 1;
                bool door = tracks.Any(c => Math.Abs(x - c) <= doorHalf);
                if (door)
                {
                    Fill(cells, x, x, h, top, zEnd, zEnd, p.Tactile);
                    if (shutter) Fill(cells, x, x, 1, h - 1, zEnd, zEnd, p.Glass);
                }
                else
                {
                    Fill(cells, x, x, 1, top, zEnd, zEnd, p.Tactile);
                }
            }
        }

        // 軒桁（トラス）と屋根
        for (int z = 0; z < len; z += 8) Fill(cells, 0, xMax, h, h, z, z, p.Body);
        RoofSheet(cells, shape, true, 0, xMax, h + 1, pitchRoof, 0, len - 1, p.Girder);

        // 屋上点検ホーム。車両屋根上の高さに通路を回し、8マスごとに支柱を落とす。
        if (walk && h >= 6)
        {
            foreach (int c in tracks)
            {
                int wx = c + 2;
                if (wx >= xMax) continue;
                Fill(cells, wx, wx, 4, 4, 0, len - 1, p.Pave);
                Fill(cells, wx, wx, 5, 5, 0, len - 1, p.Fence);
                for (int z = 0; z < len; z += 8) Fill(cells, wx, wx, 1, 3, z, z, p.Body);
            }
        }

        // 照明。各線の真上に吊る。
        if (lightStep > 0)
        {
            for (int z = lightStep / 2; z < len; z += lightStep)
                foreach (int c in tracks)
                    cells[(c, h - 1, z)] = p.Trim;
        }

        // 事務所棟（2階建・階高3）
        if (annex >= 4)
        {
            int ax0 = xMax + 1, ax1 = xMax + annex;
            int az1 = Math.Min(len, 30) - 1;
            const int AnnexH = 6;

            Fill(cells, ax0, ax1, 0, 0, 0, az1, p.Pave);
            Fill(cells, ax0, ax1, 3, 3, 0, az1, p.Pave);
            Fill(cells, ax0, ax1, AnnexH, AnnexH, 0, az1, p.Girder);
            Fill(cells, ax1, ax1, 1, AnnexH - 1, 0, az1, p.Tactile);
            Fill(cells, ax0, ax1, 1, AnnexH - 1, 0, 0, p.Tactile);
            Fill(cells, ax0, ax1, 1, AnnexH - 1, az1, az1, p.Tactile);
            Fill(cells, ax1, ax1, 2, 2, 1, az1 - 1, p.Glass);
            Fill(cells, ax1, ax1, 5, 5, 1, az1 - 1, p.Glass);
        }
    }
}
