using System;
using System.Collections.Generic;
using System.Linq;
using PB = ModSorter.Clients.ModuleGenerator.PlacedBlock;

namespace ModSorter.Architect.Generation;

// 配置済みブロック間の「接続整合」を検証し、機械的に直せるものは補正する。
// Phase1: 回転接続の軸不一致 と 動力入力面の違反 のみ。
public static class ConnectionValidator
{
    private static (int, int, int) Key(PB b) => (b.X, b.Y, b.Z);

    private static Dictionary<(int, int, int), PB> BuildIndex(IReadOnlyList<PB> placed)
    {
        var idx = new Dictionary<(int, int, int), PB>();
        foreach (var b in placed) idx[Key(b)] = b;
        return idx;
    }

    // placed を検証し、問題リストを返す。空なら合格。
    public static List<ValidationIssue> Validate(IReadOnlyList<PB> placed)
    {
        var issues = new List<ValidationIssue>();
        var idx = BuildIndex(placed);

        foreach (var b in placed)
        {
            var spec = ConnectionCatalog.GetRotation(b.Id);
            if (spec == null) continue;

            // --- (A) 入力面の制約を持つ機械(millstone/press/mixer 等) ---
            if (spec.PowerInputFaces != null)
            {
                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var (dx, dy, dz) = ConnectionCatalog.DirToVec(d);
                    var npos = (b.X + dx, b.Y + dy, b.Z + dz);
                    if (!idx.TryGetValue(npos, out var n)) continue;

                    var nspec = ConnectionCatalog.GetRotation(n.Id);
                    if (nspec == null) continue;
                    // 隣が回転を運ぶ部材(shaft/cogwheel)でなければ対象外。
                    bool nIsPart = n.Id is "create:shaft" or "create:cogwheel" or "create:large_cogwheel";
                    if (!nIsPart) continue;

                    var allow = spec.PowerInputFaces.TryGetValue(d, out var k) ? k : PowerInputKind.None;
                    bool nIsShaft = n.Id == "create:shaft";
                    bool nIsCog = n.Id is "create:cogwheel" or "create:large_cogwheel";

                    bool ok = allow switch
                    {
                        PowerInputKind.ShaftOnly => nIsShaft,
                        PowerInputKind.CogOnly => nIsCog,
                        PowerInputKind.ShaftOrCog => true,
                        _ => false // None
                    };

                    // 側面cog入力の場合、cogの軸がコア軸と一致していないと噛み合わない。
                    // (例: millstone(y軸)の側面cogは axis=y でないと動かない)
                    if (ok && allow == PowerInputKind.CogOnly && nIsCog)
                    {
                        string coreAxis = ConnectionCatalog.GetRotationAxis(b) ?? "y";
                        string? nAxis = ConnectionCatalog.GetRotationAxis(n);
                        if (nAxis != coreAxis)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Category = IssueCategory.RotationAxisMismatch,
                                AutoFixable = true,
                                TargetPos = npos,
                                SuggestedAxis = coreAxis,
                                HumanMessage =
                                    $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}がaxis={nAxis}だが、" +
                                    $"({b.X},{b.Y},{b.Z})の{b.Id}の側面で噛み合うにはコアと同じaxis={coreAxis}が必要。" +
                                    $"axis={coreAxis}にすること。",
                                GeneralAdvice =
                                    "millstone等の側面cogは、コアの回転軸(millstoneならaxis=y)と同じ向きにして噛み合わせること。"
                            });
                            continue; // 軸補正のissueを出したのでこの方向は終了
                        }
                    }

                    if (ok) continue;

                    // 違反。補正方針:
                    //  - 面が CogOnly なのに shaft → cogwheel へ種別変換(軸は面の軸に合わせる)。
                    //  - 面が None(動力不可)→ 補正不可(再生成)。
                    if (allow == PowerInputKind.CogOnly && nIsShaft)
                    {
                        // 側面cogwheelは「核の回転軸」と同じ軸にする(同じ面で噛み合うため)。
                        // millstone は縦軸(y)なので、側面cogwheelも axis=y。面の軸ではない。
                        string coreAxis = ConnectionCatalog.GetRotationAxis(b) ?? "y";
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.PowerInputFaceInvalid,
                            AutoFixable = true,
                            TargetPos = npos,
                            SuggestedBlockId = "create:cogwheel",
                            SuggestedAxis = coreAxis,
                            HumanMessage =
                                $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}が{b.Id}の側面に直結しているが、" +
                                $"{b.Id}の側面はcogwheelでしか動力を受けられない。" +
                                $"cogwheelは核と同じ回転軸(axis={coreAxis})で横に噛み合わせる。" +
                                $"create:cogwheel(axis={coreAxis})にすること。",
                            GeneralAdvice =
                                "millstone等の側面から動力を入れるときは、shaftではなくcogwheelを" +
                                "核と同じ回転軸(millstoneならaxis=y)で横に噛み合わせること。" +
                                "shaftを使うなら底面にaxis=yで縦に挿す。"
                        });
                    }
                    else
                    {
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.PowerInputFaceInvalid,
                            AutoFixable = false,
                            TargetPos = npos,
                            HumanMessage =
                                $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}が{b.Id}の動力を受けられない面に接している。" +
                                $"{b.Id}の正しい動力入力面に置き直すこと。",
                            GeneralAdvice =
                                "機械ごとに動力入力できる面が決まっている。上面が動力入力でない機械(millstone/press)に上から軸を挿さない。"
                        });
                    }
                }
            }

            // --- (B) shaft/cogwheel の接続整合 ---
            // shaft: 軸の端(同軸方向)にだけ繋がる。
            // cogwheel: 軸の端=同軸(shaft的に伝達)、軸に垂直=噛み合い(相手cogのみ)。
            //           軸に垂直に shaft を置くのは無効接続(再生成)。
            if (b.Id is "create:shaft" or "create:cogwheel" or "create:large_cogwheel")
            {
                string? axisB = ConnectionCatalog.GetRotationAxis(b);
                if (axisB == null) continue;

                bool bIsCog = b.Id is "create:cogwheel" or "create:large_cogwheel";

                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var (dx, dy, dz) = ConnectionCatalog.DirToVec(d);
                    var npos = (b.X + dx, b.Y + dy, b.Z + dz);
                    if (!idx.TryGetValue(npos, out var n)) continue;
                    if (n.Id != "create:shaft" && n.Id != "create:cogwheel"
                        && n.Id != "create:large_cogwheel") continue;

                    string? axisN = ConnectionCatalog.GetRotationAxis(n);
                    if (axisN == null) continue;

                    // この方向が b の軸の「端(同軸方向)」か、それとも「軸に垂直」か。
                    string dirAxis = AxisOfDir(d);
                    bool alongAxis = dirAxis == axisB; // 軸方向(端)
                    bool nIsCog = n.Id is "create:cogwheel" or "create:large_cogwheel";

                    if (alongAxis)
                    {
                        // 同軸の端に繋がる相手は軸一致が必須。
                        if (axisN == axisB) continue; // OK
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.RotationAxisMismatch,
                            AutoFixable = true,
                            TargetPos = npos,
                            SuggestedAxis = axisB,
                            HumanMessage =
                                $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}がaxis={axisN}だが、" +
                                $"同軸で直結する({b.X},{b.Y},{b.Z})の{b.Id}はaxis={axisB}。axis={axisB}にすること。",
                            GeneralAdvice =
                                "shaft/cogwheelの軸の端(同軸方向)に繋ぐ相手はaxisを揃えること。"
                        });
                    }
                    else
                    {
                        // 軸に垂直な隣接。
                        // b が cogwheel で 相手も cogwheel なら噛み合い(軸一致が必須)。
                        if (bIsCog && nIsCog)
                        {
                            if (axisN == axisB) continue; // 噛み合いOK
                            issues.Add(new ValidationIssue
                            {
                                Category = IssueCategory.RotationAxisMismatch,
                                AutoFixable = true,
                                TargetPos = npos,
                                SuggestedAxis = axisB,
                                HumanMessage =
                                    $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}が" +
                                    $"({b.X},{b.Y},{b.Z})の{b.Id}と噛み合う位置にあるがaxis={axisN}。" +
                                    $"噛み合うcogwheel同士は同じaxis={axisB}にすること。",
                                GeneralAdvice =
                                    "横に並べて噛み合わせるcogwheel同士は同じ向き(同じaxis)にすること。"
                            });
                        }
                        else if (bIsCog && !nIsCog)
                        {
                            // cogwheel の側面に shaft → 無効接続。LLMが繰り返し間違えるため削除する。
                            // 入力/出力口は露出したcogの軸端(上下)が担う。
                            issues.Add(new ValidationIssue
                            {
                                Category = IssueCategory.RotationAxisMismatch,
                                AutoFixable = true,
                                RemoveTarget = true,
                                TargetPos = npos,
                                HumanMessage =
                                    $"({npos.Item1},{npos.Item2},{npos.Item3})のshaftが" +
                                    $"({b.X},{b.Y},{b.Z})のcogwheelの側面(軸に垂直)に置かれ繋がらないため削除した。" +
                                    $"cogから動力を出すなら軸の端(上か下)に置くこと。",
                                GeneralAdvice =
                                    "cogwheelは軸の端(両端)からshaftのように動力を出す。横(軸に垂直)にshaftを置いても繋がらない。"
                            });
                        }
                        // それ以外(b が shaft で軸に垂直な隣接)は無関係なので無視。
                    }
                }
            }
        }

        // --- (C) 出力経路検証: RequiresFunnelOutput の機械は
        //         「隣接funnel」かつ「そのfunnelに隣接するstorage」が必要。向きは不問。
        foreach (var b in placed)
        {
            if (!ConnectionCatalog.RequiresFunnelOutput.Contains(b.Id)) continue;

            bool ok = false;
            // 機械の6方向隣接からfunnelを探す。
            foreach (Dir d in Enum.GetValues(typeof(Dir)))
            {
                var (dx, dy, dz) = ConnectionCatalog.DirToVec(d);
                if (!idx.TryGetValue((b.X + dx, b.Y + dy, b.Z + dz), out var f)) continue;
                if (!ConnectionCatalog.IsFunnel(f.Id)) continue;

                // 無印funnelの排出先は「funnelの真下(y-1)」のみ。
                // 横や上にstorageがあっても無印funnelからは渡らない。
                if (!idx.TryGetValue((f.X, f.Y - 1, f.Z), out var st)
                    || !ConnectionCatalog.IsItemStorage(st.Id)) continue;
                ok = true;

                // funnelの向き補正:
                //  無印 andesite_funnel/brass_funnel はブロック面に取り付くタイプ。
                //  向きは facing(取り付け面) と extracting(吸い出す/受け入れる) で決まる。shape は持たない。
                //   側面(水平方向)に付くfunnel → facing=機械から見た外向き水平方向。
                //   真下に付くfunnel → facing=down。
                //   extracting=true で機械から吸い出し、隣接storageへ排出する。
                //  (belt funnel は belt 上にあるときだけ。ここでは belt を使わないので無印固定。)
                string wantFacing = ConnectionCatalog.DirToFacing(d);
                const string wantExtracting = "true"; // 機械から吸い出して排出
                string curFacing = f.Properties != null
                    && f.Properties.TryGetValue("facing", out var cf) ? cf : "";
                string curExtracting = f.Properties != null
                    && f.Properties.TryGetValue("extracting", out var ce) ? ce : "";
                bool curHasShape = f.Properties != null && f.Properties.ContainsKey("shape");

                if (curFacing != wantFacing || curExtracting != wantExtracting || curHasShape)
                {
                    var props = new Dictionary<string, string>
                    {
                        ["facing"] = wantFacing,
                        ["extracting"] = wantExtracting,
                    };
                    issues.Add(new ValidationIssue
                    {
                        Category = IssueCategory.OutputChainInvalid,
                        AutoFixable = true,
                        TargetPos = (f.X, f.Y, f.Z),
                        SuggestedProps = props,
                        RemoveProps = curHasShape
                            ? new List<string> { "shape" }
                            : null,
                        HumanMessage =
                            $"({f.X},{f.Y},{f.Z})の{f.Id}の向きが不正。" +
                            $"{b.Id}から排出するにはfacing={wantFacing}, extracting={wantExtracting}にすること" +
                            (curHasShape ? "(無印funnelにshapeは無効なので削除する)" : "") + "。",
                        GeneralAdvice =
                            "無印funnel(andesite_funnel/brass_funnel)はブロック面に取り付く。" +
                            "機械の側面に付けるならfacing=機械から見た外向きの水平方向、真下ならfacing=down。" +
                            "機械から吸い出して排出するにはextracting=true。shapeはbelt funnel専用なので無印には付けない。"
                    });
                }
                break;
            }
            if (ok) continue;

            // 結合不正。直せないので再生成に回す。
            issues.Add(new ValidationIssue
            {
                Category = IssueCategory.OutputChainInvalid,
                AutoFixable = false,
                TargetPos = (b.X, b.Y, b.Z),
                HumanMessage =
                    $"({b.X},{b.Y},{b.Z})の{b.Id}の出力経路が繋がっていない。" +
                    $"{b.Id}に隣接してfunnel(extracting=true)を置き、" +
                    $"そのfunnelの真下(y-1)にdepotかchestを置くこと。funnelの横ではダメ。",
                GeneralAdvice =
                    "millstone/crushing_wheelsの加工物は、機械に隣接したfunnel(extracting=true)で吸い出し、" +
                    "funnelの真下にdepot/chestへ落とす。funnelの横や上のstorageには渡らない。"
            });
        }

        return issues;
    }

    // 自動補正を適用し、補正できた件数を返す。placed を直接書き換える。
    public static int AutoFix(List<PB> placed, List<ValidationIssue> issues)
    {
        int fixedCount = 0;
        var idx = BuildIndex(placed);

        foreach (var iss in issues)
        {
            if (!iss.AutoFixable || iss.TargetPos == null) continue;
            if (!idx.TryGetValue(iss.TargetPos.Value, out var target)) continue;

            // 削除指示: placed から該当ブロックを除く。
            if (iss.RemoveTarget)
            {
                placed.Remove(target);
                idx.Remove(iss.TargetPos.Value);
                fixedCount++;
                continue;
            }

            // 種別変換(shaft → cogwheel 等)。
            if (!string.IsNullOrEmpty(iss.SuggestedBlockId) && iss.SuggestedBlockId != target.Id)
                target.Id = iss.SuggestedBlockId!;

            // 軸の補正。
            if (!string.IsNullOrEmpty(iss.SuggestedAxis))
            {
                target.Properties ??= new Dictionary<string, string>();
                target.Properties["axis"] = iss.SuggestedAxis!;
            }


            // 不要プロパティの削除(無印funnelに紛れた shape 等)。
            if (iss.RemoveProps != null && target.Properties != null)
            {
                foreach (var key in iss.RemoveProps)
                    target.Properties.Remove(key);
            }
            fixedCount++;
        }
        return fixedCount;
    }

    private static string AxisOfDir(Dir d) => d switch
    {
        Dir.East or Dir.West => "x",
        Dir.Up or Dir.Down => "y",
        _ => "z"
    };
}
