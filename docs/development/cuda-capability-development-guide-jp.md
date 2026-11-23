# CUDA Capability 開発ガイド

Windows x64 と Linux x64 では、`AIKernel.Kernel` 0.1.4 同梱の CUDA Driver を使います。実行時に必要なネイティブ依存は NVIDIA ドライバのみです。

`AIKernel.Cuda13.0` は 0.1.3 までの旧 LibTorch 参照実装で、保守は終了しました。

[CUDA Driver 移行ガイド](https://github.com/AIKernel-NET/AIKernel.Providers/blob/dev/docs/CUDA-DRIVER-MIGRATION.md)を参照してください。

## レイヤ構造

```text
.NET ホスト
  -> AIKernel.Kernel のメモリ・推論 API
  -> aikernel_cuda_driver ak_* C ABI
  -> NVIDIA ドライバ
```

## 仮想メモリレイヤー

Core はメモリマッピングの契約を提供します。

- `IMemoryMapper.Open(path, accessMode) -> Result<IMemoryRegion>`
- `IMemoryRegion.Pointer`、`IMemoryRegion.Length`、`IMemoryRegion.Info`
- `IMemoryRegion.Unmap() -> Result<bool>`

Kernel は Windows 用の `Win32MemoryMapper`／`Win32MemoryRegion` と POSIX 用の
`PosixMemoryMapper`／`PosixMemoryRegion` を提供します。信頼済みホストでは
`AddAIKernelKernel()` で既定のマッパーを登録できます。

## .NET から使う

```bash
dotnet add package AIKernel.Kernel --version 0.1.4
```

`CudaVirtualMemoryDevice` は `ak_create` で CUDA コンテキストを作成し、`Upload` と `Download` で VRAM と転送します。確保したブロックはデバイスより先に解放します。

Python でネイティブ ABI を利用する場合は、[CUDA Driver の wheel](../../native/cuda/README.md)を参照してください。
