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

- 余計な前置き・繰り返しは減らし、要点を簡潔に。

## プロジェクト概要（背景メモ）

- 目的: ローカル LLM（Ollama）で Create MOD の機械ブロック配置を生成する PoC。
  shaft / cogwheel の向き（axis / facing）が正しく NBT に出力されることを検証する。
- 主要ファイル:
  - src/ModSorter/Clients/ModuleGenerator.cs … プロンプト組立・生成本体
  - src/ModSorter/Architect/Rules/create_power_rules.txt … 動力ルール第1層（伝達）
  - src/ModSorter/ModSorter.csproj … create_power_rules.txt のコピー設定あり
- ルールファイルの扱い:
  - create_power_rules.txt は Architect/Rules/ に置き、ビルド時に出力フォルダへコピー。
  - 実行時は AppContext.BaseDirectory 基準で読み込み、ModuleGenerator が
    プロンプト先頭に powerRulesSection として添付する。
- ブロックID表記: create: 付きの完全ID に統一する。
- 動力源の出力数値: 0.5.1 wiki 基準の「目安」のまま保持（後でゲーム内実値に更新予定）。

## 進行状況メモ（随時更新）

- 完了: PoC（向き検証）、create_power_rules.txt 第1層、ModuleGenerator へのルール添付。
- 今後の候補:
  1) 第2層ルール（加工系ブロック・動力源の選び方）への拡張
  2) ponder_rules_raw.json を人間語ルールへ変換してルール集へ合流
  3) お題（userRequest）側の指定を充実させて生成の狙いを絞る
