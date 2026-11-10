# V1 hash regression vectors

The literal expectations in
[`HashRegressionVectorTests`](../../tests/AIKernel.Core.Tests/HashRegressionVectorTests.cs)
record the v0.1.3 implementation at Core `dd8b258eba1d3380bfaff89c95697bfd9ad3c9f6`.
No production hash implementation or public contract is changed by V1.

## Coverage

| Algorithm | Fixed inputs |
|---|---|
| `ResultStepIdentity.Create` | Root success, root failure, and success/failure with an explicit parent, kind, origin, semantic slot, and metadata |
| `ResultStepIdentity.CreateReplayLogHash` | Empty log, one success, one failure, and a two-entry success/failure chain |
| `DslRomHasher.ComputeHash` | Empty text, LF JSON, Unicode LF JSON, and explicitly CRLF JSON |
| `Sha256ChatTurnSemanticHasher.ComputeHash` | Empty input, Unicode genesis turn, the same turn with a previous hash, and LF text with a previous hash |

The internal result identity methods are exercised through `ResultStep.Success`,
`Fail`, `WithSemanticDelta`, and `Bind`. Replay assertions also fix the entry count.
Metadata includes mixed-case keys, a non-ASCII key, an empty value, embedded `=`
and LF, Japanese text, an emoji, and both composed and decomposed accented text.
All content line endings are explicit C# escapes, so checking out the test file
with CRLF does not change its input bytes. Expected values are literals; tests
never derive their own expected hashes using the implementation under test.

## Linux reference and current Windows behavior

The reference values were captured by executing these inputs against the unchanged
production code on Linux with .NET SDK 10.0.300. Windows values were captured with
.NET SDK 10.0.401. Both runs used clean `dev` source plus this new test file.

`ResultStepIdentity` currently uses `AppendLine`, so its payload separators are LF
on Linux and CRLF on Windows. The `linuxLf` arguments are the immutable reference
for H1a. The separately named `windowsCrLf` arguments record the existing Windows
behavior until H1a explicitly fixes the separators. All 16 cases run on both
systems; there is no platform-based test skip. DSL and chat hash expectations are
identical on both systems, including the explicit CRLF input case.

H1a must retain the Linux literals and make them mandatory on Windows too. Do not
silently regenerate vectors when an algorithm changes. Other intentional hash
format changes require the approval and versioning defined in the review plan.

## Reproduction

From the repository root on Linux or Windows:

```text
dotnet test tests/AIKernel.Core.Tests/AIKernel.Core.Tests.csproj -c Release --filter FullyQualifiedName~HashRegressionVectorTests
dotnet build AIKernel.Core.slnx -c Release
dotnet test AIKernel.Core.slnx -c Release --no-build
```

V1 is based on `dev`; the separate `review/G1` and `review/G2` branches are not
prerequisites in this item. Their CI and SDK configuration will apply when merged
by the reviewer.

## Validation (2026-11-11)

The complete Core solution built with zero warnings/errors and passed all 455 tests
(449 unit, including the 16 new vectors, plus 6 integration) on both Windows
SDK 10.0.401 and Linux SDK 10.0.300. No tests were skipped.

Linux validation used the cached `mcr.microsoft.com/dotnet/sdk:10.0` image
(`c07906393326`), the clean `dev` source archive, and the same final test file as
Windows. NuGet packages were restored from a temporary local feed populated from
the existing package cache. Build and test ran on the container's internal disk.

Downstream validation used clean local `dev` archives on Windows SDK 10.0.401
with each repository's declared package dependencies. Existing work in their
checkouts was preserved. V1 changes tests and documentation only, so there is no
new production package to substitute into those downstream builds.

| Repository | Source | Build | Passed | Failed |
|---|---|---|---:|---:|
| Core | `dd8b258` + V1 | Pass | 455 | 0 |
| Providers | `db7e989` | Pass | 120 | 0 |
| Cuda13.0 | `9e3ef82` | Pass | 21 | 0 |
| Control | `2229477` | Pass | 42 | 0 |
| Tools | `05ec021` | Pass | 34 | 0 |
| Wasm | `2c6b11f` | Pass | 64 | 0 |
| Demo | `b3fcb62` | Pass | 71 | 0 |
| Doom | `3df7008` | Pass | 133 | 2 |

Windows total: 940 passed, zero skipped, and only these two S0 baseline failures
in `DoomWebGuiTests` (the assertions still expect 32 float elements in the HUD
uniform while the implementation uses 36):

- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`
- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`

No new failure was introduced. These existing failures and their assertions were
preserved under the S0 baseline exception.
