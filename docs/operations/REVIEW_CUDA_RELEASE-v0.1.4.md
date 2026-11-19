# CUDA配布・接続・検証の改修記録

検証日: 2026-11-13。ユーザーがPF3より優先した項目2〜4を対象にします。
PF3は調査のみで、実装・ブランチ作成は行っていません。

## ブランチと前提

- Core: dev `dd8b258`起点の`review/CUDA-NUGET`
- Providers: dev `db7e989`起点の`review/CUDA-PROVIDER`
- Tools: dev起点の`review/CUDA-VERIFY`

Coreの`review/CT1`に混在していたCUDA Driver/仮想メモリの7コミットは、
内容を維持して`72e43d3 [CUDA-NUGET] Isolate CUDA Driver and virtual memory foundation from CT1`へまとめました。
元コミット: `837a682`, `27c50c1`, `283567b`, `c5d7b29`, `75fb786`, `d028b4d`, `f504f0e`。
CT1の投票修正`2872279`はこの系列に含めません。元のreview/CT1や他のレビュー待ちブランチ、
dev/mainの参照は変更しません。統合時はCT1全体との二重取り込みを避け、上記分離を確認してください。

## 実装

1. KernelのNuGetにWindows x64 DLLとLinux x64 SO、ネイティブのライセンスを収録します。
   片方でも資産がなければpackは`CUDA_NATIVE_ASSET_MISSING`で失敗します。
   managed buildとpackを分離し、KernelのGeneratePackageOnBuildを無効にしました。
   CIは標準Windows/Ubuntuランナーでネイティブをビルドし、完全なパッケージを作り、
   PackageReferenceのみのC#消費側アプリを両OSで実行する定義です。公開ジョブはありません。
2. Providersの既定値をmodule `aikernel_cuda_driver`、entry `ak_create`、ABI `1`、
   package `AIKernel.Kernel`へ更新しました。論理参照は
   `aikernel-cuda://cuda13.0/modules/aikernel_cuda_driver`です。
   .NET/Pythonマニフェスト、Pythonカタログ、既定値テストを同期しました。
   旧設定を明示する既存.NETテストの期待値は維持し、既定値用の検証を追加しています。
3. Toolsに`aik gpu verify-native --provider cuda-driver`を追加しました。
   パッケージのPE/ELF・x64・欠落・重複・Torch資産を検査し、ライブラリ指定時は
   全21 export、ABI版、ホスト演算結果、不正引数の拒否を検証します。
   GPU検証ではないことを出力し、LibTorchの環境変数を参照しません。

Providersは記述子の公開までで、未接続の実行は引き続き失敗します。
旧操作カタログの全操作が新C ABIに実装されたと扱わず、generic JSON dispatcherを追加していません。
公開済みNET契約の削除・名称変更、Canon/ハッシュ形式の変更もありません。

## 検証

9リポジトリすべてでrestore/buildが成功しました。Windowsでの.NETテストは合計1,132件成功、
Doomの既知の2件のみ失敗し、新規の失敗はありません。

| リポジトリ | 成功 | 失敗 |
| --- | ---: | ---: |
| NET | 173 | 0 |
| Core | 460 | 0 |
| Providers | 123 | 0 |
| Cuda13.0 | 21 | 0 |
| Control | 42 | 0 |
| Tools | 45 | 0 |
| Wasm | 64 | 0 |
| Demo | 71 | 0 |
| Doom | 133 | 2（既知） |

既知の失敗は`DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`と
`DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`です。どちらも
`const HUD_UNIFORM_FLOAT_COUNT = 32`を探す既存のAssert.Containsで失敗し、
前回PF1検証のテスト名・成否・エラーメッセージとの一致を確認しました。期待値は変更していません。

- 新規.NETテストはProvidersの既定値・互換設定・マニフェスト整合の3件と、Toolsのパッケージ・
  CLI検証の11件です。Coreの460件には、分離した既存CUDA/仮想メモリ実装の21件を含みます。
- ProvidersのPython契約テスト13件が成功しました。隔離環境に今回の.NET DLLを配置し、
  CudaComputeProvider.dllがビルド成果物と一致することを確認しています。
- 実ネイティブをWindows x64（MSVC/CMake）とLinux x64（WSL/gcc）でビルドし、
  ABI版・ホストsoftmax・不正引数拒否を両方で実行しました。動的依存にはTorchを含まず、
  WindowsはKERNEL32、Linuxはlibc/libmのみでした。CUDAデバイスやコンテキストは作成していません。
- 実DLLと実NuGetに対するCLI検証が成功しました。ネイティブfixtureの6ケースでも、正常時は成功し、
  ABI版違い・export不足・演算誤り・NaN・不正引数の誤受理をそれぞれ拒否しました。
- NuGet内の両RIDのPE/ELF、x64、ライセンス、Torch資産の非同梱を検査しました。
  Windows/Linux資産の片方を欠かした2ケースではpackが失敗し、不完全なnupkgを生成しませんでした。
- PackageReferenceだけのC#消費側で、RID未指定の通常buildとwin-x64のframework-dependent
  publishを実行し、DLLの手動コピーなしでsoftmaxと行列ベクトル積が成功しました。
- 生成元・nupkg・隔離NuGetキャッシュ・今回ビルドしたソリューションの出力をSHA-256で照合しました。
  Common 37、Core 33、Hosting 7、Kernel 9、CudaComputeProvider 3の各DLL出力が一致しています。
  NuGet内のネイティブもビルドした実DLL/SOと一致しています。

機械可読の集計は証跡ディレクトリの`verified-results.json`、.NET結果は`results/*.trx`、
Python結果は`python-results.xml`にあります。CI定義の構文は確認済みですが、GitHubでの実行結果ではありません。

証跡は`D:\AIKernel\.codex_tmp\review-cuda-release-20261113`です。
Core/Providers/Toolsは上記レビュー系列、他6リポジトリはdevのgit archiveによる隔離コピーです。
検証専用フィードとキャッシュを使い、公開済み0.1.3パッケージやグローバルキャッシュは置き換えません。
ローカルの検証用パッケージ名は現行設定どおり0.1.3ですが、公開・本番導入用ではありません。

## 互換性と残事項

- Toolsの旧`cuda13`検証とNETの`CudaLibTorchPath*`キーは互換用途で残します。
  新しいDriver経路はそれらを必要としません。旧API削除・廃止は別の契約判断です。
- AIKernel.Cuda13.0の廃止/非推奨化、未コミットのAIKernel.LibTorchの削除は行っていません。
- 全リポジトリの0.1.4への版更新、リリースノート、G1/G2等の統合、その他の▲項目の採否は未実施です。
  過去文書の全LibTorch記述を消す変更も今回の範囲に含めていません。
- Linuxネイティブはローカルでビルド・ホスト実行しましたが、Linux .NET消費側とGitHub CIは
  ローカルでは実行していません。GPU実機、Cosmos、TheoriaCast、配信・取引の操作は行っていません。
- 新Kernelと新Coreの仮想メモリ契約は同じ候補パッケージ群として消費する必要があります。
  CIの消費側はpackage source mappingでCoreファミリーを生成済みフィードへ固定します。
- push/PR/公開/タグ作成/dev取り込みは行いません。リリース可能の判定はこれらの統合後です。

利用手順: [CUDA Driver NuGet](../development/cuda-driver-nuget.md)。
今回のレビュー後、保留したPF3へ戻るか、0.1.4の統合・残項目の範囲を決定できます。
