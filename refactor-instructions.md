# refactor-instructions.md

plc-comm-computerlink-dotnet のリファクタリング指示書。
この文書は実装担当モデル向けの完結した作業指示である。実装前にこの文書全体を読むこと。

> **最重要の前提**: このライブラリは NuGet に公開済み(`PlcComm.Toyopuc` 0.1.8)であり、
> JTEKT TOYOPUC の Computer Link プロトコル実装は実機(PC10G 等)での検証記録
> (`TODO.md`、`internal_docs/`)に紐づく。
> **公開 API と送信フレームのバイト列を変えてはならない。**
>
> `ToyopucDeviceClient` の `object` ベースの API(`Read(object device)` 等)は
> Python 版(`plc-comm-computerlink-python`)との**意図的なクロススタック互換**であり、
> 型安全化のための「改善」は禁止する(`HIGH_LEVEL_API_CONTRACT.md` 整合の製品判断事項)。
> 本タスクの中心は **`ToyopucDeviceClient.cs`(2,286 行)内の純粋ロジック
> (パッキング・ランプラン計画)の内部分離とテスト追加**である。

---

## Objective

公開 API・ワイヤバイト列・Python 版との意味的互換を一切壊さずに:

1. **`ToyopucDeviceClient.cs` 内の純粋静的ヘルパ(ペイロードパッキング・ランプラン計画)を
   internal クラスへ move-only 分離する**
2. **分離した純粋ロジックに直接のユニットテストを追加する**(現状はクライアント経由のみ)
3. 単一テストファイル(`ProtocolAndClientTests.cs` 723 行)の整理は**任意・小規模**

「公開面の再設計」「`object` API の型安全化」「sync/async 構造の変更」は行わない。

---

## Project Understanding

### 何のライブラリか

JTEKT TOYOPUC PLC と Computer Link プロトコル(イーサネット TCP/UDP)で通信する
.NET 9 ライブラリ。中継局ホップ(relay)、FR 領域の読み書き + Commit、PC10 モード、
デバイスプロファイル(機種別アドレッシング)対応。Python 版と高レベル契約
(`open_and_connect` / `read_typed` / `write_typed` / `read_named` / `poll`)を共有。

### 利用者(壊すと影響が出る範囲)

1. **NuGet の一般利用者**(`PlcComm.Toyopuc` 0.1.8)
2. **plc-scope-dotnet**(`lib/` にリリース DLL を取り込む下流アプリ)
3. **クロススタック検証フロー**(Python 版との横並び実機検証)

### モジュール構成(src/Toyopuc/、計約 7,400 行)

| ファイル | 行数 | 内容 |
|---|---|---|
| `ToyopucDeviceClient.cs` | 2,286 | 高レベルクライアント: デバイス解決キャッシュ、ランプラン計画+キャッシュ、PC10 マルチビット/ワードのパッキング、relay/FR/Commit、`object` 値の正規化 |
| `ToyopucClient.cs` | 1,089 | 低レベル同期クライアント(トランスポート + コマンド) |
| `ToyopucClient.Async.cs` / `ToyopucDeviceClient.Async.cs` | 451 / 360 | async ラッパ(partial) |
| `ToyopucDeviceClientExtensions.cs` | 711 | 契約ヘルパ(`ReadTypedAsync` / `PollAsync` / single-request / chunked) |
| `ToyopucPlcProfiles.cs` / `ToyopucDeviceResolver.cs` / `ToyopucAddress.cs` | 612 / 476 / 564 | プロファイル・アドレス解決(データ + 純粋ロジック) |
| `ToyopucProtocol.cs` | 535 | フレーム組立・パース(純粋。健全) |
| ほか | — | Queued ラッパ、Factory、Options、Relay、Models、Errors |

### テスト / CI

- `tests/PlcComm.Toyopuc.Tests/ProtocolAndClientTests.cs`(723 行、単一ファイル)
- `run_ci.bat`: `dotnet build` → `dotnet test` → `dotnet format --verify-no-changes` →
  HighLevelSample publish(win-x64)
- `Directory.Build.props`: `TreatWarningsAsErrors=true`、アナライザ有効

### examples/

`SmokeTest`(1,498 行)/ `BitPatternProbe` / `SoakMonitor` 等は実機検証ツール。
**触らない**(ビルドが通ることのみ維持)。

---

## Behaviors To Preserve(絶対に壊さない既存挙動)

1. **公開 API**: すべての public 型・メソッド・シグネチャ・既定値
   (`object` ベースの `Read` / `Write` / `RelayRead` 系を含む)。
2. **送信フレームのバイト列**: `ToyopucProtocol` とパッキングヘルパの出力。
3. **ランプラン(バッチ化)の分割規則**: PC10 ブロック境界の split 条件、
   連続判定(`GetBatchRunLength` / `CompileRunPlan`)。読取回数・順序が変わると
   実機挙動(FR Commit のタイミング等)に影響しうる。
4. **キャッシュセマンティクス**: `_resolvedDeviceCache`(最大 512)/
   `_runPlanCache`(最大 256)の上限とキー形式。
5. **FR 書込の特別扱い**(`_raise_generic_fr_write_error` 相当のガード、Commit/wait)。
6. **セマンティック原子性**(`HIGH_LEVEL_API_CONTRACT.md`): 暗黙のフォールバック分割禁止。
7. **NuGet パッケージ ID・バージョン・CHANGELOG**: 本タスクで変更しない。

---

## Non-Negotiables(交渉不可の制約)

- 最初に `git status` を確認する。未コミット変更があれば混ぜず、報告して停止する。
- 編集前に Baseline Commands をすべて実行し、結果(テスト件数含む)を記録する。
- 変更は小さく戻しやすい単位。コミットはユーザーの指示があるまで行わない。
- 無関係な整形・「ついで」リファクタリングをしない(`dotnet format` 既定に従う以外の整形禁止)。
- NuGet 依存を追加しない。csproj / `Directory.Build.props` を変更しない。
- 分離した型の可視性は `internal` まで。`public` にしない
  (`InternalsVisibleTo` が無い場合はテストプロジェクトへの追加のみ可、要報告)。
- 既存テストの既存アサーションを変更しない(追加のみ可)。
- 実機 PLC への接続を行わない。
- 正しさが不明な場合は実装を止め、「Stop And Ask」として質問を報告書に書く。

---

## Stop And Ask Conditions(即時停止して質問する条件)

- 移動対象の「静的ヘルパ」が実はインスタンス状態(キャッシュ・オプション)に依存していた
- 特性テスト作成中に、パッキング出力やランプラン結果が Python 版・文書の記述と
  食い違って見えた(**修正せず**報告)
- 既存テストが自分の変更後に落ちた ⇒ 即座に巻き戻して報告
- 公開 API・フレームバイト列・分割規則に影響しうる変更が必要に見えた
- `object` API の型の扱い(`ToInt32Invariant` 等)に文化依存などの疑義を見つけた(報告のみ)
- 本書の Debt Map に無い大きな問題を発見した(報告のみ)

---

## Baseline Commands

作業ディレクトリ: リポジトリルート。.NET 9 SDK。Windows 推奨(`run_ci.bat` の publish が
win-x64)。実機 PLC 不要・接続禁止。

```powershell
git status                                          # クリーンであることを確認
dotnet build PlcComm.Toyopuc.sln
dotnet test PlcComm.Toyopuc.sln --no-build          # テスト件数を記録
dotnet format PlcComm.Toyopuc.sln --verify-no-changes
```

可能なら `run_ci.bat` をフル実行(publish 含む)。不可なら未実施と報告書に明記。

---

## Debt Map

行番号は調査時点(main, commit `a263b5b`)のアンカー。ドリフトしていたら宣言名で探すこと。

### D1. 純粋ロジックへの直接テスト不在 【実装可 / 最優先】

- **根拠**: `ToyopucDeviceClient.cs` 内の `PackWordValues`(502 行)/
  `BuildPc10MultiWordReadPayload`(514 行)/ `PackPc10MultiWordPayload`(527 行)/
  `PackPc10MultiBitPayload`(568 行)/ `GetBatchRunLength`(682, 703 行)/
  `CompileRunPlan`(783 行)等は private static の純粋関数だが、テストは
  クライアント統合経由でしか通らない。
- **改善案**: まず**現在の出力を固定する特性テスト**を追加する(D2 の前提)。
  private のままなら一時的にクライアント経由で、D2 完了後は直接テストに置き換える。
- **リスク**: 低(テスト追加のみ)。

### D2. `ToyopucDeviceClient.cs`(2,286 行)の責務集中 【実装可 / 主作業】

- **根拠**: 1 クラスに (a) デバイス解決+キャッシュ、(b) ランプラン計画+キャッシュ、
  (c) PC10 ペイロードパッキング、(d) relay/FR/Commit の高レベル操作、
  (e) `object` 値正規化(`NormalizeWordValues` / `ToInt32Invariant` 等)が同居。
- **なぜ負債か**: (b)(c) は最も複雑な純粋ロジックなのに、(d) の公開面と混在して
  単独で読めず、テストもしにくい。
- **改善案**: move-only で internal static クラスへ分離する:
  - `Pc10Payloads`(internal static): パッキング/ビルド系 (c)
  - `DeviceRunPlanner`(internal static): `GetBatchRunLength` / `CompileRunPlan` /
    連続判定 (b)(キャッシュは**クライアント側に残す**。キー生成も挙動不変で移動可)
  - `ToyopucDeviceClient` は呼び出し側に置換。公開面 (d) とキャッシュ (a) は不動
- **影響範囲**: src 1 ファイル → 3 ファイル。公開 API 不変。
- **リスク**: 中。D1 の特性テスト完了後に着手。
- **検証**: 全テスト + 特性テスト + `dotnet format`。

### D3. 単一テストファイル(723 行) 【任意・小】

- **根拠**: `ProtocolAndClientTests.cs` にプロトコル・クライアント・拡張の全テストが同居。
- **改善案**: D1/D2 で**新規追加するテストは新ファイル**(例: `Pc10PayloadTests.cs` /
  `RunPlannerTests.cs`)に置く。既存ファイルの分割は**しない**(テスト資産の改変リスク)。

### D4. sync コア + `.Async.cs` ラッパの二重面 【現状維持 / 報告のみ】

- `ToyopucClient.cs`(同期)+ `.Async.cs`(非同期ラッパ)は構造的な選択であり、
  Python 版(同期コア + thin async)との対応もある。変更しない。

### D5. `object` ベースの公開 API 【現状維持 / 報告のみ】

- Python 版との互換を意図した設計。型安全なオーバーロード追加は公開面の変更であり、
  製品判断が必要。**提案として報告のみ可**。

---

## Implementation Phases

### Phase 0: 現状確認

1. `git status` 確認(クリーンでなければ停止・報告)
2. Baseline Commands を実行し、結果を記録

### Phase 1: 特性テスト(D1)

1. パッキング系・ランプラン系の代表入力(単独デバイス、連続、PC10 ブロック境界跨ぎ、
   ビット/ワード/バイト混在)について現在の出力を採取し、特性テストとして追加
2. 期待値は**現在の実装出力**を機械的に採取したものに限る(食い違いを見つけたら Stop And Ask)
3. 全テスト実行

### Phase 2: 内部分離(D2)

1. `Pc10Payloads` 分離 → 全テスト → `DeviceRunPlanner` 分離 → 全テスト
2. 想定外の状態依存が出たらその関数をスキップして報告

### Phase 3: 検証と報告

1. 全 Verification Requirements を最終実行
2. Reporting Format に従って報告書を作成

---

## Verification Requirements

各フェーズ完了時に最低限:

```powershell
dotnet build PlcComm.Toyopuc.sln
dotnet test PlcComm.Toyopuc.sln --no-build
dotnet format PlcComm.Toyopuc.sln --verify-no-changes
```

最終フェーズでは追加で:

- テスト件数が baseline から増えていること
- `git diff` で確認: 公開型・メソッドのシグネチャ無変更、csproj /
  `Directory.Build.props` / `CHANGELOG.md` 無変更、examples 無変更
- examples を含む sln 全体がビルドできること

---

## Reporting Format

1. **Baseline 結果**: 実行コマンドと結果(テスト件数)
2. **特性テスト一覧**: 対象関数 × 入力ケース × 採取出力
3. **分離一覧**: 移動した宣言と移動先(D2)
4. **各フェーズの検証結果**: 最後に実行したコマンドと結果(失敗を隠さない)
5. **Stop And Ask**: 発生した質問と停止範囲
6. **提案事項**: D5(型安全オーバーロード)等、実装しなかった改善案
7. **未実施事項**: `run_ci.bat` フル実行可否等

---

## Out-of-scope Items(やらないこと)

- 公開 API の変更・追加・整理(`object` API の型安全化を含む。提案のみ)
- 送信フレームバイト列・ランプラン分割規則・キャッシュセマンティクスの変更
- sync/async 構造(`.Async.cs` partial)の再設計
- `ToyopucProtocol` / `ToyopucAddress` / `ToyopucPlcProfiles` の変更
  (プロファイルデータは実機検証に紐づく)
- `examples/` の変更、既存テストファイルの分割・既存アサーション変更
- バージョン番号変更、`CHANGELOG.md` 更新、NuGet publish
- 依存追加、csproj / props 変更、CI 変更
- `internal_docs/` / `docsrc/` の変更
- 実機 PLC を使う検証
- 兄弟リポジトリ(python 版ほか)の変更
