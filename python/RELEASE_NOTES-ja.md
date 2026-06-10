# AIKernel.Python リリースノート

[English](RELEASE_NOTES.md)

## 0.1.1 — Core release alignment

- Python binding metadata を AIKernel.Core 0.1.1 release line に昇格しました。
- Managed assembly discovery を AIKernel.NET contract packages と AIKernel.Core packages の 0.1.1 に揃えました。
- standard provider、provider manifest、monadic result、managed assembly discovery の公開面を 0.1.1 package registration flow 向けに安定化しました。

- Core 標準 Provider の Python descriptor を追加しました:
  `MinimalRuntimeProvider`, `LocalExecutionProvider`, `VfsProvider`,
  `SkillProvider`, `SystemInfoProvider`。
- C# 側の provider execution logic を再実装せず、standard provider / capability
  lookup helper を公開しました。
- Managed assembly discovery を AIKernel.Core 0.1.1 package family に合わせました。
- `SkillProvider` の provider-level managed invoker metadata を追加し、Python
  descriptor を C# の dynamic skill invocation surface と整合させました。
- Python の ROM storage / VFS Git contract operation order を C# の Core-owned
  capability contract と整合させました。
- Python の provider manifest capability order を保持し、Core dynamic manifest
  loading semantics と整合させました。
- Core 標準 Provider、Skill、ROM storage、VFS Git descriptor の C# record
  parameter に bilingual documentation を追加しました。
- C# library member と Python public helper の必須 public surface comment
  coverage を完了しました。
- CLI / provider manifest loading のための Core-owned dynamic provider registry
  surface を追加しました。
- Provider manifest と、Core-owned ROM storage / VFS Git capability contract 用の
  Python descriptor を追加しました。
- Local execution pipeline JSON parsing を monadic `Result` / LINQ composition に
  切り替えました。
- DSL compiler validation と DSL ROM snapshot creation を monadic `Result` / LINQ
  composition に切り替えました。
- DSL ROM capability invocation の解決/実行境界を monadic `Result` / LINQ
  composition に切り替え、ROM replay metadata の付与は維持しました。
- DSL document の node-array parsing と DSL ROM resolution を monadic `Result` /
  LINQ composition に切り替えました。
- compile 済み DSL capability execution の値検証を monadic `Result` / LINQ
  composition に切り替え、逐次 node execution を短絡 aggregate に整理しました。
- DSL ROM store の save/load I/O orchestration を async monadic
  `Task<Result<T>>` / LINQ composition に切り替えました。
- DSL ROM registration と metadata canonical identity validation を monadic
  `Result` / LINQ composition に切り替え、辞書登録は明示的な副作用境界として維持しました。
- DSL argument parsing と compiler capability-argument validation を短絡
  monadic `Result` aggregate に切り替えました。
- DSL ROM capability snapshot validation を monadic `Result` / LINQ composition
  に切り替え、failure 時の ROM metadata 付与は維持しました。
- SkillProvider の descriptor lookup、operation validation、contract metadata
  read を `Option` / `Either` の純粋分岐に切り替えました。
- Core-owned ROM storage / VFS Git contract metadata と SystemInfo provider
  metadata extraction を `Option` ベースの存在確認に切り替えました。
- VFS invocation の provider/path/credential selection と static model prompt
  capability resolution を `Option` / `Either` 分岐に切り替えました。
- Provider manifest endpoint extraction、candidate ROM metadata、ROM frontmatter
  の optional/required field read を `Option` / `Either` 分岐に切り替えました。
- DSL ROM registry の presence check を既存の `Option` snapshot lookup helper
  経由に揃えました。
- VFS credential parameter の presence check を `Option` helper 経由に揃えました。
- Core standard invoker を DI と dynamic provider registry の両方へ登録し、
  CLI / provider loading から組み込み invocation surface を列挙できるようにしました。
- provider と invoker の両方を含む dynamic provider registry snapshot constructor
  の直接テストを追加しました。
- LocalExecutionProvider の DSL node / argument parsing を Core DSL parser と
  同じ短絡 monadic `Result` aggregate に揃えました。
- LocalExecutionProvider の parse / compile / execute を `Task<Result<T>>`
  LINQ として合成し、compiler/runtime 例外を fail-closed invocation result
  に閉じ込めるようにしました。
- 例外境界を持たない pipeline / Skill.MD の純粋分岐を `Either<string,T>` に
  置き換えました。Skill header selection、fallback steps、slug normalization、
  local JSON value selection、ROM metadata equality check が対象です。
- local pipeline の存在チェックを `Option<string>` に置き換え、JSON parse、
  DSL compile、DSL execute の例外境界を `Try.Run` / `Try.RunAsync` で
  捕捉してから structured result に戻すようにしました。
- ResultStep の loop trace branching を monadic primitives に寄せました。
  optional loop timestamp と replay-log parent は `Option<T>`、loop transition
  decision は `Either<string,T>` を使います。
- DSL document parser の property presence check を `Option<JsonElement>`、
  property validation / value selection を `Either<string,T>` に寄せました。
- LocalExecutionProvider の inline DSL parser も Core parser と同じく、
  property presence check を `Option<JsonElement>`、純粋な property validation
  を `Either<string,T>` に寄せました。
- compiler の純粋 validation、ROM registry の presence check、ROM store の
  I/O 例外捕捉、ROM path parsing を更新後のモナド分担へ寄せました。
  純粋 validation は `Either`、辞書 presence は `Option`、例外から `Result`
  への変換は `Try` を使います。

## 0.1.0 — Stable Python binding baseline

- 公式 PyPI package `aikernel-net` を `0.1.0` へ昇格しました。
- import package name は `aikernel_net` のままです。
- 無関係な PyPI `aikernel` project との混乱を避けるため、旧 in-repository
  `aikernel` import package を削除しました。
- Managed assembly discovery を AIKernel.Core 0.1.0 package family と揃えました。

Install:

```bash
pip install aikernel-net==0.1.0
```

Import:

```python
import aikernel_net
```

PyPI の `aikernel` は別プロジェクトです。AIKernel.NET は project identity を守り、
user confusion を避けるために `aikernel-net` を使います。

Package は既定で CPU-only であり、CUDA、LibTorch、native ABI payload は含みません。
GPU / native execution は外部 Capability concern です。
