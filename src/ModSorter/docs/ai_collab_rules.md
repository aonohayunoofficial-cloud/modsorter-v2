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
  ・millstone … 隣接 andesite_funnel(extracting=true, 外向き facing) →
                funnel の真下(y-1) に depot/chest。funnel の横はダメ。
  ・無印funnel … facing(取り付け面) と extracting で向きが決まる。shape は持たない
                （shape は belt funnel 専用。無印に付けたら削除する）。
  ・mechanical_press … 動力は facing軸の両端2面のみ（south→north/south, west→east/west）。
                出力は press 真下(y-1) の depot（板材デフォルト）。
  ・mechanical_mixer … プロパティ無し・軸はY固定。動力は側面4面に cogwheel(axis=y)。
                上下面は動力不可。出力は mixer(y)→空気(y-1)→basin(y-2) の縦並び。
  ・basin … 出力に funnel 不要。basin 横の空気ブロックの真下にある belt/depot へ
                spout で自動排出（公式Wiki: Basin）。

- 完了【AutoFix の機能】:
  ・RemoveTarget … 不正ブロック削除
  ・SuggestedBlockId / SuggestedAxis … 種別変換・軸補正
  ・SuggestedProps … 任意プロパティ上書き（funnel の facing/extracting 等）
  ・RemoveProps … 不要プロパティ削除（無印funnel の shape 等）
  ・AddBlocks … 新規ブロック追加（mixer 側面 cogwheel、mixer 下 basin）
  ・mixer 専用: 上下面の不正 shaft を削除し、空き側面に cogwheel(axis=y) を追加
  ・MainWindow.Machine.cs: AutoFix 収束ループ（issue が出続ける限り再 AutoFix）

- 課題: cogwheel の直列は「ここから繋がる」表現として許容（仕様内）。
  出力マーカー(lime_wool)の置き場所の精緻化は後回しで可。
- 課題: press の basin 構成（圧縮レシピ）は未実装。生成段階でレシピ判別不可のため
  press はデフォルト「真下 depot」固定。basin 化は mixer の C-2 と同形で後付け可能。
- 課題: mixer 横に LLM が millstone のクセで置く funnel+depot は basin 構成では不要だが、
  害がない（動力・出力に干渉しない）ため強制削除しない方針（補正回数の節約）。

    ・mechanical_press … 動力は facing軸の両端2面のみ（south/north→相手axis=z,
                east/west→相手axis=x）。出力は press(y)→空気(y-1)→depot/belt(y-2)。
                press は真下に1マス作業空間を空け、その下のアイテムを叩く（直下密着は不作動）。
                press 隣接の funnel は不要なので撤去する。
  ・完了【検証＆AutoFix 対応機種】: millstone / mechanical_mixer / mechanical_press の
                3機種すべて、動力入力面と出力経路を実機準拠で検証・自動補正・収束まで確認済み。


## 次回やるべきこと（TODO）

明日で「自然言語から機械が組み上がる」を完成させる。実装はすべて C#（.NET / fNbt / WPF）。
Python は使わない。フェーズ順に上から着手する。

### フェーズA: 入力仕様の確定（マスト）

A-1. お題（userRequest）入力の確定
   - 自然言語のお題を必須入力として受け取る。空欄は弾く。
A-2. 指定空間サイズ入力の確定（マスト）
   - 幅(X)・高さ(Y)・奥行(Z)を入力として受け取る。
   - 現状の座標0-8固定をやめ、指定サイズを上限としてプロンプト・検証の両方に反映。
   - 範囲外座標のブロックは生成後に弾く（バリデーション追加）。
A-3. GenerateAsync のシグネチャ変更
   - userRequest に加え、空間サイズ(sizeX, sizeY, sizeZ)を引数として渡せるようにする。
   - プロンプト内の座標制約文を固定値から動的生成に差し替え。

### フェーズB: ルール辞書の整備

B-1. PonderRuleExtractor に Properties 抽出を追加
   - assets/create/ponder/*.nbt から create: ブロックの
     ID → プロパティ名（axis / facing / waterlogged 等）→ 取りうる値 を抽出。
   - Block Entity の nbt タグは無選別で入れない。ダンプして機能に使える値だけ採用。
B-2. 全ブロックの BlockStates 抽出ユーティリティ
   - assets/create/blockstates/*.json を解析し ID → プロパティ → 取りうる値 を辞書化。
   - 出力先: diagnostics/block_palette.json（現状2014ブロック）。
B-3. ponder_rules_raw.json の安定性確認
   - 178件一括処理が毎回同じ結果になるか検証。出力がブレないことを確認。
B-4. ponder_rules_raw.json を人間語ルールへ変換 【着手済み】
   - 隣接統計を英語ルール文化(ToRuleText)し、生成プロンプトへ合流。
   - 接続位置の精度を上げる（駆動元の軸延長線上に置く等）チューニングが残る。
B-5. 第2層ルールの起草（加工系・動力源の選び方）
   - millstone / mechanical_press など加工系の入力面ルールを明文化。
   - 動力源の選定基準（必要su・rpmから逆算）を追加。

### フェーズC: UI改修（マスト）

C-1. お題入力欄の追加 【完了】
C-2. 空間サイズ入力欄の追加 【完了】
C-3. 生成実行ボタンの整理 【完了 / Tab5独立パネル】
C-4. 生成結果の表示 【完了 / 結果＋NBTパス＋エラー表示】

### フェーズD: 結合と検証

D-1. エンドツーエンド結線 【完了】
   - UI入力（お題＋サイズ）→ GenerateAsync → ルール添付 → 生成 → NBT出力 を一本に。
D-2. 生成品質の検証
   - 複数のお題・複数サイズで生成し、向き・構成・サイズ制約の遵守を確認。
   - shaft/cogwheel/動力源が axis/facing 付きで繋がっているかを確認。
D-3. 不具合修正と微調整
   - 検証で出た破綻（向き不整合・範囲外・未接続）をルール文 or 検証ロジックで潰す。

### フェーズE: ゲーム内への設置（マスト）

E-1. 出力NBTのスキマティック対応形式を確定
   - Create のスキマティック（.nbt）として読める構造で書き出す。
   - 既存の構造NBT出力（module_*.nbt）がそのまま設置に使えるか検証。
E-2. 設置導線の確立
   - 生成NBTをインスタンスの schematics フォルダへ出力 or コピーする処理を追加。
   - 出力先パスは instancePath 基準で動的に解決。
E-3. 実機設置の確認
   - schematic cannon または構造ブロックで読み込み、向き・接続が保たれて
     設置されることを確認。回る／動力が通ることを確認。

### フェーズF: 汎用化とアプリ仕上げ（実用化）

F-1. MODスキャンの汎用化
   - create 専用前提を外し、読み込んだ全MOD jar の blockstates から
     ブロック辞書を生成（minecraft / create / cobblemon 等を横断）。
   - 現状の MOD別件数集計・パレットキャッシュ生成を本導線に統合。
F-2. ルール層のMOD非依存化
   - create_power_rules.txt は Create 用ルールとして層分離し、
     対象MODが無い場合は添付しない／別MODルールに差し替え可能な構造にする。
F-3. インスタンス/jar パス設定UI
   - instancePath・versions パスをUIから指定・保存できるようにする。
   - jar が見つからない場合のエラー表示と再スキャン導線。
F-4. アプリとしての最低限の仕上げ
   - 設定の永続化、生成履歴 or 直近結果の保持、例外時の落ちない処理。
   - 発表用に最小限の操作説明（README）を整備。
F-5. リリース確認
   - クリーン環境でビルド・起動・スキャン・生成・設置まで通すスモークテスト。

### フェーズG: コード掃除・整理（発表前マスト）

G-1. テスト/診断コードの棚卸し
   - ArchTestTexture_Click などの診断・お試し系コードを列挙。
   - 本番導線に必要なもの／検証用に消すものを仕分け。
   - CORE-01 ブロックは Tab5 へ機能移行済みのため、診断からの撤去を検討。
G-2. 不要コードの除去
   - 使われていない診断ボタン・テストメソッド・デッドコードを削除。
   - 残す診断機能は「開発用」と分かる形に隔離（別領域 or フラグ管理）。
G-3. ファイル/責務の整理
   - Clients / Architect 配下の責務重複を整理。命名を統一。
   - 一時出力（diagnostics/*）と本番出力（schematics/*）のパスを明確に分離。
G-4. ビルド警告の解消
   - Nullable 警告・未使用 using・未使用変数を一掃。
G-5. 最終確認
   - 掃除後にフェーズEまでの主要パスが壊れていないか再度通す。

### フェーズH: 性能・キャッシュ

7. Ponder隣接ルールのキャッシュ化 【完了】
   - Ponder解析(178件)は重いため、初回のみ実行してメモリにキャッシュ。
   - 2回目以降の生成はキャッシュを使い、待ち時間を出さない。
   - Tab5 に「Ponder再スキャン」ボタンを置き、MOD追加時は手動で再構築。
   - キャッシュのフィンガープリントは Create本体jarのパス＋更新日時で判定。

## 進め方（段階ゴール）

各段階で「動く状態」を確保しながら進める。

第1段階(機械接続の正確化・進行中):
  millstone / press / mixer の「動力入力・出力経路」を実機準拠で検証＆自動補正する。
  ・millstone … 完了（funnel→真下depot）
  ・press … 動力入力（facing軸両端）完了。出力は真下depot。
  ・mixer … 動力入力（側面cog自動追加）完了。出力 basin（C-2）実装し実機確認待ち。
  次の対象機械が出たら、同じく実機で構成を確定してから検証ルールを足す。

第2段階(MVPの通し):
  UIでお題＋空間サイズ入力 → ルール添付で生成 → 検証＆AutoFix → NBT出力 →
  ゲーム内設置まで一本で通す（フェーズA→C→D→E）。

第3段階(実用化):
  MODスキャンの汎用化とルール層のMOD非依存化（フェーズB残り＋F-1/F-2）。

第4段階(発表準備):
  コード掃除・テスト除去（フェーズG）→ パス設定UI・仕上げ・リリース確認（F-3〜F-5）。
  掃除は主要パスが動く状態を確認した後に行い、掃除後に再度通して壊れていないことを確認する。

## ゴール

UI上で自然言語のお題と空間サイズ(X/Y/Z)を入力し「生成」を押すと、
指定サイズ内に、動力が正しく繋がり加工物が正しく出力される機械（shaft/cogwheel/動力源/
funnel/basin が axis/facing 付きで接続）が生成され、NBT出力し、ゲーム内へ設置できる
ところまでを通しで完成させる。
