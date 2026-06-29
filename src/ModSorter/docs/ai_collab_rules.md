## 1. AI 協働の固定ルール（厳守）

### コード提示の形式
1. 差し替え: 「差し替え対象の全文」→「差し替え後の全文」をセットで出す。
2. 追加: 「追加する直前の5行」→「追加後の全文（または追加コード）」で示す。
3. 新規ファイル: ディレクトリ・ファイル名・新規である旨を明記し、全文を出力する。
   → どこに何を入れる／消すかを毎回明確にするため。
   形式が守れていない場合は「固定ルール忘れてる」と指摘してよい。

### 応答スタイル
- 余計な前置き・繰り返しを減らし、要点を簡潔に。
- 前回までの内容を忘れて話すことを禁止。分からない場合は「分からない」と言う。
- 憶測での発言を禁止。判断に必要な現物が足りないときは、その現物を要求する。
- 一回一回の確認往復をしない。自分で3回検討し、完成品だけを出す。
  足りない現物があるときだけ、その現物を求める（確認の往復はしない）。
- ユーザーへの命令・指示形を禁止。手順はユーザーが実行する前提で淡々と提示する。
- 生成依頼を出すときはプロンプト文を貼るだけ。余計な会話を足さない。
- 実機作業はユーザー側。AI はアプリを動かせない。AI の仕事はコードと判断の提示まで。
  結果（ログ・ブロックリスト）はユーザーが貼る。

### コード参照ルール（厳守）
- 追加・差し替え・新規作成が発生する場合、該当箇所の現物は AI 自身が先に Git(raw) を
  参照して確認してから出す。
  raw 例: https://raw.githubusercontent.com/aonohayunoofficial-cloud/modsorter-v2/main/<パス>
- 呼び出し先・既存の同種メソッド・対応する UI 要素など関連実装も、憶測で書かず先に確認する。
- 既存に同じ機能のメソッドがあれば新規実装せず使い回す。
  （例: モデル一覧取得は `_architectHost.Generation.ListModelsAsync()` を使う）
- コミット・プッシュから次のコミット・プッシュまでは、ローカル（差し替え・追加済み）の内容を
  最新として進める。

### リポジトリ
- URL・raw URL はこのファイルに固定記載しない。再開時にユーザーが URL を送る。
  受け取るまで取得を試みず待機する。受領後、そこを基点に raw を取得して作業する。
- スタック: C#（.NET / WPF / fNbt）。Python は使わない。

---

## 2. プロジェクト概要

導入済み MOD をスキャンし、その MOD ブロックを使って自然言語の建築・機械依頼をこなす
デスクトップアプリ。中心は Create MOD の機械自動生成・検証。
フロー: 自然言語のお題＋空間サイズ → LLM(Ollama) → 検証&AutoFix → 構造NBT出力 →
ゲーム内 schematics へ設置。
2系統を持つ: 建築モード（Tab4）／ Create機械モード（Tab5）。

### 主要ファイル
- src/ModSorter/MainWindow.xaml … 画面（Tab4=建築、Tab5=Create機械の独立パネル）
- src/ModSorter/MainWindow.Architect.cs … Tab4 のハンドラ群（ArchGenerate_Click / ArchExport_Click 等）
- src/ModSorter/MainWindow.Machine.cs … Tab5 の処理（生成・出力・ResolveSchematicOutPath/ResolveMachineOutPath）
- src/ModSorter/Clients/ModuleGenerator.cs … プロンプト組立・機械生成本体
- src/ModSorter/Architect/Generation/ArchitectGenClient.cs … LLM 呼び出し（2パス: PlanAsync→GenerateAsync）
- src/ModSorter/Architect/Generation/DesignPlan.cs … 設計方針 DTO（2パス目1パス目の出力）
- src/ModSorter/Architect/Generation/StructureSpec.cs … 中間表現（設計SPEC）
- src/ModSorter/Architect/Generation/StructureExpander.cs … 確定展開（屋根/様式/開口/ドーム/ピラミッド/アーチ）
- src/ModSorter/Architect/Generation/StructureNbtWriter.cs … 構造NBT書き出し（fNbt, DataVersion 3955）
- src/ModSorter/Architect/Generation/BlockTextureProvider.cs … ブロックテクスチャ取得（jar走査・パレット抽出）
- src/ModSorter/Architect/Generation/ConnectionValidator.cs / ConnectionSpec.cs / ValidationIssue.cs … 接続検証＆AutoFix
- src/ModSorter/Architect/Rules/create_power_rules.txt … 動力ルール第1層（人間語）
- src/ModSorter/ModSorter.csproj … create_power_rules.txt のコピー設定あり

※削除済み（フェーズG）: PrimitiveSpec.cs / PrimitiveExpander.cs / StructureNbtReader.cs /
  PonderRuleExtractor.cs。曲面表現は StructureExpander 側のドーム屋根に統合済み。

### ルールファイルの扱い
- create_power_rules.txt は Architect/Rules/ に置き、ビルド時に出力フォルダへコピー。
- 実行時は AppContext.BaseDirectory 基準で読み込み、ModuleGenerator が
  プロンプト先頭に powerRulesSection として添付する。
- ブロックID表記は create: 付きの完全 ID に統一。
- 動力源の出力数値は 0.5.1 wiki 基準の「目安」のまま保持（後でゲーム内実値に更新予定）。

### 【重要・確定方針】Ponder 路線は撤回済み
Ponder 隣接統計を生成プロンプトに合流させる路線
（adjacencySection / ToRuleText / GetPonderAdjacencyRules）は試して失敗し撤回済み。
動いていた生成を壊しただけだったため、生成プロンプトには Ponder を渡さない。
配置精度は create_power_rules.txt の人間語ルールで詰める。この路線には戻らない。
関連デッドコードはフェーズGで掃除済み（StructureNbtReader / PonderRuleExtractor 削除）。

### 建築モードの設計方針（確認済み・転換不要）
建築モードは既に「自然言語→LLM(2パス: 設計方針 DesignPlan→設計SPEC StructureSpec)→
StructureExpander で確定展開」になっている。LLM に座標は書かせていない。
設計方針の転換は不要。

---

## 3. 確定知見（Create 機械・実機/公式Wikiで確認済み）

- millstone … 隣接 andesite_funnel(extracting=true, 外向き facing) → funnel の真下(y-1) に
  depot/chest。funnel の横はダメ。
- 無印funnel … facing(取り付け面) と extracting で向きが決まる。shape は持たない
  （shape は belt funnel 専用。無印に付いていたら削除）。
- mechanical_press … 動力は facing軸の両端2面のみ（south/north→相手axis=z、east/west→相手axis=x）。
  出力は press(y)→空気(y-1)→depot/belt(y-2)。press 直下に1マス作業空間が必要（直下密着は不作動）。
  press 隣接の funnel は不要なので撤去。
- mechanical_mixer … プロパティ無し・軸はY固定。動力は側面4面に cogwheel(axis=y)。
  上下面は動力不可。出力は mixer(y)→空気(y-1)→basin(y-2) の縦並び。
- basin … 出力に funnel 不要。basin 横の空気ブロックの真下にある belt/depot へ spout で自動排出。

### AutoFix の機能
- RemoveTarget（不正ブロック削除）/ SuggestedBlockId・SuggestedAxis（種別変換・軸補正）/
  SuggestedProps（任意プロパティ上書き）/ RemoveProps（不要プロパティ削除）/
  AddBlocks（新規追加: mixer側面cogwheel、mixer下basin、basin斜め下depot、press下depot）。
- mixer 専用: 上下面の不正 shaft を削除し、空き側面に cogwheel(axis=y) を追加。
- MainWindow.Machine.cs: AutoFix 収束ループ（issue が出続ける限り再 AutoFix）。
- 検証＆AutoFix 対応機種: millstone / mechanical_mixer / mechanical_press の3機種を
  実機準拠で検証・自動補正・収束まで確認済み。

---

## 4. 作業方針（重要）

- 建築モードの「テストコード排除（掃除）」と「2パス化アップグレード（建て増し）」は同時並行しない。
  触るファイル（MainWindow.Architect.cs / ArchitectGenClient.cs）が重なり、ビルドが壊れたとき
  「消したせいか足したせいか」の切り分けが不能になるため。掃除で土台を整えてから建て増しする。
- 各段階の境目で必ずコミットし、壊れても戻れるようにする。各段階で「動く状態」を確保して進む。
- 設置導線は Create機械と建築モードで共通の出力ヘルパーに寄せ、二重実装を避ける。
- テスト用コード・最小実験UIは「本機能と混線するもの」を最優先で切り離し、判断がつかないものは
  一覧化してから一括判定する。
- 機能追加は1機能1コミット。ビルド通過を確認してから次へ進む。

---

## 5. 現在の主作業：建築モード整理＆設置導線＆語彙拡張

### 第1段階: 棚卸し（消さずに把握）※完了
MainWindow.Architect.cs と MainWindow.xaml(Tab4) を取得し、ボタン⇔Clickハンドラ対応表を作成。
本番/テスト診断の仕分けのみ。結果は本ファイル「付録A」に記載。

### 第2段階: 混線の解消（バグ修正・単独）※完了
ArchTestTexture_Click 冒頭に混入していた Create軸PoC（create:shaft/cogwheel の NBT 出力）を除去。
テクスチャ取得は本番フロー（RenderArchPreviewAsync → BuildTextureMap → BlockTextureProvider）で
自動取得が完結していることを現物確認済み。コミット済み。

### 第3段階: テストコード排除（掃除）※完了
不要な診断ボタン・テストメソッド・最小実験UIを削除。
※完了: ArchTestTextureBtn（XAML）と ArchTestTexture_Click 本体、専用ヘルパー PaletteSummary を削除。
左パネルの最小実験文言も実機能の説明文に差し替え。Tab4 の本番ボタンに混線・結線ズレは無いことを確認。
ArchTestTexture_Click から連鎖して未使用化した可能性のあるクラス側メソッド
（StructureNbtReader.* / PonderRuleExtractor.Analyze 等）は当時 Tab5本番や他経路の参照可能性が
あるため未着手とした。→ フェーズGで grep により参照確認のうえ、StructureNbtReader /
PonderRuleExtractor は孤児と判明したためファイルごと削除済み（下記フェーズG参照）。

#### 種別選択の整理（プリミティブ廃止）※完了
ArchKindCombo の選択肢を3択から2択に整理。
- 旧: 0=簡易建築 / 1=プリミティブ（球・ドーム等曲面） / 2=生成AI（GLBから）
- 新: 0=簡易建築 / 1=生成AI（GLBから）
理由: 球体単体の生成は実用性が薄く、ドーム屋根は既に SPEC 側
（StructureExpander.BuildDomeRoof, roof_type="dome" / dome_height）に統合済みのため、
プリミティブを独立選択肢として残す必要がない（確認済み）。
変更:
- MainWindow.xaml: ArchKindCombo から「プリミティブ」ComboBoxItem を削除。
- ArchKind_SelectionChanged: 生成AI判定を SelectedIndex==2 → ==1 に修正（index 繰り上げ対応）。
- ArchGenerate_Click: 生成AI分岐を ==2 → ==1 に修正。isPrimitive 分岐
  （GeneratePrimitiveMultipleAsync 呼び出し）を削除し、常に GenerateMultipleAsync
  （簡易建築=SPEC展開）へ寄せた。
未削除→削除済み（フェーズG）: GeneratePrimitiveMultipleAsync / GeneratePrimitiveAsync /
PrimitiveExpander / PrimitiveSpec は当時本体を残置していたが、フェーズGで参照確認のうえ
削除済み。曲面表現は SPEC 側ドーム屋根に統合済みのため失われていない。
コミット: chore(arch): 種別選択からプリミティブを廃止し簡易建築/生成AIの2択に整理

### フェーズG: 不要コード・孤児クラスの掃除 ※完了
第2〜4段階・種別整理で参照元が消えたデッドコードと孤児クラスをまとめて削除。
削除のたびにビルド確認 → コミット。全工程ビルド通過済み。

削除内容:
- ArchitectGenClient.cs: プリミティブ生成経路（GeneratePrimitiveAsync /
  GeneratePrimitiveMultipleAsync）と、未使用だった TryParse（GeneratedBlock版）/
  TryParsePrimitive を削除。種別からプリミティブを廃止し呼び出し元が消えていた。
- MainWindow.Machine.cs: 未使用の Ponder 解析デッドコードを削除。
  フィールド _ponderRulesCache / _ponderStatsCache / _ponderCacheKey、
  メソッド ComputePonderKey / GetPonderAdjacencyRules / MachineRescanPonder_Click。
  MachineGenerate_Click 内の「[一時確認] プロパティlog」ブロック（仕様確定後削除と明記済）も除去。
- MainWindow.xaml: Ponder再スキャンボタン（MachineRescanPonderBtn）を削除。
- ファイルごと削除（全リポジトリ grep で外部参照ゼロを確認のうえ実施）:
  - Architect/Generation/PrimitiveSpec.cs
  - Architect/Generation/PrimitiveExpander.cs
  - Architect/Generation/StructureNbtReader.cs（ReadFile 含め呼び出し元なしを確認）
  - Architect/Generation/PonderRuleExtractor.cs

確認事項:
- StructureExpander は PrimitiveExpander を参照していない（ドーム屋根は自前の
  BuildDomeRoof + Outside で完結）。曲面表現は失われていない。
- StructureNbtReader / PonderRuleExtractor は grep で自ファイル内参照のみ＝孤児だった。
- using ModSorter.Architect（Machine.cs）は ArchitectModeHost で現役のため残置。
- 未使用 using 警告が出た場合のみ該当行を個別除去（現状は未対応で問題なし）。

コミット:
- chore(arch): 未使用のプリミティブ生成経路と一時確認ログを削除
- chore(machine): 未使用のPonder解析デッドコードと再スキャンボタンを削除
- chore(arch): 未使用のプリミティブSpec/Expanderを削除
- chore(arch): 未使用のPonder解析クラス(StructureNbtReader/PonderRuleExtractor)を削除

### 第4段階: 建築モードの設置導線 ※完了（実機確認済み）
共通ヘルパー ResolveSchematicOutPath(rawName, defaultName) を MainWindow.Machine.cs に新設し、
ResolveMachineOutPath はそのラッパに変更（機械側の挙動は不変）。
Tab4 に出力ファイル名欄 ArchNameBox と出力ボタン ArchExportBtn を追加。
ShowCase で表示中の案インデックス（_archShownCaseIndex）を記録し、成功案表示時に ArchExportBtn を有効化。
ArchExport_Click は表示中の案（List<GeneratedBlock>）を StructureNbtWriter.Block へ詰め替えて出力。
GeneratedBlock は座標+IDのみで向き状態を持たないため Properties は未設定（StructureNbtWriter は
Properties=null を null安全に扱うことを現物確認済み）。出力先決定は機械側と共通。
実機確認済み: 「生成→案表示→名前指定→出力→ゲーム内（Create Schematic）設置」を一巡確認。
10×10×10 / 15×15×15 で設置成功。新規出力直後はゲーム側の schematics 一覧反映にラグがあり、
リスト再読み込みで拾われる（ツール側の不具合ではない）。デフォルト名 building 固定のため
連続出力は同名上書きになる点に留意（上書き確認モーダルで「いいえ」を押すと保存中止）。

### 第5段階: 2パス生成化（精度向上・優先）※完了（実機評価済み）
1パス（指示→SPEC一発）を2段に分割。
1パス目 PlanAsync: 指示 → 設計方針 DesignPlan（design_notes / stories / style / roof /
decoration / openings）を JSON で出す。失敗時は plan=null で従来の1パス相当にフォールバック。
2パス目 GenerateAsync: DesignPlan を「DESIGN PLAN」節としてプロンプトに添えて StructureSpec(JSON) を出す。
GenerateMultipleAsync は PlanAsync を1回だけ呼び、3案で同一 plan を共有（variant は temperature と
方向性ヒントで差を出す）。改修は ArchitectGenClient のみ。StructureExpander 以降は不変。
新規 DTO: Architect/Generation/DesignPlan.cs。
実機評価結果: 所要時間・成功率・方針反映・3案ブロック数いずれも不満なし。
コミット: feat(arch): 建築生成を2パス化（設計方針フェーズPlanAsyncを追加・3案で共有）

### 第6段階: SPEC語彙の拡張（案B＝単一箱に「形の語彙」を足す）
【方針転換・確定】当初案（StructureSpec を box/tower/wing の parts リストに拡張し合成展開）は
撤回。参照サイト BlockArchitect.de の設計思想（幾何プリミティブ、~25ブロックの単一subject、
巨大合成は世界内でパーツ合体）に倣い、単一箱モデルを保ったまま「屋根・開口部・全体形状」の
語彙を増やす案Bを採用。リスクが低く既存案を壊さず、実用建築の見栄えに直結する。

参照サイトが「強い」と挙げた要素を、箱モデルとの相性で3群に仕分け:
- 第1群（箱の屋根・開口部に乗る）: houses/towers/castles/temples/city walls（既存様式で表現可）、
  pyramids（pyramid屋根）、domes（実装済）、arches（arch開口）。
- 第2群（箱を別形状に切替＝structure_type 新設が必要）: bridges / ramps / pools / stadiums。
- 第3群（曲面装飾オブジェクト）: rainbows / waves。

#### 段階6-1: ピラミッド屋根＋アーチ開口 ※完了（実機確認・おおむね良好）
- StructureExpander: 屋根分岐に roof_type="pyramid"（四角錐、底面を塞いでから全周1マスずつ絞る
  BuildPyramidRoof）を追加。ApplyOpening に Opening.Kind="arch"（床から立つ縦長開口、上端を
  左右1マスずつ詰めて曲線風）を追加。既存 flat/gable/dome・door/window には非干渉。
- ArchitectGenClient: プロンプトの roof_type 説明に pyramid、openings の kind 説明に arch を追記。
- 対象ファイル: StructureExpander.cs / ArchitectGenClient.cs。
コミット: feat(arch): SPEC語彙にピラミッド屋根とアーチ開口を追加

#### 段階6-2: 全体形状モード structure_type（未着手・次の作業）
StructureSpec に structure_type（"building"既定 | "bridge" | "ramp" | "pool" 等）を新設し、
StructureExpander.Expand 冒頭で「箱を建てる」か「特殊形状を建てる」かを分岐。
bridge / ramp / pool / stadium を1形状ずつ実装・コミット（実用頻度なら bridge か ramp を先に）。

#### 段階6-3: 曲面装飾（未着手）
rainbow（同心の半円アーチ・多色）、wave（正弦の帯）。曲面ロジックが要るため最後。

### フェーズG: Create機械側の掃除 ※完了（上記フェーズG参照）
撤回済み Ponder デッドコード・一時確認ログ・孤児クラスの削除はすべて実施済み。
残るは未使用 using 警告が出た場合の個別除去のみ（現状は問題なし）。
diagnostics/*（一時出力）と schematics/*（本番出力）のパス分離は ResolveSchematicOutPath で明確化済み。

---

## 6. 進行状況（完了済み）

- PoC（向き検証）、create_power_rules.txt 第1層、ModuleGenerator へのルール添付。
- Tab5（Create機械）を独立パネルとして新設。お題＋空間サイズ(X/Y/Z) → 生成 → 構造NBT出力 →
  出力フォルダを開く、まで結線。範囲外バリデーション動作。
- create_power_rules.txt に water_wheel / large_water_wheel の facing と軸の対応ルールを追加。
- Tab5 に LLMモデル選択欄（Ollama）。ListModelsAsync() を使い回し、選択モデルで生成。
- ConnectionValidator + AutoFix（millstone / mixer / press の3機種、検証・補正・収束まで確認）。
- 設置導線: Tab5 に MachineNameBox、Tab4 に ArchNameBox。生成NBTを .minecraft/schematics/<名前>.nbt へ
  直接出力する共通ヘルパー ResolveSchematicOutPath を実装。同名は上書き確認モーダル、instancePath
  未設定時は diagnostics へフォールバック、名前サニタイズ済み。機械・建築とも実機確認・コミット済み。
  → 「自然言語→生成→検証&AutoFix→schematics出力→ゲーム内設置」の MVP 一巡が機械・建築の両系統で成立。
- Ponder 隣接ルールの生成プロンプト合流は撤回し、関連デッドコード・孤児クラスをフェーズGで削除済み。
- 建築モード Tab4 の棚卸し（第1段階）と掃除（第2・3段階・種別整理・フェーズG）完了。
- 建築生成の2パス化（第5段階）完了・実機評価済み。
- SPEC語彙拡張 段階6-1（ピラミッド屋根・アーチ開口）完了・実機確認済み。

---

## 7. 残課題（後回し可）

### 建築モード側（次の主作業）
- 段階6-2: structure_type 新設 → bridge / ramp / pool / stadium を1形状ずつ。
- 段階6-3: rainbow / wave の曲面装飾。
- 任意: 6-1 の微調整（アーチの曲線・ピラミッドの絞り具合）。実機で気になる点が出たら対応。

### Create機械側
- crushing_wheels（RequiresFunnelOutput 登録済み・未検証）の実機確認。
- saw / deployer 等の構成確定。
- 出力マーカー lime_wool を depot 隣へ寄せる精緻化（RemoveTarget＋AddBlocks で移動表現可能）。
- press の basin 構成（圧縮レシピ）。生成段階でレシピ判別不可のため、press はデフォルト
  「press→空気→depot/belt」固定。basin 化は mixer の C-2 と同形で後付け可能。
- mixer 横に LLM が millstone のクセで置く funnel+depot は basin 構成では不要だが、害が無い
  （動力・出力に干渉しない）ため強制削除しない方針（補正回数の節約）。
- cogwheel の直列は「ここから繋がる」表現として許容（仕様内）。

---

## 8. ゴール

UI 上で自然言語のお題と空間サイズ(X/Y/Z)を入力し「生成」を押すと、指定サイズ内に、動力が
正しく繋がり加工物が正しく出力される機械（shaft/cogwheel/動力源/funnel/basin が axis/facing 付きで
接続）が生成され、schematics へ NBT 出力し、ゲーム内へ設置できるところまでを通しで完成させる。
建築モードも同様に、整理されたコードで建築データを schematics へ出力し、ゲーム内設置まで通せる状態を目指す。

---

## 付録A: Tab4（建築モード）ボタン⇔Clickハンドラ 対応表（第1段階の成果）

現物（MainWindow.xaml の Tab4 全数 / MainWindow.Architect.cs）で照合済み。
実ファイルは約16KB（引き継ぎメモの「約56KB」は誤り）。

| ボタン (x:Name) | ラベル | ハンドラ | 区分 |
|---|---|---|---|
| (無名) | ← 戻る | Back_Click | 共通(本番) |
| ArchModelRefreshBtn | 再取得 | ArchModelRefresh_Click | 本番（モデル一覧更新）|
| ArchPickBlocksBtn | ブロックを選択... | ArchPickBlocks_Click | 本番（ブロック選択）|
| ArchGenBtn | 3案を生成 | ArchGenerate_Click | 本番（生成の中核・2分岐）|
| ArchGalleryBtn | 画像ギャラリー | ArchGallery_Click | 本番（彫刻フロー入口）|
| ArchCase1Btn | 案1 | ArchCase_Click (Tag=0) | 本番（案切替）|
| ArchCase2Btn | 案2 | ArchCase_Click (Tag=1) | 本番（案切替）|
| ArchCase3Btn | 案3 | ArchCase_Click (Tag=2) | 本番（案切替）|
| ArchExportBtn | 表示中の案を schematics に出力 | ArchExport_Click | 本番（出力・第4段階で追加）|
| ArchOpenPreviewBtn | 3Dプレビューを開く | ArchOpenPreview_Click | 本番（プレビュー）|

（ArchTestTextureBtn / ArchTestTexture_Click は第3段階で廃止済み。表からも除外。）

ComboBox/CheckBox 由来の本番ハンドラ: ArchKind_SelectionChanged、ArchGenre_SelectionChanged。
（ArchFacadeCombo / ArchModelCombo / ArchResolutionCombo / ArchWindowToggle はハンドラ無し、参照のみ）

NavArchitect_Click は Tab0 の「建築モード」ボタン（Tab4 への遷移・遅延起動）。Tab4 内ではない。

本番系の補助メソッド（掃除対象外）: SetCaseButtonsEnabled, ShowCase, RenderArchPreviewAsync,
BuildTextureMap, GenerateSculptureAsync, RunSculptureFromImagesAsync, GetSculptResolution,
FindVanillaJar, DiagPath, ContainsJapanese, LoadArchModelsAsync, LoadArchGenres, UpdateBlocksSummary。

ArchGenerate_Click の分岐（ArchKindCombo の SelectedIndex）※プリミティブ廃止後の現状:
0=簡易建築（GenerateMultipleAsync・2パス）/ 1=生成AI・彫刻（GenerateSculptureAsync）。
（旧 index1 のプリミティブは廃止。詳細は第3段階「種別選択の整理」を参照。）
