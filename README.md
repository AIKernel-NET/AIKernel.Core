# AIKernel.NET Core

![AIKernel.NET Logo](assets/aikernel-logo.png)

**AIKernel.NET Core** is a runtime designed to solve three core problems in LLM applications: **context drift, lack of reproducibility, and lack of governance**.

It is the implementation of a deterministic and immutable **Knowledge OS** designed for the .NET ecosystem.

It is not merely an LLM wrapper.

AIKernel.NET Core provides a robust execution foundation for managing knowledge assets (**ROM**), governing context (**Context**), and treating inference execution as a controlled transaction.

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

## Solution Structure

This repository consists of runtime, provider, and verification layers.

```text
src/
  AIKernel.Common
  AIKernel.Core
  AIKernel.Cuda.Libtorch.2.12-cuda13.0
  AIKernel.Kernel
  AIKernel.Hosting
  Providers/
    AIKernel.Providers.MicrosoftAI

tests/
  AIKernel.TestKit
  AIKernel.Core.Tests
  AIKernel.IntegrationTests
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

#### `AIKernel.Kernel`

The governance and orchestration layer of the OS.

It exposes the `IKernel` Facade and integrates all runtime layers.

#### `AIKernel.Hosting`

The ignition switch for .NET applications.

It provides `IServiceCollection` extensions and default wiring for ASP.NET Core / Generic Host.

#### `Providers/`

Provider implementations that connect AIKernel to external models and external services.

##### `AIKernel.Cuda.Libtorch.2.12-cuda13.0`

A Windows/MSVC Native ABI reference Capability module for LibTorch 2.12.0 with
CUDA 13.0. It keeps CUDA and LibTorch runtime files outside the Core runtime and
outside the NuGet payload.

##### `AIKernel.Providers.MicrosoftAI`

An OpenAI-compatible Provider implementation based on `Microsoft.Extensions.AI` (MEAI).

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

---

## Quick Start

### 1. Install Packages

```bash
dotnet add package AIKernel.Core --version 0.0.5
dotnet add package AIKernel.Hosting --version 0.0.5
dotnet add package AIKernel.Kernel --version 0.0.5
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.0.5
```

For direct use of functional primitives and contract testing helpers:

```bash
dotnet add package AIKernel.Common --version 0.0.5
dotnet add package AIKernel.TestKit --version 0.0.5
```

For the optional LibTorch 2.12.0 / CUDA 13.0 Native ABI reference capability:

```bash
dotnet add package AIKernel.Cuda.Libtorch.2.12-cuda13.0 --version 0.0.5
```

The v0.0.5 package family is aligned with the AIKernel.NET contract packages
`AIKernel.Abstractions`, `AIKernel.Dtos`, and `AIKernel.Enums` v0.0.5.
`AIKernel.Vfs` is no longer a separate package dependency; the VFS contracts are
provided by `AIKernel.Abstractions`.

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
`AIKernel.Cuda.Libtorch.2.12-cuda13.0` is the first Native ABI reference module:
it wraps LibTorch through a small C API bridge and intentionally keeps all CUDA
and LibTorch runtime files outside AIKernel.Core and outside the NuGet payload.

User-land routing pipelines can return a `KernelProviderRoutingDecision` from a
`ResultStep`/LINQ chain, then apply it to a `KernelRequest` and its metadata.
This supports policies such as low-tier versus high-tier LLM selection, or
routing `aik...` contexts to a CLI-backed capability adapter, while keeping
Kernel execution driven by the same ProviderId / ModelId contract.

AIKernel.Core also includes a standard JSON DSL pipeline runtime for
AI-generated plans. The DSL compiles to deterministic `ResultStep` pipelines,
supports finite `Loop` / `LoopUntil` / `Suspend` nodes, and can be saved as DSL
ROM under `rom/dsl/{namespace}/{name}.json` for later invocation through
`dsl://{namespace}/{name}`. The canonical schema and operational rules are
documented in AIKernel.NET as `docs/architecture/18.DSL_PIPELINE_AND_ROM_SPEC.md`.

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
[KERNEL] Initializing AIKernel.NET Core v0.1.0...
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

In the initial implementation phase, prompt composition may be simplified and static.

Advanced Governance, signature enforcement, Semantic Cache, and Deterministic Replay will be expanded incrementally.

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

### v0.0.x — Development of Core Runtime Components

During the v0.0.x phase, Core runtime components will be implemented incrementally toward v0.1.0.

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

The purpose of this phase is not to fully implement Governance or Deterministic Replay.

The first goal is to confirm that AIKernel.Core can provide the following minimal execution path.

`VFS → ROM → Context → Static Prompt → Provider → IExecutionResult`

---

### v0.1.0 — Synthesis: First Executable Runtime

v0.1.0 is the first executable runtime release of AIKernel.Core.

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

The purpose is to prove, with a minimal configuration, that AIKernel can **load knowledge, construct context, and execute inference through a Provider**.

---

## Roadmap Note

AIKernel.Core development proceeds based on the Canonical Contracts defined by AIKernel.NET.

AIKernel.NET defines the contracts.  
AIKernel.Core proves them through implementation.

During the v0.0.x phase, Core runtime components will be implemented incrementally and integrated as the first executable runtime in v0.1.0.

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

## License

MIT License.  
See the `LICENSE` file for details.
