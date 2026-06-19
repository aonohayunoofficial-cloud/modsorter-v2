using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using fNbt;


namespace ModSorter.Architect.Generation;

// Minecraft 1.21.1 の構造ファイル(.nbt)を読み込む。
// StructureNbtWriter の対になるリーダー。fNbt が gzip/非圧縮を自動判定して読む。
//
// 読み取る形:
//   root(Compound)
//     size:    List<Int>      ... [sx, sy, sz]
//     palette: List<Compound> ... 各要素 { Name: "create:shaft", Properties:{...任意} }
//     blocks:  List<Compound> ... 各要素 { state: Int(paletteのindex), pos: List<Int>[x,y,z] }
public static class StructureNbtReader
{
    // 1個のブロックの配置情報。
    public sealed class Block
    {
        public string Name = "";
        public int X, Y, Z;
        // 向き等の Block State。例 { "axis" -> "x" }, { "facing" -> "down" }。無ければ空。
        public Dictionary<string, string> Properties = new();
    }

    // 構造全体。
    public sealed class Structure
    {
        public int SizeX, SizeY, SizeZ;
        public List<Block> Blocks = new();
    }

    // ファイルパスから読み込む(gzip かどうかは fNbt が自動判定)。
    public static Structure ReadFile(string path)
    {
        var file = new NbtFile();
        file.LoadFromFile(path);
        return BuildStructure(file.RootTag);
    }

    // jar(zip)内の指定エントリを読み込んで構造化する。
    // 例: jarPath = "...create-1.21.1-6.0.10.jar", entryPath = "assets/create/ponder/millstone.nbt"
    public static Structure ReadFromJar(string jarPath, string entryPath)
    {
        using var zip = ZipFile.OpenRead(jarPath);
        var entry = zip.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException($"jar内にエントリが見つかりません: {entryPath}");

        // エントリのバイト列を取り出す。
        byte[] bytes;
        using (var s = entry.Open())
        using (var ms = new MemoryStream())
        {
            s.CopyTo(ms);
            bytes = ms.ToArray();
        }

        // fNbt はファイルパスから読む方が確実なので、一時ファイル経由で読む。
        // Ponder NBT は数KBと小さいため、一時ファイルのコストは無視できる。
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tmp, bytes);
            var file = new NbtFile();
            file.LoadFromFile(tmp);
            return BuildStructure(file.RootTag);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    // jar内の assets/<ns>/ponder/ 配下にある .nbt エントリのパス一覧を返す。
    // 例: ns = "create" なら assets/create/ponder/*.nbt をすべて(サブフォルダ含む)。
    public static List<string> ListPonderNbtEntries(string jarPath, string ns)
    {
        var result = new List<string>();
        using var zip = ZipFile.OpenRead(jarPath);
        string prefix = $"assets/{ns}/ponder/";
        foreach (var e in zip.Entries)
        {
            if (!e.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!e.FullName.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(e.FullName);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    // ルート Compound から Structure を組み立てる。
    private static Structure BuildStructure(NbtCompound root)
    {
        var result = new Structure();

        // size: Int の List (3要素)
        var sizeList = root.Get<NbtList>("size");
        if (sizeList != null && sizeList.Count >= 3)
        {
            result.SizeX = sizeList[0].IntValue;
            result.SizeY = sizeList[1].IntValue;
            result.SizeZ = sizeList[2].IntValue;
        }

        // palette: Compound の List。各要素は { Name, Properties? }
        var palette = new List<(string Id, Dictionary<string, string> Props)>();
        var palList = root.Get<NbtList>("palette");
        if (palList != null)
        {
            foreach (var tag in palList)
            {
                if (tag is not NbtCompound e) continue;

                string id = e.Get<NbtString>("Name")?.StringValue ?? "";

                var props = new Dictionary<string, string>(StringComparer.Ordinal);
                var propC = e.Get<NbtCompound>("Properties");
                if (propC != null)
                {
                    foreach (var p in propC)
                    {
                        if (p is NbtString ps)
                            props[ps.Name ?? ""] = ps.StringValue;
                    }
                }
                palette.Add((id, props));
            }
        }

        // blocks: Compound の List。各要素は { state: Int, pos: Int List[3] }
        var blkList = root.Get<NbtList>("blocks");
        if (blkList != null)
        {
            foreach (var tag in blkList)
            {
                if (tag is not NbtCompound b) continue;

                int stateIndex = b.Get<NbtInt>("state")?.IntValue ?? -1;
                if (stateIndex < 0 || stateIndex >= palette.Count) continue;

                int px = 0, py = 0, pz = 0;
                var posList = b.Get<NbtList>("pos");
                if (posList != null && posList.Count >= 3)
                {
                    px = posList[0].IntValue;
                    py = posList[1].IntValue;
                    pz = posList[2].IntValue;
                }

                var pal = palette[stateIndex];
                result.Blocks.Add(new Block
                {
                    Name = pal.Id,
                    X = px,
                    Y = py,
                    Z = pz,
                    Properties = new Dictionary<string, string>(pal.Props, StringComparer.Ordinal)
                });
            }
        }

        return result;
    }
}
