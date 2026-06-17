# User Guide

[日本語](index-ja.md)

This guide explains how to consume AIKernel.Core from .NET and Python hosts.

Core is the kernel runtime layer of the AIOS SDK. It provides the deterministic
runtime, VFS/ROM, hosting, Kernel, and standard Core capabilities that an AIOS
distribution builds on before adding providers, control, WASM, GPU, or tools
layers.

AIKernel.Monolith is the official AIOS distribution now in development. It will
serve as the standard reference distribution that integrates the SDK layers
after the 0.1.x line stabilizes.

## Install Packages

```bash
dotnet add package AIKernel.Common --version 0.1.2
dotnet add package AIKernel.Core --version 0.1.2
dotnet add package AIKernel.Hosting --version 0.1.2
dotnet add package AIKernel.Kernel --version 0.1.2
```

During local integration, use `0.1.2-dev{buildNumber}` NuGet packages instead
of stable `0.1.2` packages until the release task opens publication.

Python package:

```bash
pip install aikernel-net
```

During local Python validation, use `0.1.2.dev{buildNumber}` wheels instead of
stable `0.1.2` wheels. The package exposes the generated managed API catalog and
the bundled CTG-ROM sample asset helpers.

## Register Core Services

```csharp
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAIKernelCore();

using var provider = services.BuildServiceProvider();
```

Core registration includes the standard provider baseline:

- MinimalRuntimeProvider
- LocalExecutionProvider
- VfsProvider
- SkillProvider
- SystemInfoProvider

Core registration also includes the CTG governance integration surface.
Use `AddCtgGovernance()` directly when a host needs only CTG services:

```csharp
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCtgGovernance();
```

`AddCtgGovernance()` registers CTG evaluators and adapters without replacing an
already registered `IDecisionGate` or `ITrajectoryGate`.

## Use CTG Governance

Core provides the deterministic CTG kernel surface:

- `CtgCouncilVoteExtractor`
- `CtgCouncilDecisionToGateInputAdapter`
- `CtgDecisionGateEvaluator`
- `CtgTrajectoryGateEvaluator`
- `CtgRejectReasonClassifier`
- `CtgGovernanceTraceBuilder`
- `CtgStepTraceAssembler`
- `CtgRomLocaleYamlAdapter`
- `ICtgGovernanceService`

Decision Gate evaluation reads only `GateInput.Logos`, `GateInput.Ethos`, and
`GateInput.Pathos`. Continuous carriers such as confidence, risk score,
diagnostics, and metadata scores are trace material, not gate inputs.

```csharp
using AIKernel.Core.Governance;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using Microsoft.Extensions.DependencyInjection;

var ctg = provider.GetRequiredService<ICtgGovernanceService>();

var decision = await ctg.EvaluateDecisionGateAsync(
    new GateInput
    {
        Logos = CouncilVoteValue.Approve,
        Ethos = CouncilVoteValue.Approve,
        Pathos = CouncilVoteValue.Abstain
    },
    cancellationToken);
```

## Use Standard Capabilities

Standard capabilities are available before external providers are loaded:

- `aikernel.runtime.ping`
- `aikernel.local.execute`
- `aikernel.vfs`
- `aikernel.system.info`
- dynamic `skill.*` capabilities from `SKILL.md`

Hosts can inspect descriptors through the capability registry and invoke them
through registered invokers.

## Use VFS

Core provides local, memory, and web-get VFS implementations. The standard VFS
capability is read-only and supports:

- `vfs.exists`
- `vfs.list`
- `vfs.metadata`
- `vfs.read_file`

Use it for deterministic inspection, not for mutating host files.

## Use SKILL.md

`SkillProvider` recursively discovers `SKILL.md` files and registers each skill
as a capability module. The preferred public spelling is `SKILL.md`.

Skills are parsed into structured manifests and can be compiled into DSL
pipeline descriptors for local execution.

## Use Monad Primitives

`AIKernel.Common` contains fail-closed functional primitives:

- `Result<T>` for execution outcomes
- `Either<L,R>` for pure two-way validation
- `Option<T>` for presence/absence
- `Try` / `TryAsync` for exception capture
- `Async<T>` for LINQ-composable asynchronous result pipelines
- `ResultStep<TState,T>` for replayable pipeline steps

Use these primitives to keep parsing, validation, execution, and replay
semantics explicit.

Core code should keep the phase boundary visible:

- parsing and validation use `Either<L,R>` when no exception-producing API is involved
- optional metadata, optional providers, and lookup misses use `Option<T>`
- file, network, provider, DSL, and replay execution use `Result<T>` or `Try.RunAsync`
- replayable pipeline execution uses `ResultStep<TState,T>`

Public compatibility adapters may still expose existing exception-throwing
members, but their internal implementation should delegate to the fail-closed
monadic path first.

## Use Python Wrapper

```python
from aikernel_net import standard_provider_contracts, standard_capability

for provider in standard_provider_contracts():
    print(provider.provider_id, provider.name)

vfs = standard_capability("aikernel.vfs")
print(vfs.operations)
```

The Python package exposes contract descriptors and managed assembly loading
helpers. It does not replace the C# runtime.

## Failure Behavior

Core is designed to fail closed:

- invalid ROM / DSL / Skill metadata returns explicit errors
- capability lookup fails explicitly for unknown IDs
- VFS credential and path validation are bounded
- external providers are loaded through manifest boundaries

## Next Steps

- Read the CUDA guide before adding native compute packages.
- Read the release checklist before publishing patch packages.
- Use AIKernel.Providers for external provider implementations.
- Use AIKernel.Tools for operator commands and diagnostics.
