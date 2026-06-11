using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SharpGLTF.Schema2;

namespace ModSorter.Architect.Generation;

// GLB(表面メッシュ) を Minecraft ブロック集合へ変換する。
// 前段(格子化): GLB → ボクセルグリッド(中間表現)
// 後段(ブロック化): 中間表現 → GeneratedBlock のリスト
// ※第1版: 色サンプリングは未実装。全ボクセルを単一ブロックにする(形の確認用)。
public static class MeshVoxelizer
{
    public enum FillMode { Hollow, Solid }

    // 中間表現: 存在するボクセルの座標集合。色は後で足す。
    public sealed class VoxelGrid
    {
        public int Resolution;
        public HashSet<(int x, int y, int z)> Cells = new();
    }

    // エントリポイント。glbPath: 入力GLB / resolution: 格子分割数 / fill: 充填モード
    // blockId: 全ボクセルに割り当てるブロック(第1版は単一色)。
    public static GenerationResult Voxelize(
        string glbPath, int resolution, FillMode fill, string blockId)
    {
        var result = new GenerationResult();
        // 段階マーカー。最後に到達した段階が Error に残る。
        result.Error = "[M0] 開始";
        try
        {
            result.Error = "[M1] LoadTriangles 呼び出し直前";
            var tris = LoadTriangles(glbPath);
            result.Error = $"[M2] LoadTriangles 完了 tris={tris.Count}";

            if (tris.Count == 0)
            {
                result.Error =
                    "三角形が 0 件でした。GLB にメッシュが無いか、POSITION が読めていません。";
                return result;
            }

            // 既に読んだ tris を使い、グリッドを生成する（再読み込みしない）。
            result.Error = "[M3] BuildGridFromTris 呼び出し直前";
            var grid = BuildGridFromTris(tris, resolution, fill);
            if (grid.Cells.Count == 0)
            {
                result.Error = $"セルが 0 件 (tris={tris.Count})";
                return result;
            }

            var blocks = grid.Cells
                .OrderBy(c => c.y).ThenBy(c => c.z).ThenBy(c => c.x)
                .Select(c => new GeneratedBlock { X = c.x, Y = c.y, Z = c.z, Id = blockId })
                .ToList();

            // 成功。診断マーカーを消して正常結果を返す。
            result.Error = null;
            result.Blocks = blocks;
            return result;
        }
        catch (Exception ex)
        {
            // 例外の型と全文を出す（内部例外も）。
            result.Error =
                $"{ex.GetType().Name}: {ex.Message}" +
                (ex.InnerException != null
                    ? $" / inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                    : "");
            return result;
        }
    }

    // ── 前段: 三角形リスト → ボクセルグリッド ──
    // (GLB の読み込みは呼び出し側で済ませ、ここでは tris を受け取る)
    private static VoxelGrid BuildGridFromTris(
        List<Triangle> tris, int resolution, FillMode fill)
    {
        var grid = new VoxelGrid { Resolution = resolution };

        if (tris.Count == 0) return grid;

        // 2. 全頂点からAABBを求める。
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var t in tris)
        {
            foreach (var p in new[] { t.A, t.B, t.C })
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        // 立方体セルにするため、最大辺を基準にセルサイズを決める。
        Vector3 size = max - min;
        float maxEdge = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (maxEdge <= 0) return grid;
        float voxelSize = maxEdge / resolution;

        // 座標→グリッドインデックス変換。
        (int x, int y, int z) ToCell(Vector3 p)
        {
            int cx = (int)((p.X - min.X) / voxelSize);
            int cy = (int)((p.Y - min.Y) / voxelSize);
            int cz = (int)((p.Z - min.Z) / voxelSize);
            cx = Math.Clamp(cx, 0, resolution - 1);
            cy = Math.Clamp(cy, 0, resolution - 1);
            cz = Math.Clamp(cz, 0, resolution - 1);
            return (cx, cy, cz);
        }

        // 3. 表面ボクセルを塗る。各三角形を細かくサンプリングして、
        //    通過するセルを埋める(簡易ラスタライズ)。
        foreach (var t in tris)
        {
            // 三角形の辺の長さからサンプリング密度を決める。
            float edgeLen = Math.Max(
                Vector3.Distance(t.A, t.B),
                Math.Max(Vector3.Distance(t.B, t.C), Vector3.Distance(t.C, t.A)));
            int steps = Math.Max(2, (int)(edgeLen / voxelSize) + 1);

            // 重心座標で三角形内部を走査してセルを埋める。
            for (int i = 0; i <= steps; i++)
                for (int j = 0; j <= steps - i; j++)
                {
                    float u = (float)i / steps;
                    float v = (float)j / steps;
                    float w = 1 - u - v;
                    if (w < 0) continue;
                    Vector3 p = t.A * w + t.B * u + t.C * v;
                    grid.Cells.Add(ToCell(p));
                }
        }

        // 4. Solid なら内部を埋める。
        if (fill == FillMode.Solid)
            FillInterior(grid, resolution);

        return grid;
    }

    // ── 内部充填: 外側から flood fill で外部を特定し、残りを内部として埋める ──
    private static void FillInterior(VoxelGrid grid, int res)
    {
        // 外部セル集合。境界の空きセルから6方向に広げる。
        var outside = new HashSet<(int, int, int)>();
        var queue = new Queue<(int x, int y, int z)>();

        // 境界面の空きセルを起点にする。
        for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
                for (int z = 0; z < res; z++)
                {
                    bool onBoundary = x == 0 || y == 0 || z == 0 ||
                                      x == res - 1 || y == res - 1 || z == res - 1;
                    if (!onBoundary) continue;
                    var c = (x, y, z);
                    if (grid.Cells.Contains(c)) continue; // 表面は外部にしない
                    if (outside.Add(c)) queue.Enqueue(c);
                }

        var dirs = new (int dx, int dy, int dz)[]
        {
            (1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)
        };

        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            foreach (var (dx, dy, dz) in dirs)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (nx < 0 || ny < 0 || nz < 0 ||
                    nx >= res || ny >= res || nz >= res) continue;
                var nc = (nx, ny, nz);
                if (grid.Cells.Contains(nc)) continue; // 表面で遮られる
                if (outside.Add(nc)) queue.Enqueue(nc);
            }
        }

        // 外部でも表面でもないセル = 内部。埋める。
        for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
                for (int z = 0; z < res; z++)
                {
                    var c = (x, y, z);
                    if (!grid.Cells.Contains(c) && !outside.Contains(c))
                        grid.Cells.Add(c);
                }
    }

    // ── GLB読み込み: 三角形(頂点位置)を集める ──
    private struct Triangle { public Vector3 A, B, C; }

    private static List<Triangle> LoadTriangles(string glbPath)
    {
        var tris = new List<Triangle>();
        // trimesh 由来の GLB は法線が未正規化などで検証に弾かれることがある。
        // 位置(POSITION)しか使わないため、読み込み時の検証はスキップする。
        var readSettings = new SharpGLTF.Schema2.ReadSettings
        {
            Validation = SharpGLTF.Validation.ValidationMode.Skip
        };
        var model = ModelRoot.Load(glbPath, readSettings);

        foreach (var mesh in model.LogicalMeshes)
            foreach (var prim in mesh.Primitives)
            {
                var posAccessor = prim.GetVertexAccessor("POSITION");
                if (posAccessor == null) continue;
                var positions = posAccessor.AsVector3Array();

                foreach (var (ia, ib, ic) in prim.GetTriangleIndices())
                {
                    tris.Add(new Triangle
                    {
                        A = positions[ia],
                        B = positions[ib],
                        C = positions[ic]
                    });
                }
            }

        return tris;
    }
}
