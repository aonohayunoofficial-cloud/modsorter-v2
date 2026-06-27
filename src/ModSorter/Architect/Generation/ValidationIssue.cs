namespace ModSorter.Architect.Generation;

// 検証で検出する問題のカテゴリ(機械可読ID)。
public enum IssueCategory
{
    RotationAxisMismatch,    // 直結すべき shaft/cogwheel の軸が核と不一致
    PowerInputFaceInvalid,   // 動力入力できない面に接続している/種別が違う
    RotationNeighborMissing, // 動力源の回転軸方向に接続相手がいない(将来)
    NoPowerSource,           // 回転接続があるのに動力源が無い(将来)
    OutputChainInvalid,      // 機械の出力経路(隣接funnel→storage)が繋がっていない
}

public sealed class ValidationIssue
{
    public IssueCategory Category { get; init; }
    public string CategoryId => Category.ToString();
    public bool AutoFixable { get; init; }
    public string HumanMessage { get; init; } = "";   // 再生成用の具体指摘(座標込み)
    public string GeneralAdvice { get; init; } = "";  // 学習用の一般化注意(座標なし)

    // 補正対象の特定に使う。
    public (int x, int y, int z)? TargetPos { get; init; }
    public string? SuggestedAxis { get; init; }
    public string? SuggestedBlockId { get; init; }    // 種別変換先(例 create:cogwheel)
    public bool RemoveTarget { get; init; }           // TargetPos のブロックを削除する

    // 任意プロパティの上書き(例 funnel の facing/extracting)。AutoFix で適用。
    public Dictionary<string, string>? SuggestedProps { get; init; }

    // 不要プロパティの削除(例 無印funnelに紛れた shape)。AutoFix で適用。
    public List<string>? RemoveProps { get; init; }

    // 新規ブロックの追加(例 mixerの側面にcogwheelを補う)。AutoFix で placed に足す。
    // 追加先座標に既存ブロックがあればスキップする。
    public List<ModSorter.Clients.ModuleGenerator.PlacedBlock>? AddBlocks { get; init; }
}
