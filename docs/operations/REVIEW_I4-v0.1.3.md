# I4: Result.OrElseのフォールバック例外

検証日: 2026-11-13 (JST)。v0.1.3のレビュー指摘を0.1.4向けに修正する項目です。
対象: AIKernel.Core、dev `dd8b258` から作成した `review/I4`。
既存のreview/I1、review/B4等は保持し、このブランチへ取り込んでいません。

## 再現と修正

Result.OrElse(Func<ErrorContext, Result<T>>)は、失敗時のfallback呼出しをtry/catchで囲んでおらず、
fallbackが投げた例外が呼出元へ漏れていました。
修正前の追加30件は21件失敗・9件成功、修正後は30件すべて成功です。

fallback呼出しの例外を捕捉し、既存のErrorContext.FromExceptionを使った失敗Resultに変換します。
新しいエラーのCodeはUNHANDLED_EXCEPTION、Messageは例外のMessage、IsRetryableはfalse、
Metadataのexception_typeは今回の例外型です。FailureKind / OriginStep / SemanticSlotは
既存のFromExceptionと同じく未設定にし、元の分類は以下の別キーへ保持します。

元の失敗情報はErrorContextの形を変えず、Metadataに保存します。

| キー | 内容 |
|---|---|
| source_error_code | 元のCode。既存のResultMetadataKeys.SourceErrorCodeを再利用 |
| source_error_message | 元のMessage |
| source_error_is_retryable | 元のIsRetryable（True / False） |
| source_error_failure_kind | 元のFailureKind |
| source_error_origin_step | 元のOriginStep |
| source_error_semantic_slot | 元のSemanticSlot |
| source_error_metadata.&lt;元のキー&gt; | 元のMetadataの各値 |

元の各項目がnullの場合は対応キーを追加せず、空文字は空文字のまま保持します。
元のError自体がないdefault Result / Fail(null)からfallbackが例外を投げた場合も、
UNHANDLED_EXCEPTIONとして返し、存在しない元エラー情報を作りません。

Metadataは例外処理時にOrdinalの新しい辞書へコピーします。
元の辞書を書き換えず、結果作成後に元の辞書が変更されてもコピーは変わりません。
元のexception_type等は接頭辞の下に保存するため、今回の例外型や元のCodeを上書きしません。
OrElseが繰り返し失敗した場合も、前の原因情報を同じ接頭辞で一段ずつ保存します。

## 維持する挙動

- 成功Resultではfallbackを呼ばず、元の値を返します。
- fallbackが普通に返したResultは、成功・失敗・defaultを含めそのまま返します。
- OrElse(Result<T>)の値を渡すオーバーロードは変更しません。
- null delegateは従来どおりArgumentNullExceptionです。引数検証は例外捕捉の外側に置きます。
- OperationCanceledException / TaskCanceledExceptionも、Map / Bind / Try等の汎用Result合成と同様に
  UNHANDLED_EXCEPTIONへ変換します。B4の実行境界におけるCanceled分類を変更するものではありません。
- 既存のErrorContext.FromExceptionそのものや、他のResult合成処理は変更しません。

公開済みインターフェース・DTO・enum、Canon、ハッシュ計算式・形式は変更していません。
追加実装はResultのprivate helperです。正常に返るfallbackのResultStep / ReplayLogHashも変わらないことを検証します。
これまで外へ投げていた例外が失敗Resultとして返る部分だけが今回の動作変更です。

## 追加テスト

`tests/AIKernel.Core.Tests/Common/ResultOrElseExceptionTests.cs` に30件を追加しました。
既存テストのコード・期待値は変更していません。

| テスト | 件数 |
|---|---:|
| 7種類の例外×値型・参照型、元の全情報・呼出し回数・キー衝突を検証 | 14 |
| default / Fail(null)に元エラーがなくても失敗Resultを返す | 2 |
| null項目と空文字を区別し、任意情報を捏造しない | 2 |
| 連続したfallback失敗で両方の原因を保持 | 1 |
| 結果作成後の元Metadataの変更から独立 | 1 |
| 成功時は例外を投げるfallbackも実行しない | 1 |
| fallbackが返した成功・失敗・defaultを維持 | 3 |
| 値を渡すオーバーロードの選択を維持 | 2 |
| 成功・失敗の両方でnull delegateの引数検証を維持 | 2 |
| 例外を失敗へ変換した後の成功側処理を実行しない | 1 |
| 非例外経路のStepId / ReplayLogHash / ReplayLog一致 | 1 |

7種類はInvalidOperationException、IOException、TimeoutException、OperationCanceledException、
TaskCanceledException、独自派生例外、AggregateExceptionです。

## 全体・下流検証

全9リポジトリのrestore / Release buildが成功しました。
テストは合計1,127件成功、DoomのS0既知失敗2件のみです。新しい失敗はありません。

| リポジトリ | restore | build | 成功テスト | 既知失敗 |
|---|---|---|---:|---:|
| NET | 成功 | 成功 | 173 | 0 |
| Core | 成功 | 成功 | 469 | 0 |
| Providers | 成功 | 成功 | 120 | 0 |
| Cuda13.0 | 成功 | 成功 | 21 | 0 |
| Control | 成功 | 成功 | 42 | 0 |
| Tools | 成功 | 成功 | 34 | 0 |
| Wasm | 成功 | 成功 | 64 | 0 |
| Demo | 成功 | 成功 | 71 | 0 |
| Doom | 成功 | 成功 | 133 | 2 |

Coreの469件は既存439件と追加30件です。既存テストの期待値は変更していません。
Doomの既知失敗は以下の2件で、HUD_UNIFORM_FLOAT_COUNTの期待値32とdevの実装36の差です。

- DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets
- DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping

Common / Core / Hosting / Kernelの4 DLLについて、ビルド元、検証用nupkg、
隔離キャッシュ、全77個のビルド出力のSHA-256一致を確認しました。
出力内訳: AIKernel.Common: 34 / AIKernel.Core: 30 / AIKernel.Hosting: 7 / AIKernel.Kernel: 6。
集計と照合結果は証跡ディレクトリのverified-results.jsonに記録しています。
各devのHEAD、元のreview/I1、既存テスト、ハッシュ計算コードも保持されていることを確認しました。

証跡: `D:\AIKernel\.codex_tmp\review-i4-20261113`。
Coreはreview/I4、他8リポジトリは各devのgit archiveによる隔離コピーを使いました。
Common / Core / Hosting / Kernelを検証専用フィードへpackして下流へ供給し、DLLを照合しました。
ローカル0.1.3再packは配布物ではなく、グローバルキャッシュや公開済みパッケージは置き換えません。
restore / Release build / testのログ、TRX、pack結果、各devのHEADを保存しています。

実行コマンドの主要部分:

```powershell
dotnet restore <solution> --configfile <scratch>/NuGet.config --packages <scratch>/packages --force --no-http-cache -p:NuGetAudit=false -m:2 -nr:false
dotnet build <solution> -c Release --no-restore -m:2 -nr:false -p:UseSharedCompilation=false
dotnet test <solution> -c Release --no-build --no-restore --logger trx --results-directory <scratch>/results -m:2 -nr:false
dotnet pack <core-project> -c Release --no-build --no-restore -o <scratch>/feed -m:2 -nr:false
```

Windowsの.NETで検証しました。Linux、実GPU、実Cosmos、TheoriaCast共同受入、DoomのEmscripten再ビルドは含みません。

## 次の項目と人間の判断

今回実装した項目はI4のみで、git上の状態はレビュー待ちです。review/I4のレビューとdevへの取り込みは人間が行います。
push、PR、main/devへの取り込み、タグ、公開は行いません。
review/I1等との統合後の検証はリリース前に必要です。

元の順序で次の着手可能項目はPF1です。I2は承認待ち、H1aはG2/V1のdev取り込み待ちです。
I1で報告したdefault ResultStepへの直接トレース追加時の例外は、本項目でも変更していません。
I4だけで0.1.4を公開可能とは判定しません。
