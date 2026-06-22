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
  対応ルールを追加し、facing と shaft の axis・配置方向が一致するよう改善。
- 完了: Tab5 に LLMモデル選択欄（Ollama）を追加。建築モードと同じ
  _architectHost.Generation.ListModelsAsync() を使い回し、選択モデルで生成。
- 課題: cogwheel の直列は「ここから繋がる」表現として許容（仕様内）。
  出力マーカー(lime_wool)の置き場所の精緻化は後回しで可。

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

## 明日の進め方（段階ゴール）

明日は以下の順で、各段階で「動く状態」を確保しながら進める。

第1段階(MVP・最優先):
  UIでお題＋空間サイズを入力 → ルール添付で生成 → 指定サイズ内に
  向きの揃った機械骨格を生成 → NBT出力 → ゲーム内へ設置できるところまで通す。
  （フェーズA→C→D→E の主要パスを一本に繋ぐ）

第2段階(実用化):
  MODスキャンの汎用化とルール層のMOD非依存化（フェーズB残り＋F-1/F-2）。

第3段階(発表準備):
  コード掃除・テスト除去・整理（フェーズG）→ パス設定UI・仕上げ・
  リリース確認（F-3〜F-5）。掃除はフェーズEまで動く状態を確認した後に行い、
  掃除後に主要パスを再度通して壊れていないことを確認する。
  時間が尽きた場合はここを翌日へ持ち越す。

## 明日のゴール

UI上で自然言語のお題と空間サイズ(X/Y/Z)を入力し、「生成」を押すと、
指定サイズ内に向きの揃った機械の骨格（shaft/cogwheel/動力源が axis/facing 付きで接続）が
生成され、NBTとして出力し、ゲーム内へ設置できるところまでを通しで完成させる。
