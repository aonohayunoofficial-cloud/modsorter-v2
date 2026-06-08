using ModSorter.Architect.Generation;

namespace ModSorter.Architect;

// 建築モードのリソース管理。起動（生成）されるまで何もロードしない。
// 仕様書 第8部: ArchitectModeHost.cs に対応。
public sealed class ArchitectModeHost
{
    // 最小実験では生成クライアントのみ保持。3D/IO はまだ持たない。
    public ArchitectGenClient Generation { get; }

    public ArchitectModeHost()
    {
        Generation = new ArchitectGenClient();
    }
}
