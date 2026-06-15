# AIKernel.NET Core

![AIKernel.NET Logo](assets/aikernel-logo.png)

**AIKernel.NET Core** is a runtime designed to solve three core problems in LLM applications: **context drift, lack of reproducibility, and lack of governance**.

It is the implementation of a deterministic and immutable **Knowledge OS** designed for the .NET ecosystem.

It is not merely an LLM wrapper.

AIKernel.NET Core provides a robust execution foundation for managing knowledge assets (**ROM**), governing context (**Context**), and treating inference execution as a controlled transaction.

---

## AIOS SDK Role

AIKernel.Core is the kernel runtime layer of the AIOS SDK. It supplies the
deterministic runtime, monads, DSL, VFS/ROM, hosting, and standard Core
providers that other layers combine into an AI Operating System distribution.

AIKernel also provides an official AIOS distribution, codenamed
**AIKernel.Monolith**. Monolith has begun development as the standard AIOS that
integrates semantic runtime, capability graph, governance, providers, WASM,
GPU backends, and tools after the 0.1.x SDK line stabilizes.

---

## Concept Elevation

AIKernel.Core follows the common Concept Elevation naming policy maintained in
AIKernel.NET. Core adds only concept-level facades and does not rename CTG
contracts, GateInput, DTOs, enums, providers, mappers, adapters, or serializers.

Repository notes: [docs/development/concept-elevation.md](docs/development/concept-elevation.md)

---

## Architectural Discipline

AIKernel.NET Core operates according to the following three canonical principles.

### 1. Fail-Closed

If a signature mismatch, governance rejection, or token budget violation occurs, AIKernel does not allow incomplete success.

Execution stops immediately.

### 2. Deterministic Replay

Every inference context is fixed by `ContextHash` and `PromptHash`.

Given the same input and execution environment, AIKernel aims to reproduce the same execution context.

> The goal is not to make the LLM itself fully deterministic.  
> The goal is to make the execution process around the LLM reproducible, inspectable, and governable.

### 3. Immutability

Loaded knowledge assets (**ROM**) and generated execution results (`IExecutionResult`) are treated as immutable DTOs while flowing through the system.

Once materialized, objects are not modified in place.

---

## Runtime Architecture

AIKernel.NET Core is designed as an OS-like layered runtime.

```text
+-----------------------------+
|        Hosting Layer        |  .NET application integration
+-----------------------------+
|         Kernel Layer        |  Governance and orchestration
+-----------------------------+
|          Core Layer         |  Pure logic: VFS / ROM / Context / Execution
+-----------------------------+
|        Provider Layer       |  External model and service boundaries
+-----------------------------+
```

---

## OS Abstractions

Core owns the AIKernel OS syscall surface. Provider implementations must depend
on these contracts instead of defining their own copies.

The OS abstraction surface now includes:

- `AIKernel.Abstractions.Compute`: `IComputeProvider`, `ComputeBuffer`,
  `ComputeKernel`
- `AIKernel.Abstractions.Processes`: `ProcessId`, `ProcessState`, `IProcess`,
  `IProcessHost`, `IProcessSupervisorProvider`
- `AIKernel.Abstractions.Network`: `INetworkProvider`, `INetworkStream`,
  `IHttpResponse`
- `AIKernel.Abstractions.Logging`: `ILoggingProvider`, `LogLevel`
- `AIKernel.Abstractions.Routing`: `ISemanticRouter`, `RouteResult`
- `AIKernel.Vfs`: `IFileSystemProvider` as an alias over `IVfsProvider`
- `AIKernel.Core.Control`: `IBonsaiRule`, `IBonsaiEngine`, `BonsaiEngine`,
  `RuleEvaluator`

The Control layer can consume OS events such as `ProcessStarted`,
`ProcessStopped`, `ProcessCrashed`, `GpuKernelExecuted`, `FileAccessed`, and
`NetworkRequest` through the shared EventBus abstraction. Providers and WASM
runtimes publish those events; Core keeps the rule engine and routing
contracts.

This keeps Core as the contract and deterministic runtime layer, Providers as
the host OS driver layer, and AIKernel.Wasm as the browser/WASM runtime layer.

---

## CTG Governance Core

AIKernel.Core 0.1.1.1 includes the implementation-side Canonical Triadic
Governance (CTG) kernel surface. CTG is implemented as deterministic Core
services that consume the AIKernel.NET contract DTOs and enums without changing
ROM, Canon, Council, Gate, RejectPolicy, YAML, or DTO semantics.

The Core CTG surface includes:

- vote extraction and `CouncilDecision` to `GateInput` adaptation
- pure Decision Gate evaluation
- pure Trajectory Gate aggregation
- reject reason serialization and classification
- canon reference resolution and locale YAML adaptation
- step / trajectory governance trace assembly
- `ICtgGovernanceService` as the Core-facing integration facade

Register CTG services directly with:

```csharp
services.AddCtgGovernance();
```

`AddAIKernelCore()` also registers the CTG surface. Registrations are
non-replacing for existing `IDecisionGate` and `ITrajectoryGate` services.

Read the [CTG Governance Integration Guide](docs/development/ctg-governance-integration.md)
for implementation boundaries.

Distribution note: the 0.1.1.1 update is NuGet-only. No PyPI package is created
for this update line.

---

## Solution Structure

This repository consists of runtime, provider-integration, and verification
layers.

```text
src/
  AIKernel.Common
  AIKernel.Core
  AIKernel.Kernel
  AIKernel.Hosting
  Providers/
    (external provider integration workspace)

tests/
  AIKernel.TestKit
  AIKernel.Core.Tests
  AIKernel.IntegrationTests

python/
  AIKernel.Python
```

### `src/` — Runtime Implementation

#### `AIKernel.Common`

Functional primitives shared by the runtime family.

It contains pure Result / Option / Either helpers and does not depend on
AIKernel runtime DTOs, providers, hosting, or kernel implementations.

#### `AIKernel.Core`

A pure logical engine responsible for the phase transition of knowledge.

```text
VFS → ROM → Context → Execution
```

This layer owns the Core runtime logic and separates implementation concerns from external Hosting and Provider boundaries.

Core also owns the OS-level standard providers that do not call external
services:

- `MinimalRuntimeProvider` exposes `aikernel.runtime.ping` for deterministic
  boot and capability-graph validation.
- `LocalExecutionProvider` exposes `aikernel.local.execute` for inline DSL
  pipeline execution through the existing Core DSL runtime.
- `VfsProvider` exposes `aikernel.vfs` as a read-only VFS capability module for
  file reads, directory listing, existence checks, and metadata summaries.
- `SkillProvider` loads OpenAI-compatible `SKILL.md` files, converts them into
  DSL pipeline descriptors, and registers them as capability modules.
- `SystemInfoProvider` exposes `aikernel.system.info` for safe, read-only
  introspection of providers, capabilities, VFS mount state, and runtime
  versions.

These providers are deterministic, side-effect free at the provider boundary,
and do not depend on AIKernel.Tools or external provider packages.

For Native Capability modules, the OS-independent MemoryRegion / MemoryMapper
runtime surface is owned by AIKernel.Core in the 0.1.0 prototype validation
baseline. Core exposes the Result-based runtime adapter, while the concrete
Win32/POSIX mapping implementations live in Kernel.

#### `AIKernel.Kernel`

The governance and orchestration layer of the OS.

It exposes the `IKernel` Facade and integrates all runtime layers.
It also supplies the default OS-specific `IMemoryMapper` implementation used by
trusted hosts.

#### `AIKernel.Hosting`

The ignition switch for .NET applications.

It provides `IServiceCollection` extensions and default wiring for ASP.NET Core / Generic Host.

#### `Providers/`

Provider integration workspace for packages that connect AIKernel to external
models and services. The 0.1.1.1 Core package line does not publish provider
packages from this repository.

##### `AIKernel.Providers.MicrosoftAI`

An external OpenAI-compatible Provider package based on
`Microsoft.Extensions.AI` (MEAI).

It uses Microsoft’s AI abstraction layer to accelerate development while preserving AIKernel’s Capability-based execution model.

---

### `tests/` — Verification

#### `AIKernel.TestKit`

A Contract Test Framework for verifying ABI and behavioral discipline.

It provides the foundation for validating that downstream implementations comply with AIKernel contracts.

#### `AIKernel.Core.Tests`

Unit tests for internal runtime logic.

#### `AIKernel.IntegrationTests`

Integration tests that pass through multiple runtime layers.

### `python/` — Language Binding

#### `AIKernel.Python`

A thin Python binding for AIKernel.Core functional primitives and managed
assembly discovery. It is released only when a Python package release is
explicitly scheduled.

Previously published Python releases install as the `aikernel-net` package and
are CPU-only by default. The package exposes Python monad helpers and managed
assembly discovery; it does not ship CUDA, LibTorch, or the native
`libtorch_bridge` ABI. The Python package does not reimplement OS-specific
memory mapping, Kernel internals, or Capability internals. The 0.1.1.1 CTG Core
update does not publish a PyPI package.

---

## Quick Start

For a focused package usage walkthrough, see the
[AIKernel.Core User Guide](docs/user-guide/index.md).

### 1. Install Packages

```bash
dotnet add package AIKernel.Core --version 0.1.1.1
dotnet add package AIKernel.Hosting --version 0.1.1.1
dotnet add package AIKernel.Kernel --version 0.1.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1
```

`AIKernel.Providers.MicrosoftAI` remains on the 0.1.1 provider package line
until the provider repository is updated. The 0.1.1.1 Core update is centered
on Core, Hosting, Kernel, Common, TestKit, and the AIKernel.NET contract
packages.

For direct use of functional primitives and contract testing helpers:

```bash
dotnet add package AIKernel.Common --version 0.1.1.1
dotnet add package AIKernel.TestKit --version 0.1.1.1
```

CUDA is optional and lives outside this repository. GPU hosts should install an
external Capability package such as `AIKernel.Cuda13.0.Libtorch2.12.win-x64`
explicitly. CUDA Capability packages may use split distribution: NuGet.org
contains a small metadata package, while the full runtime package with
LibTorch/CUDA/native payloads is attached to the matching Capability GitHub
Release. For CUDA execution, download the full `.nupkg`, add its folder as a
local NuGet source, and install from that source:

```bash
dotnet nuget add source <folder-containing-full-cuda-nupkg> --name AIKernel-CUDA
dotnet add package AIKernel.Cuda13.0.Libtorch2.12.win-x64 --version 0.1.1
```

LLM / SLM developers who need direct CUDA integration should read
[docs/development/cuda-capability-development-guide.md](docs/development/cuda-capability-development-guide.md).
Other CUDA versions, model runtimes, or Linux CUDA hosts should fork the CUDA
Capability repository and publish a separate Capability module.

For the previously published Python language binding:

```bash
pip install aikernel-net
```

The base Python package is a CPU-only universal `py3-none-any` wheel for
Windows and Linux. The 0.1.1.1 CTG Core update does not publish a PyPI package;
existing Python hosts should stay on the latest published `aikernel-net`
distribution until a Python release is scheduled. Import it as `aikernel_net`.
The PyPI package named `aikernel` is a different project. GPU/native runtimes
remain explicit Capability installs.

Stable Python releases are published to PyPI as `aikernel-net` only when a
Python release is explicitly scheduled. Development builds may be used for
CI/CD validation, but user-facing release notes describe only public package
releases and fold development changes into the next public release entry.

For source-based local validation, install from the repository subdirectory:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

The default Python install is CPU-only/CUDA-free and does not include a native
bridge. Install GPU integrations from the matching external Capability package
and follow that Capability repository's distribution instructions.

The v0.1.1.1 Core package family is aligned with the AIKernel.NET contract
packages `AIKernel.Abstractions`, `AIKernel.Dtos`, and `AIKernel.Enums`
v0.1.1.1.
`AIKernel.Vfs` is no longer a separate package dependency; the VFS contracts are
provided by `AIKernel.Abstractions`. The `AIKernel.Vfs` namespace remains as a
Core implementation namespace for in-process VFS providers and stores; it is not
a separate NuGet package.

### 2. Register Core for an API Host

Use the Server/API host to hold model credentials and execute OpenAI-compatible
providers. Keep browser/WASM clients behind your own API boundary; do not place
model API keys in a WebAssembly client.

```csharp
builder.Services
    .AddAIKernelCore(builder.Configuration)
    .WithOpenAI(
        builder.Configuration.GetSection("AIKernel:Providers:OpenAI"),
        (sp, options) =>
        {
            // Return an IChatClient from Microsoft.Extensions.AI.
            // The provider package registers default capabilities and prompt
            // capability metadata for the configured ProviderId and ModelId.
            return CreateChatClient(options);
        });

builder.Services.AddAIKernelKernel();
```

Example configuration:

```json
{
  "AIKernel": {
    "Providers": {
      "OpenAI": {
        "ProviderId": "openai-compatible",
        "ModelId": "gpt-4.1-mini",
        "SecretKeyName": "OpenAI:ApiKey",
        "MaxInputTokens": 8192,
        "MaxOutputTokens": 1024
      }
    }
  },
  "OpenAI": {
    "ApiKey": "<store this in user-secrets, Key Vault, or environment configuration>"
  }
}
```

For browser/WASM-oriented clients, register only browser-safe VFS providers in
the client-side service collection:

```csharp
services.AddAIKernelBrowserVfsProviders();
```

Use `AddAIKernelCoreVfsProviders` only in trusted server or desktop hosts where
local filesystem access is expected.

When a host registers external capability modules or model providers, select the
provider through request metadata using `KernelFacadeMetadataKeys.ProviderId`.
This avoids hard-coded metadata strings across AIKernel.Tools, AIKernel.RH, and
other provider packages.

External provider packages can attach either assembly-referenced providers or
process-backed adapter providers through `WithModelProvider<TProvider>`. The
extension registers the `IModelProvider` implementation and its
`ModelPromptCapability` entries together, so the static resolver can bind the
selected ProviderId and ModelId without provider-specific wiring in Core.

For contract-level external Capability modules, Core registers an in-memory
`ICapabilityModuleRegistry` and a fail-closed `ICapabilityModuleInvoker` by
default. Hosts can register module descriptors for CLI, assembly-referenced,
native, DSL ROM, or remote modules without granting execution by accident.
Actual module execution should be supplied by a trusted Tools, Provider, or
host package that replaces the default invoker.
Core standard providers register their own safe inline capabilities during
initialization. This gives a boot baseline for `runtime.ping`, local DSL
execution, read-only VFS access, SKILL.md registration, and system
introspection before external providers are loaded. Their invokers are also
registered through the dynamic provider registry; `SkillProvider` is tracked as
a provider-level invoker because its capabilities are discovered from
`SKILL.md` files at runtime.
Core also exposes `IDynamicProviderRegistry` as a Core-owned extension over the
stable provider registry contract. Hosts and CLI tools can use it to load
provider manifests, register capability metadata, and optionally load provider
assemblies without adding external provider dependencies to Core.
GPU and Native ABI implementations are external Capability packages. For
example, `AIKernel.Cuda13.0.Libtorch2.12.win-x64` owns its native bridge, runtime version metadata,
and CUDA-specific implementation while conforming to AIKernel Capability
contracts.
For trusted hosts, `AddAIKernelKernel()` registers an OS-specific
`IMemoryMapper` (`Win32MemoryMapper` on Windows, `PosixMemoryMapper` elsewhere)
behind the Core memory abstraction. Native Capability packages consume only the
Core abstraction and never reference Kernel directly.

User-land routing pipelines can return
`AIKernel.Kernel.KernelProviderRoutingDecision` from a `ResultStep`/LINQ chain,
then apply it to a `KernelRequest` and its metadata through `AIKernel.Kernel`
extension helpers.
This supports policies such as low-tier versus high-tier LLM selection, or
routing `aik...` contexts to a CLI-backed capability adapter, while keeping
Kernel execution driven by the same ProviderId / ModelId contract.
Use `KernelProviderRoutingDecisionFactory` for Core-provided construction
guards; the decision carrier stays behavior-free in the Kernel facade package.

AIKernel.Core also includes a standard JSON DSL pipeline runtime for
AI-generated plans. The DSL compiles to deterministic `ResultStep` pipelines,
supports finite `Loop` / `LoopUntil` / `Suspend` nodes, and can be saved as DSL
ROM under `rom/dsl/{namespace}/{name}.json` for later invocation through
`dsl://{namespace}/{name}`. The canonical schema and operational rules are
documented in AIKernel.NET as `docs/architecture/18.DSL_PIPELINE_AND_ROM_SPEC.md`.

Compiled DSL pipelines also support C# LINQ query composition. `Select` and
passing `where` predicates are pure projections and do not append replay nodes;
`SelectMany` executes the next DSL pipeline with the previous output as input
and concatenates the `ResultStep` replay log. Failed `where` predicates are
recorded as deterministic reject nodes.

```csharp
IKernelPipeline observe = compiler.Compile(observeDocument).Value!;
IKernelPipeline decide = compiler.Compile(decideDocument).Value!;

IKernelPipeline agent =
    from first in observe
    where first.Data["last_capability"] == "Observe"
    from second in decide
    select second.With(
        "route",
        $"{first.Data["last_capability"]}->{second.Data["last_capability"]}");

var result = agent.Execute(DslPipelineExecutionContext.Create());
```

Chat histories can also be fixed as immutable HistoryROM assets. Use
`HistoryRomStore.SaveHistoryAsRomAsync` to convert ordered chat records into a
signed Markdown ROM, store it in VFS under `rom/history/{namespace}/{name}.md`,
and register it as `history://{namespace}/{name}`. Loading a HistoryROM uses the
same ROM signature verification path as other Core ROM assets and rejects hash
mismatches or attempts to overwrite an existing history path with different
content.

---

## Target Boot Experience

```text
[KERNEL] Initializing AIKernel.NET Core v0.1.1.1...
[KERNEL] Loading VFS Provider: local... [OK]
[KERNEL] Mounting ROM root... [OK]
[KERNEL] Building ContextSnapshot... [OK]
[KERNEL] Computing ContextHash... [OK]
[KERNEL] Resolving Provider: microsoft-ai.openai-compatible... [OK]
[KERNEL] Executing governed inference... [OK]

> Hello Intelligence.
> The Semantic Context is stable.
> Execution is reproducible. Governance is active.
```

---

## Runtime Flow

In the minimal implementation, AIKernel.NET Core follows the execution path below.

```text
KernelRequest
→ VFS Mount
→ ROM Load
→ Context Build
→ Governance Check
→ Prompt Composition
→ Provider Execution
→ IExecutionResult
```

In the 0.1.0 prototype validation phase, prompt composition may be simplified
and static when a demo or provider host does not yet supply a richer pipeline.

Advanced Governance, signature enforcement, Semantic Cache, and Deterministic
Replay are validated incrementally through the prototype repositories before
being promoted into stricter runtime defaults.

---

## Development Roadmap

### v0.0.0 — Initial Repository Setup

This phase sets up the initial AIKernel.Core repository.

Based on the Contracts / DTO / Enum packages established in AIKernel.NET, this repository prepares the basic structure for the Core implementation.

- Create the repository structure
- Set up basic project templates
- Prepare the initial `src/` and `tests/` layout
- Define the project skeletons for Core / Kernel / Hosting / Provider / Tests
- Organize dependencies on AIKernel.NET contract packages

The purpose of this phase is not to complete an executable Kernel.

The goal is to prepare a foundation that allows future Core implementation to proceed without ambiguity.

---

### v0.0.x — Completed Design-Implementation Phase

The v0.0.x phase implemented Core runtime components incrementally toward
v0.1.0.

Based on the Canonical Contracts fixed in v0.0.1, minor refinements may be made to naming, API boundaries, and test structure where needed for implementation consistency.

- Incremental implementation of Core runtime components
- Foundation for VFS / ROM / Context / Provider integration
- Minimal implementation for mounting local files as VFS
- Loading ROM files and transforming them into Context
- Minimal execution path using static Prompt Composition
- Initial implementation of the MicrosoftAI Provider wrapper
- Initial Hosting / DI wiring
- Unit Test / Integration Test skeletons
- API, naming, and contract-boundary refinements toward v0.1.0

The purpose of this phase was not to fully implement Governance or
Deterministic Replay.

The first goal was to confirm that AIKernel.Core can provide the following
minimal execution path.

`VFS → ROM → Context → Static Prompt → Provider → IExecutionResult`

---

### v0.1.0 — Prototype Validation: First Executable Runtime

v0.1.0 is the first executable runtime and prototype validation release of
AIKernel.Core, scheduled for publication on 2026-06-09.

It integrates the Canonical Contracts established in AIKernel.NET v0.0.1 into an executable form through Core implementation, Provider integration, and tests.

This is the Core-side realization of **Synthesis: Executable Contracts** defined in Issue #6.

- Minimal Core runtime
- VFS-based ROM loading
- ContextSnapshot foundation
- MicrosoftAI Provider wrapper
- Initial Hosting integration
- First executable path from ROM to inference
- Basic generation of `IExecutionResult`
- Output of `ContextHash`
- Connection to Contract Test skeletons

The purpose of this release is not to implement the entire AIKernel.NET philosophy all at once.

The purpose is to prove, with prototype applications and external Capability
modules, that AIKernel can **load knowledge, construct context, execute
inference through a Provider, route Capabilities, and preserve replay evidence**.

---

## Roadmap Note

AIKernel.Core development proceeds based on the Canonical Contracts defined by AIKernel.NET.

AIKernel.NET defines the contracts.  
AIKernel.Core proves them through implementation.

The v0.0.x phase is complete. The 0.1.0 line validates the implementation
through prototype repositories before the package family is promoted toward a
broader stable release line.

Release notes:

- [English](RELEASE_NOTES.md)
- [日本語](RELEASE_NOTES-ja.md)

This roadmap preserves the progression established by the existing AIKernel.NET release notes and Issue #6:

**Init → Fix → Synthesis**

It concretizes that progression on the Core implementation repository side.

---

## Repository Relationship

AIKernel.NET Core depends on the canonical contract packages defined in the main AIKernel.NET contract repository.

```text
AIKernel.NET
= contracts, DTOs, enums, documentation, contract-test skeletons

        ↓

AIKernel.Core
= concrete runtime implementation and standard providers

        ↓

AIKernel.Providers.*
= external model and service integrations
```

AIKernel.NET defines the contracts.  
AIKernel.Core proves them through implementation.

---

## Contributor Guidelines

Core changes must follow the shared AIKernel development discipline:

- [AIKernel Development Guidelines](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES.md)
- [AIKernel 開発ガイドライン](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES-jp.md)

These guidelines define the required monadic LINQ style, fail-closed behavior,
DRY/DGA rules, bilingual public documentation comments, tests, and release
checks for package code.

---

## License

Apache License 2.0.
See the `LICENSE` file for details.
