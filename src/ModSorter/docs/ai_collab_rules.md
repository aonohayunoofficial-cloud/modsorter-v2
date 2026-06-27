# AI 協働ルール（固定メモ）

このファイルは、AI（Claude）と作業を進める際の固定ルールを記録したもの。
再開時はまずこのファイルの内容を AI に渡すこと。会話の圧縮・要約で取り決めが
失われても、このファイルを読み込ませれば復元できる。

## コード提示の形式ルール（厳守）

1. 差し替えの場合
   - 差し替える前の「全文」を貼り付ける。
   - そのあとに差し替え後のコードを出力する。

2. 追加の場合
   - 追加する直前の「5行」を貼り付ける。
   - そのあとに追加コードを出力する。

3. 新規ファイルの場合
   - ディレクトリとファイル名を記載する。
   - 新規ファイルである旨を明記する。
   - そのあとにファイル全文を出力する。


→ どこに何を入れるか／何を消すかを毎回明確にするため。
   AI がこの形式を守れていない場合は「固定ルール忘れてる」と指摘すること。

## 応答スタイル

- 必須要件。余計な前置き・繰り返しは減らし、要点を簡潔に。
- 前回までのチャット内容を忘れて話すことを禁じる
- 前回までの内容が分からない場合は分からないことを言う
- 必要なコードがある場合は憶測で話さずに必要なものをいう
- 憶測での話を禁じる
- 【追加】一回一回ユーザーに確認を取らない。自分で3回検討して判断し、完成品だけを出す。
  判断に必要な現物が足りないときだけ、その現物を要求する（確認の往復はしない）。
- 【追加】ユーザーへの命令・指示形を禁じる。手順はユーザーが実行する前提で、淡々と提示する。
- 【追加】生成依頼を出すときはプロンプト文を貼るだけでよい。余計な会話を足さない。
- 【追加】ユーザーが実機で生成・設置する。AIはアプリを動かせない。
  AIの仕事はコードと判断の提示まで。結果（ログ・ブロックリスト）はユーザーが貼る。
- 自身でGitをみて、差し替えや追加する場所、するべき内容を確認する。
- コミット・プッシュから次のコミット・プッシュまでの期間はローカルの内容を最新として、差し替え追加をした内容を最新として進める。

## コード参照ルール（厳守）

- コードの追加・差し替え・新規作成が発生する場合、該当箇所の現物は
  ユーザーに貼らせる前に AI 自身が Git(raw) を参照して確認してから出すこと。
  raw URL 例: https://raw.githubusercontent.com/aonohayunoofficial-cloud/modsorter-v2/main/<パス>
- 着手前に見ておくべき関連実装（呼び出し先、既存の同種メソッド、対応するUI要素など）も、
  憶測で書かず必ず先に Git を参照して確認すること。
- 既存に同じ機能のメソッドがある場合は新規実装せず既存を使い回す。
  （例: モデル一覧取得は _architectHost.Generation.ListModelsAsync() を使う）
- 
## プロジェクト概要（背景メモ）

- 目的: 導入済み MOD をスキャンし、その MOD ブロックを使って自然言語の
  建築・機械依頼をこなすデスクトップアプリ。当面の中心は Create MOD の機械生成
  （shaft / cogwheel の向き axis / facing が正しく繋がる骨格を生成する）。
- 主要ファイル:
  - src/ModSorter/Clients/ModuleGenerator.cs … プロンプト組立・生成本体
  - src/ModSorter/Architect/Generation/PonderRuleExtractor.cs … Ponder隣接統計の解析＋ルール文化
  - src/ModSorter/Architect/Generation/StructureNbtReader.cs … Ponder NBT読み取り
  - src/ModSorter/Architect/Rules/create_power_rules.txt … 動力ルール第1層（伝達）
  - src/ModSorter/MainWindow.xaml … 画面（Tab5 = Create機械の独立パネル）
  - src/ModSorter/MainWindow.Machine.cs … Tab5 の処理（生成・Ponderキャッシュ・出力フォルダ）
  - src/ModSorter/ModSorter.csproj … create_power_rules.txt のコピー設定あり
- ルールファイルの扱い:
  - create_power_rules.txt は Architect/Rules/ に置き、ビルド時に出力フォルダへコピー。
  - 実行時は AppContext.BaseDirectory 基準で読み込み、ModuleGenerator が
    プロンプト先頭に powerRulesSection として添付する。
  - 【重要・確定方針】Ponder 隣接統計を生成プロンプトに合流させる路線
    （adjacencySection / ToRuleText / GetPonderAdjacencyRules）は試して失敗し、撤回済み。
    動いていた生成を壊しただけだったため、生成プロンプトには Ponder を渡さない。
    配置精度は create_power_rules.txt の人間語ルールを足す方向で詰める。
    この路線には戻らないこと。
- ブロックID表記: create: 付きの完全ID に統一する。
- 動力源の出力数値: 0.5.1 wiki 基準の「目安」のまま保持（後でゲーム内実値に更新予定）。

## 進行状況メモ（随時更新）

- 完了: PoC（向き検証）、create_power_rules.txt 第1層、ModuleGenerator へのルール添付。
- 完了: Tab5（Create機械）を独立パネルとして新設。お題＋空間サイズ(X/Y/Z)入力 →
  生成 → 構造NBT出力 → 出力フォルダを開く、まで結線。範囲外バリデーションも動作。
- 撤回: Ponder隣接ルールの生成プロンプト合流は失敗のため撤回（ModuleGenerator から削除）。
  GetPonderAdjacencyRules 等のコードは未使用のまま残置、フェーズGで掃除予定。
- 完了: create_power_rules.txt に water_wheel / large_water_wheel の facing と軸の
  対応ルールを追加。
- 完了: Tab5 に LLMモデル選択欄（Ollama）を追加。
  _architectHost.Generation.ListModelsAsync() を使い回し、選択モデルで生成。

- 完了【接続検証 ConnectionValidator】: shaft/cogwheel の軸整合 と 動力入力面 と
  出力経路を検証し、機械的に直せるものは AutoFix で補正する仕組みを実装。
  ファイル: Architect/Generation/ConnectionValidator.cs / ConnectionSpec.cs / ValidationIssue.cs

- 完了【実機で確定した Create 機械の正しい構成】（全て実機 or 公式Wikiで確認済み）:
  - millstone … 隣接 andesite_funnel(extracting=true, 外向き facing) →
                funnel の真下(y-1) に depot/chest。funnel の横はダメ。
  - 無印funnel … facing(取り付け面) と extracting で向きが決まる。shape は持たない
                （shape は belt funnel 専用。無印に付けたら削除する）。
  - mechanical_press … 動力は facing軸の両端2面のみ（south/north→相手axis=z,
                east/west→相手axis=x）。出力は press(y)→空気(y-1)→depot/belt(y-2)。
                press は真下に1マス作業空間を空け、その下のアイテムを叩く（直下密着は不作動）。
                press 隣接の funnel は不要なので撤去する。
  - mechanical_mixer … プロパティ無し・軸はY固定。動力は側面4面に cogwheel(axis=y)。
                上下面は動力不可。出力は mixer(y)→空気(y-1)→basin(y-2) の縦並び。
  - basin … 出力に funnel 不要。basin 横の空気ブロックの真下にある belt/depot へ
                spout で自動排出（公式Wiki: Basin）。

- 完了【AutoFix の機能】:
  - RemoveTarget … 不正ブロック削除
  - SuggestedBlockId / SuggestedAxis … 種別変換・軸補正
  - SuggestedProps … 任意プロパティ上書き（funnel の facing/extracting 等）
  - RemoveProps … 不要プロパティ削除（無印funnel の shape 等）
  - AddBlocks … 新規ブロック追加（mixer 側面 cogwheel、mixer 下 basin、basin 斜め下 depot、
                press 下 depot）
  - mixer 専用: 上下面の不正 shaft を削除し、空き側面に cogwheel(axis=y) を追加
  - MainWindow.Machine.cs: AutoFix 収束ループ（issue が出続ける限り再 AutoFix）

- 完了【検証＆AutoFix 対応機種】: millstone / mechanical_mixer / mechanical_press の
  3機種すべて、動力入力面と出力経路を実機準拠で検証・自動補正・収束まで確認済み。

- 完了【フェーズE 設置導線】: Tab5 に出力ファイル名入力欄(MachineNameBox)を追加。
  生成NBTを .minecraft/schematics/<名前>.nbt へ直接出力する ResolveMachineOutPath を実装。
  同名ファイルがあれば上書き確認モーダル（MessageBox YesNo）を出し、拒否時は保存中止。
  instancePath 未設定時は diagnostics へフォールバック。名前サニタイズ（不正文字除去・
  空欄時 module_machine）も実装。実機で指定ファイルへの出力を確認、コミット・プッシュ済み。
  → これで「自然言語入力→生成→検証&AutoFix→schematics出力→ゲーム内設置」のMVP一巡が成立。

- 課題: cogwheel の直列は「ここから繋がる」表現として許容（仕様内）。
- 課題: 出力マーカー(lime_wool)を depot の隣へ寄せる精緻化が未実装（後回し可）。
  ※「出力」の意味上 depot 近傍にあるべき。RemoveTarget＋AddBlocks で移動表現可能。
- 課題: press の basin 構成（圧縮レシピ）は未実装。生成段階でレシピ判別不可のため
  press はデフォルト「press→空気→depot/belt」固定。basin 化は mixer の C-2 と同形で後付け可能。
- 課題: mixer 横に LLM が millstone のクセで置く funnel+depot は basin 構成では不要だが、
  害がない（動力・出力に干渉しない）ため強制削除しない方針（補正回数の節約）。


## 次回やるべきこと（TODO）

明日以降は「建築モードの整理」と「建築モードの設置導線」を中心に進める。
実装はすべて C#（.NET / fNbt / WPF）。Python は使わない。

### 最優先: 建築モード（Tab4）のテストコード整理（フェーズG相当）

- 建築モードに最小実験用のテストUI・テストコードが多数残っており、機能が混線している。
- 不具合: 「テクスチャ取得テスト」ボタン(ArchTestTextureBtn / ArchTestTexture_Click)を押すと、
  本来のテクスチャ取得ではなく機能モジュールのテストが走ってしまう。
  → まず Clickハンドラの中身を現物確認し、誤って呼んでいる処理を特定して切り離す。
- Tab4 左パネルの「最小実験: 指示+ブロックリスト → 座標+ID JSON が返るか確認」等、
  テスト目的の文言・ボタン類を棚卸しし、除去対象か残置かを一つずつ判定する。
- 他にもボタンと実処理の結線ズレが潜在している前提で、Tab4 の各 Clickハンドラを順に確認する。

### 次点: 建築モードの設置導線（schematics 出力）

- 建築データ（建物・プリミティブ・生成AIのボクセル結果）を
  ゲーム内 schematics フォルダへ名前指定で出力できるようにする。
- Create機械側で確立した ResolveMachineOutPath と同じ仕組みを建築モードにも適用。
  共通化できる部分は共通の出力ヘルパーに寄せ、二重実装を避ける。

### Create機械側の残課題

- crushing_wheels（RequiresFunnelOutput 登録済み・未検証）の実機確認。
- saw / deployer 等の構成確定。
- lime_wool 出力マーカーを depot 隣へ寄せる件。
- mixer 隣接 funnel の強制削除可否の判断。

### フェーズG 仕上げ（コード掃除）

- 撤回済み Ponder デッドコード（GetPonderAdjacencyRules 等）の除去。
- MainWindow.Machine.cs に残る [プロパティ] 一時確認ログ（仕様確定後に削除と明記済み）の除去。
- Nullable 警告・未使用 using・未使用変数の一掃。
- diagnostics/*（一時出力）と schematics/*（本番出力）のパス分離を明確化。


## 追加するルール／方針

- 設置導線は Create機械と建築モードで共通の出力ヘルパーに寄せ、二重実装を避ける。
- テスト用コード・最小実験UIは「本機能と混線するもの」を最優先で切り離し、
  判断がつかないものは一覧化してから一括判定する。
- ボタンと実処理の結線ズレ（テクスチャ取得テスト→機能モジュールテストのような誤呼び出し）が
  他にも潜在している前提で、Tab4 の各ボタンの Clickハンドラを順に現物確認する。


## 進め方（段階ゴール）

各段階で「動く状態」を確保しながら進める。

第1段階(機械接続の正確化・完了):
  millstone / press / mixer の「動力入力・出力経路」を実機準拠で検証＆自動補正。3機種完了。
  次の対象機械（crushing_wheels / saw / deployer 等）が出たら、
  同じく実機で構成を確定してから検証ルールを足す。

第2段階(MVPの通し・完了):
  UIでお題＋空間サイズ入力 → ルール添付で生成 → 検証＆AutoFix → schematics出力 →
  ゲーム内設置まで一本で通る（Create機械で達成）。

第3段階(建築モードの整理と設置導線・次の主作業):
  建築モードのテスト/診断コードを整理し、機能の混線を解消。
  建築データを schematics へ名前指定で出力できるようにする（Create機械の導線を再利用）。

第4段階(実用化・発表準備):
  MODスキャンの汎用化とルール層のMOD非依存化。パス設定UI・仕上げ・リリース確認。
  コード掃除は主要パスが動く状態を確認した後に行い、掃除後に再度通して壊れていないことを確認する。


## ゴール

UI上で自然言語のお題と空間サイズ(X/Y/Z)を入力し「生成」を押すと、
指定サイズ内に、動力が正しく繋がり加工物が正しく出力される機械（shaft/cogwheel/動力源/
funnel/basin が axis/facing 付きで接続）が生成され、schematics へ NBT出力し、
ゲーム内へ設置できるところまでを通しで完成させる。
建築モードについても同様に、整理されたコードで建築データを schematics へ出力し、
ゲーム内設置まで通せる状態を目指す。
