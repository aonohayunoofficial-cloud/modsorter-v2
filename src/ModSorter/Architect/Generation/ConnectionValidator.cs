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

            // --- (A') press型: facing軸の両端2面だけがshaft/cog動力入力。残りは不可。 ---
            if (spec.PowerOnAxisEnds)
            {
                string coreAxis = ConnectionCatalog.GetRotationAxis(b) ?? "z";
                var (endA, endB) = ConnectionCatalog.AxisToDirs(coreAxis);
                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var (dx, dy, dz) = ConnectionCatalog.DirToVec(d);
                    var npos = (b.X + dx, b.Y + dy, b.Z + dz);
                    if (!idx.TryGetValue(npos, out var n)) continue;

                    bool nIsPart = n.Id is "create:shaft" or "create:cogwheel" or "create:large_cogwheel";
                    if (!nIsPart) continue;

                    bool isAxisEnd = d == endA || d == endB;
                    if (isAxisEnd)
                    {
                        // 軸端に繋がる部材は、軸がコア(=facing軸)と一致している必要がある。
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
                                    $"({b.X},{b.Y},{b.Z})の{b.Id}の軸端で繋ぐにはコアと同じaxis={coreAxis}が必要。" +
                                    $"axis={coreAxis}にすること。",
                                GeneralAdvice =
                                    "pressはfacingの向きとその反対の2側面(facing軸の両端)からのみ動力を受ける。" +
                                    "そこに繋ぐshaft/cogはpressと同じ軸にすること。"
                            });
                        }
                    }
                    else
                    {
                        // 軸端以外(facingに垂直な側面・上下)に部材が接している → 動力を受けられない。
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.PowerInputFaceInvalid,
                            AutoFixable = false,
                            TargetPos = npos,
                            HumanMessage =
                                $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}が{b.Id}の動力を受けない面に接している。" +
                                (string.IsNullOrEmpty(spec.PowerInputHint)
                                    ? "pressはfacingの向きとその反対の2側面(facing軸の両端)からのみ動力を受ける。"
                                    : spec.PowerInputHint),
                            GeneralAdvice =
                                "pressの動力入力はfacing軸の両端2面のみ。上下やfacingに垂直な側面に軸を挿しても繋がらない。"
                        });
                    }
                }
            }

            // --- (A) 入力面の制約を持つ機械(millstone/mixer 等) ---
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
                        // mixer は「側面cog一択」なので機械的に補正する:
                        //  不正な部材(上下面のshaft等)を削除し、空いている側面に
                        //  cogwheel(axis=y)を1個追加して動力入力口を作る。
                        if (b.Id == "create:mechanical_mixer")
                        {
                            // 空いている側面(north/south/east/west)を探す。
                            Dir? freeSide = null;
                            foreach (Dir sd in new[] { Dir.North, Dir.South, Dir.East, Dir.West })
                            {
                                var (sx, sy, sz) = ConnectionCatalog.DirToVec(sd);
                                if (!idx.ContainsKey((b.X + sx, b.Y + sy, b.Z + sz)))
                                {
                                    freeSide = sd;
                                    break;
                                }
                            }

                            if (freeSide != null)
                            {
                                var (sx, sy, sz) = ConnectionCatalog.DirToVec(freeSide.Value);
                                var cogPos = (b.X + sx, b.Y + sy, b.Z + sz);

                                // 不正部材を削除する issue。
                                issues.Add(new ValidationIssue
                                {
                                    Category = IssueCategory.PowerInputFaceInvalid,
                                    AutoFixable = true,
                                    RemoveTarget = true,
                                    TargetPos = npos,
                                    HumanMessage =
                                        $"({npos.Item1},{npos.Item2},{npos.Item3})の{n.Id}がmixerの動力を受けない面" +
                                        $"(上面/下面)に接しているため削除した。",
                                    GeneralAdvice =
                                        "mixerの動力は側面cogのみ。上面・下面に軸を挿しても繋がらない。"
                                });

                                // 空き側面に cogwheel(axis=y) を追加する issue(TargetPosなし)。
                                issues.Add(new ValidationIssue
                                {
                                    Category = IssueCategory.PowerInputFaceInvalid,
                                    AutoFixable = true,
                                    AddBlocks = new List<PB>
                                    {
                                        new PB
                                        {
                                            Id = "create:cogwheel",
                                            X = cogPos.Item1,
                                            Y = cogPos.Item2,
                                            Z = cogPos.Item3,
                                            Properties = new Dictionary<string, string> { ["axis"] = "y" }
                                        }
                                    },
                                    HumanMessage =
                                        $"mixerの側面({cogPos.Item1},{cogPos.Item2},{cogPos.Item3})に" +
                                        $"create:cogwheel(axis=y)を追加して動力入力口にした。",
                                    GeneralAdvice =
                                        "mixerの動力入力は側面にcogwheel(axis=y)を噛み合わせる。"
                                });
                            }
                            else
                            {
                                // 側面に空きが無い → 補正不可。再生成へ。
                                issues.Add(new ValidationIssue
                                {
                                    Category = IssueCategory.PowerInputFaceInvalid,
                                    AutoFixable = false,
                                    TargetPos = npos,
                                    HumanMessage =
                                        $"mixerの動力入力に使える側面が空いていない。" +
                                        $"mixerの側面のいずれかを空けて、そこにcreate:cogwheel(axis=y)を置くこと。" +
                                        $"上面・下面には動力を繋げない。",
                                    GeneralAdvice = spec.PowerInputHint
                                });
                            }
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
                                    (string.IsNullOrEmpty(spec.PowerInputHint)
                                        ? $"{b.Id}の正しい動力入力面に置き直すこと。"
                                        : spec.PowerInputHint),
                                GeneralAdvice =
                                    "機械ごとに動力入力できる面が決まっている。上面が動力入力でない機械(millstone/mixer)に上から軸を挿さない。"
                            });
                        }
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

        // --- (C) 出力経路検証 ---
        foreach (var b in placed)
        {
            // (C-2) mixer の basin 出力経路: 機械(y) → 空気(y-1) → basin(y-2)。
            //  basin の出力は funnel 不要。basin 横の空気の真下にある depot へ spout で自動排出される。
            //  ここでは (1)basin本体 (2)basin横空気＋斜め下depot (3)余計なfunnel撤去 を保証する。
            if (b.Id == "create:mechanical_mixer")
            {
                var below1 = (b.X, b.Y - 1, b.Z);   // 空気であるべき
                var below2 = (b.X, b.Y - 2, b.Z);   // basinであるべき

                bool below2IsBasin = idx.TryGetValue(below2, out var bb)
                                     && bb.Id == "create:basin";

                if (!below2IsBasin)
                {
                    // 真下2マスが生成空間に収まらない高さ(mixerが低すぎる)なら補正不可で再生成へ。
                    if (below2.Item2 < 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.OutputChainInvalid,
                            AutoFixable = false,
                            TargetPos = (b.X, b.Y, b.Z),
                            HumanMessage =
                                $"({b.X},{b.Y},{b.Z})のcreate:mechanical_mixerが低すぎてbasinを置く空間がない。" +
                                $"mixerはy>=2に置き、真下に1マス空け、その下にcreate:basinを置くこと。",
                            GeneralAdvice =
                                "mixerの出力はbasin経由。mixer→空気1マス→basinの縦並びが入る高さ(mixerはy>=2)に置くこと。"
                        });
                    }
                    else
                    {
                        // (y-1)に余計なブロックがあれば除去して空気にする。
                        bool gapBlocked = idx.TryGetValue(below1, out var mid)
                                          && mid.Id != "minecraft:air";

                        // basin を真下2マスに追加。
                        var adds = new List<PB>
                        {
                            new PB
                            {
                                Id = "create:basin",
                                X = below2.Item1, Y = below2.Item2, Z = below2.Item3,
                                Properties = new Dictionary<string, string> { ["facing"] = "down" }
                            }
                        };

                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.OutputChainInvalid,
                            AutoFixable = true,
                            TargetPos = gapBlocked ? below1 : ((int, int, int)?)null,
                            RemoveTarget = gapBlocked,
                            AddBlocks = adds,
                            HumanMessage =
                                $"({b.X},{b.Y},{b.Z})のcreate:mechanical_mixerの出力経路が不正。" +
                                $"真下に1マス空け({b.X},{b.Y - 1},{b.Z}=空気)、" +
                                $"その下({b.X},{b.Y - 2},{b.Z})にcreate:basinを置くこと。" +
                                $"basinの出力はfunnel不要で、basin横の空気の真下にあるbelt/depotへ自動排出される。",
                            GeneralAdvice =
                                "mixer(と圧縮press)の出力はbasin経由。機械→空気1マス→basinの縦並びが必須。" +
                                "basinはfunnel出力ではなく、隣接空気の真下のbelt/depotへspoutで排出する。"
                        });
                    }
                }
                else
                {
                    // (C-3) basin の排出先: basin横1マス=空気 かつ その斜め下にdepot。
                    //  basin が確定している場合のみ。4水平方向のどこか1方向に
                    //  「横=空気・斜め下=depot」が既にあればOK。無ければ1方向に自動配置する。
                    var basin = bb!;
                    bool outOk = false;
                    foreach (Dir sd in new[] { Dir.North, Dir.South, Dir.East, Dir.West })
                    {
                        var (sx, sy, sz) = ConnectionCatalog.DirToVec(sd);
                        var side = (basin.X + sx, basin.Y, basin.Z + sz);          // 横(空気であるべき)
                        var sideBelow = (basin.X + sx, basin.Y - 1, basin.Z + sz); // 斜め下(depotであるべき)

                        bool sideAir = !idx.ContainsKey(side);
                        bool depotThere = idx.TryGetValue(sideBelow, out var dp)
                                          && dp.Id == "create:depot";
                        if (sideAir && depotThere) { outOk = true; break; }
                    }

                    if (!outOk)
                    {
                        // 横が空いている方向を1つ選び、その斜め下にdepotを置く。
                        Dir? useSide = null;
                        foreach (Dir sd in new[] { Dir.North, Dir.South, Dir.East, Dir.West })
                        {
                            var (sx, sy, sz) = ConnectionCatalog.DirToVec(sd);
                            var side = (basin.X + sx, basin.Y, basin.Z + sz);
                            var sideBelow = (basin.X + sx, basin.Y - 1, basin.Z + sz);
                            // 横が空気で、斜め下が空き(または既存depot以外で埋まっていない)方向。
                            if (!idx.ContainsKey(side) && !idx.ContainsKey(sideBelow)
                                && sideBelow.Item2 >= 0)
                            {
                                useSide = sd;
                                break;
                            }
                        }

                        if (useSide != null)
                        {
                            var (sx, sy, sz) = ConnectionCatalog.DirToVec(useSide.Value);
                            var depotPos = (basin.X + sx, basin.Y - 1, basin.Z + sz);
                            issues.Add(new ValidationIssue
                            {
                                Category = IssueCategory.OutputChainInvalid,
                                AutoFixable = true,
                                AddBlocks = new List<PB>
                                {
                                    new PB
                                    {
                                        Id = "create:depot",
                                        X = depotPos.Item1, Y = depotPos.Item2, Z = depotPos.Item3
                                    }
                                },
                                HumanMessage =
                                    $"({basin.X},{basin.Y},{basin.Z})のcreate:basinの排出先が無い。" +
                                    $"basin横({basin.X + sx},{basin.Y},{basin.Z + sz})を空気にして、" +
                                    $"その斜め下({depotPos.Item1},{depotPos.Item2},{depotPos.Item3})にcreate:depotを置いた。",
                                GeneralAdvice =
                                    "basinは横の空気ブロックの真下にあるdepot/beltへspoutで排出する。" +
                                    "basinの真横や真下のstorageには渡らない。"
                            });
                        }
                        else
                        {
                            issues.Add(new ValidationIssue
                            {
                                Category = IssueCategory.OutputChainInvalid,
                                AutoFixable = false,
                                TargetPos = (basin.X, basin.Y, basin.Z),
                                HumanMessage =
                                    $"({basin.X},{basin.Y},{basin.Z})のcreate:basinの排出先を置く空間が無い。" +
                                    $"basinの横1マスを空気にし、その斜め下にcreate:depotを置くこと。",
                                GeneralAdvice =
                                    "basinの排出には「横=空気・斜め下=depot」の空間が要る。隣を空けること。"
                            });
                        }
                    }

                    // basin 構成では funnel は不要。mixer/basin に隣接する andesite/brass funnel を撤去。
                    foreach (Dir fd in Enum.GetValues(typeof(Dir)))
                    {
                        var (fx, fy, fz) = ConnectionCatalog.DirToVec(fd);
                        foreach (var anchor in new[] { (b.X, b.Y, b.Z), (basin.X, basin.Y, basin.Z) })
                        {
                            var fpos = (anchor.Item1 + fx, anchor.Item2 + fy, anchor.Item3 + fz);
                            if (idx.TryGetValue(fpos, out var fb) && ConnectionCatalog.IsFunnel(fb.Id))
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Category = IssueCategory.OutputChainInvalid,
                                    AutoFixable = true,
                                    RemoveTarget = true,
                                    TargetPos = fpos,
                                    HumanMessage =
                                        $"({fpos.Item1},{fpos.Item2},{fpos.Item3})の{fb.Id}は" +
                                        $"basin構成では不要なため撤去した。basinはfunnelを使わずspoutで排出する。",
                                    GeneralAdvice =
                                        "mixer/press+basin構成ではfunnelを使わない。basinは横空気の斜め下のdepotへ直接排出する。"
                                });
                            }
                        }
                    }
                }
            }

            // (C-4) press の出力経路: press(y) → 空気(y-1) → depot/belt(y-2)。
            //  press は真下に1マス作業空間を空け、その下の depot/belt 上のアイテムを叩く。
            //  間が詰まっていると作動しない。depot/belt どちらでも受かる(beltは一時停止して叩かれる)。
            //  あわせて press 隣接の funnel は不要なので撤去する。
            if (b.Id == "create:mechanical_press")
            {
                var gap = (b.X, b.Y - 1, b.Z);   // 空気であるべき(作業空間)
                var recv = (b.X, b.Y - 2, b.Z);  // depot/belt であるべき

                bool recvOk = idx.TryGetValue(recv, out var rb)
                              && (rb.Id == "create:depot" || rb.Id == "create:belt");

                if (!recvOk)
                {
                    if (recv.Item2 < 0)
                    {
                        // 真下2マスが生成空間外(pressが低すぎる)。受け皿を置けないので再生成へ。
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.OutputChainInvalid,
                            AutoFixable = false,
                            TargetPos = (b.X, b.Y, b.Z),
                            HumanMessage =
                                $"({b.X},{b.Y},{b.Z})のcreate:mechanical_pressが低すぎて受け皿を置く空間がない。" +
                                $"pressはy>=2に置き、真下に1マス空け({b.X},{b.Y - 1},{b.Z}=空気)、" +
                                $"その下({b.X},{b.Y - 2},{b.Z})にcreate:depotを置くこと。",
                            GeneralAdvice =
                                "pressは真下に1マス作業空間を空け、その下のdepot/belt上のアイテムを叩く。" +
                                "press→空気1マス→depotの縦並びが入る高さ(pressはy>=2)に置くこと。"
                        });
                    }
                    else
                    {
                        // (y-1)に余計なブロックがあれば除去して空気にする(同パスで depot 追加と併走可)。
                        bool gapBlocked = idx.TryGetValue(gap, out var gm)
                                          && gm.Id != "minecraft:air";

                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.OutputChainInvalid,
                            AutoFixable = true,
                            TargetPos = gapBlocked ? gap : ((int, int, int)?)null,
                            RemoveTarget = gapBlocked,
                            AddBlocks = new List<PB>
                            {
                                new PB
                                {
                                    Id = "create:depot",
                                    X = recv.Item1, Y = recv.Item2, Z = recv.Item3
                                }
                            },
                            HumanMessage =
                                $"({b.X},{b.Y},{b.Z})のcreate:mechanical_pressの出力経路が不正。" +
                                $"真下に1マス空け({b.X},{b.Y - 1},{b.Z}=空気)、" +
                                $"その下({b.X},{b.Y - 2},{b.Z})にcreate:depotを置くこと。",
                            GeneralAdvice =
                                "pressは真下に1マス作業空間を空け、その下のdepot/belt上のアイテムを叩く。" +
                                "press直下にdepotを密着させると隙間が無く作動しない。"
                        });
                    }
                }

                // press 隣接の funnel は不要なので撤去する。
                foreach (Dir fd in Enum.GetValues(typeof(Dir)))
                {
                    var (fx, fy, fz) = ConnectionCatalog.DirToVec(fd);
                    var fpos = (b.X + fx, b.Y + fy, b.Z + fz);
                    if (idx.TryGetValue(fpos, out var fb) && ConnectionCatalog.IsFunnel(fb.Id))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Category = IssueCategory.OutputChainInvalid,
                            AutoFixable = true,
                            RemoveTarget = true,
                            TargetPos = fpos,
                            HumanMessage =
                                $"({fpos.Item1},{fpos.Item2},{fpos.Item3})の{fb.Id}は" +
                                $"press構成では不要なため撤去した。pressは真下のdepotへ直接叩き落とす。",
                            GeneralAdvice =
                                "press構成ではfunnelを使わない。pressは真下に1マス空けたdepot上のアイテムを叩く。"
                        });
                    }
                }
            }

            // (C-0) crushing_wheels 専用検証(AutoFix不可・再生成誘導)。
            //  公式仕様: 2個1組・互いに1ブロック離して並べる(隣接させない)・
            //  両方を逆回転で駆動・素材は2輪の隙間の上から投入・加工物は隙間の真下に排出。
            //  ここでは「相方の存在」と「1マス間隔(縦または横)」のみを機械的に検証する。
            //  軸整合・逆回転・受け皿の自動補正は行わない(リスクが高いため将来の第2段)。
            if (b.Id == "create:crushing_wheels")
            {
                // 自分から見て「1ブロック離れた位置(+2方向)」に相方がいるか。
                //  Create では2輪の間に1マスの隙間を空けて配置する。
                //  6方向それぞれ2マス先(縦: y±2 / 横: x±2, z±2)を確認する。
                bool hasPartnerWithGap = false;
                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var (dx, dy, dz) = ConnectionCatalog.DirToVec(d);
                    var partnerPos = (b.X + dx * 2, b.Y + dy * 2, b.Z + dz * 2);
                    if (idx.TryGetValue(partnerPos, out var p)
                        && p.Id == "create:crushing_wheels")
                    {
                        hasPartnerWithGap = true;
                        break;
                    }
                }

                // 「隣接(間隔ゼロ)で置かれた相方」を誤配置として検出する。
                bool hasAdjacentPartner = false;
                foreach (Dir d in Enum.GetValues(typeof(Dir)))
                {
                    var (dx, dy, dz) = ConnectionCatalog.DirToVec(d);
                    var adjPos = (b.X + dx, b.Y + dy, b.Z + dz);
                    if (idx.TryGetValue(adjPos, out var p)
                        && p.Id == "create:crushing_wheels")
                    {
                        hasAdjacentPartner = true;
                        break;
                    }
                }

                if (!hasPartnerWithGap)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = IssueCategory.OutputChainInvalid,
                        AutoFixable = false,
                        TargetPos = (b.X, b.Y, b.Z),
                        HumanMessage = hasAdjacentPartner
                            ? $"({b.X},{b.Y},{b.Z})のcreate:crushing_wheelsが相方と隙間なく密着している。" +
                              $"crushing_wheelsは2個を1ブロック離して(間に1マス空けて)並べること。"
                            : $"({b.X},{b.Y},{b.Z})のcreate:crushing_wheelsが単体で置かれている。" +
                              $"crushing_wheelsは必ず2個1組で、互いに1ブロック離して(縦または横に)並べること。",
                        GeneralAdvice =
                            "crushing_wheelsは2個1組。互いに1ブロック離して並べ(間に1マスの隙間)、" +
                            "両方を逆回転で駆動する(片方だけでは動かない)。" +
                            "素材は2輪の隙間の真上から投入し、加工物は隙間の真下のdepot/beltで受ける。"
                    });
                }

                // crushing_wheels はこの専用検証で完結(millstone型funnel検証には回さない)。
                continue;
            }

            // (C-1) RequiresFunnelOutput の機械(millstone)は
            //        「隣接funnel(extracting=true)」かつ「funnel真下のstorage」が必要。
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
            if (!iss.AutoFixable) continue;

            // 新規ブロックの追加(TargetPos を持たない補正もある)。
            if (iss.AddBlocks != null)
            {
                foreach (var nb in iss.AddBlocks)
                {
                    var k = (nb.X, nb.Y, nb.Z);
                    if (idx.ContainsKey(k)) continue; // 既存があればスキップ
                    placed.Add(nb);
                    idx[k] = nb;
                    fixedCount++;
                }
                // AddBlocks専用issue(TargetPosなし)はここで完了。
                if (iss.TargetPos == null) continue;
            }

            if (iss.TargetPos == null) continue;
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

            // 任意プロパティの上書き(funnel の facing/extracting 等)。
            if (iss.SuggestedProps != null)
            {
                target.Properties ??= new Dictionary<string, string>();
                foreach (var kv in iss.SuggestedProps)
                    target.Properties[kv.Key] = kv.Value;
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
