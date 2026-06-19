using System.Collections.Generic;
using fNbt;

namespace ModSorter.Architect.Generation;

// Minecraft 1.21.1 のバニラ構造ファイル(.nbt)を書き出す。
// このフォーマットは Create の schematics/ に置くブループリントとしても、
// バニラの構造ブロックでも読める(中身は同一)。
//
// 出力NBTの形:
//   root(Compound, 名前空)
//     DataVersion: Int        ... 1.21.1 = 3955
//     size:        List<Int>  ... [sx, sy, sz]
//     palette:     List<Compound> ... 各要素 { Name: "minecraft:stone", Properties:{...任意} }
//     blocks:      List<Compound> ... 各要素 { state: Int(paletteのindex), pos: List<Int>[x,y,z] }
//     entities:    List(空)
public static class StructureNbtWriter
{
    // 1.21.1 のデータバージョン。これがズレると読み込み時に警告/変換が走る。
    public const int DataVersion_1_21_1 = 3955;

    // 1個のブロックを表す入力。
    //   Name      : 名前空き完全ID (例 "minecraft:stone")
    //   X,Y,Z     : 構造内の相対座標 (0 始まり)
    //   Properties: Block State (例 {"axis":"y"})。無ければ null。
    public sealed class Block
    {
        public string Name = "minecraft:stone";
        public int X, Y, Z;
        public Dictionary<string, string>? Properties;
    }

    // ブロック集合を構造NBTとしてファイルに保存する(gzip圧縮)。
    //   blocks   : 配置するブロック群
    //   sizeX/Y/Z: 構造の寸法(=最大座標+1)。0以下なら blocks から自動算出。
    //   path     : 出力先 .nbt のフルパス
    public static void Save(
        IEnumerable<Block> blocks, string path,
        int sizeX = 0, int sizeY = 0, int sizeZ = 0,
        int dataVersion = DataVersion_1_21_1)
    {
        var blockList = new List<Block>(blocks);

        // 寸法が指定されていなければ、ブロックの最大座標+1 で求める。
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            int mx = 0, my = 0, mz = 0;
            foreach (var b in blockList)
            {
                if (b.X > mx) mx = b.X;
                if (b.Y > my) my = b.Y;
                if (b.Z > mz) mz = b.Z;
            }
            sizeX = mx + 1; sizeY = my + 1; sizeZ = mz + 1;
        }

        // --- palette を作る。 (Name + Properties) の組をユニーク化して index を振る ---
        var palette = new NbtList("palette", NbtTagType.Compound);
        var paletteIndex = new Dictionary<string, int>();

        int GetStateIndex(Block b)
        {
            // ユニークキー: 名前 + プロパティを文字列化したもの。
            string key = b.Name;
            if (b.Properties != null && b.Properties.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in b.Properties)
                    parts.Add($"{kv.Key}={kv.Value}");
                parts.Sort(); // 順序非依存にする
                key += "[" + string.Join(",", parts) + "]";
            }

            if (paletteIndex.TryGetValue(key, out int idx))
                return idx;

            // 新規 palette エントリを作る。
            var entry = new NbtCompound { new NbtString("Name", b.Name) };
            if (b.Properties != null && b.Properties.Count > 0)
            {
                var props = new NbtCompound("Properties");
                foreach (var kv in b.Properties)
                    props.Add(new NbtString(kv.Key, kv.Value));
                entry.Add(props);
            }
            palette.Add(entry);

            idx = paletteIndex.Count;
            paletteIndex[key] = idx;
            return idx;
        }

        // --- blocks リストを作る ---
        var blocksTag = new NbtList("blocks", NbtTagType.Compound);
        foreach (var b in blockList)
        {
            int state = GetStateIndex(b);
            var pos = new NbtList("pos", NbtTagType.Int)
            {
                new NbtInt(b.X), new NbtInt(b.Y), new NbtInt(b.Z)
            };
            blocksTag.Add(new NbtCompound
            {
                new NbtInt("state", state),
                pos
            });
        }

        // --- size リスト ---
        var sizeTag = new NbtList("size", NbtTagType.Int)
        {
            new NbtInt(sizeX), new NbtInt(sizeY), new NbtInt(sizeZ)
        };

        // --- root を組み立てる ---
        var root = new NbtCompound("")
        {
            new NbtInt("DataVersion", dataVersion),
            sizeTag,
            palette,
            blocksTag,
            new NbtList("entities", NbtTagType.Compound) // 空でも必須
        };

        var file = new NbtFile(root);
        // 構造ファイルは gzip 圧縮で保存する。
        file.SaveToFile(path, NbtCompression.GZip);
    }
}
