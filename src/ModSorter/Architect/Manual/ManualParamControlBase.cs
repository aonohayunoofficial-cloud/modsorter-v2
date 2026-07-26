using System;
using System.Collections.Generic;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 手動生成の中分類パラメータUIが実装する契約。
// UserControl は素直に UserControl を継承し、この契約を実装する。
public interface IManualParamControl
{
    // UIの値から spec を組み立てて返す。allowed と summary も出力する。
    StructureSpec BuildSpec(out List<string> allowed, out string summary);

    // パラメータが変わったら発火。MainWindow が購読して再描画予約する。
    event EventHandler ParamsChanged;
}
