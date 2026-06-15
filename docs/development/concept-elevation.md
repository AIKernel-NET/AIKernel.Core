# Concept Elevation Notes / 概念昇格ノート

## Canonical Reference / 正典参照

Common naming rules are maintained in the AIKernel.NET documentation set:

- `AIKernel.NET/docs/canonical-language/index.md`
- `AIKernel.NET/docs/design/concept-elevation-refactoring-design.md`
- `AIKernel.NET/docs/guidelines/concept-elevation-guidelines.md`
- `AIKernel.NET/docs/migration/concept-elevation-v0.1.1.1.md`
- `AIKernel.NET/docs/todo/concept-elevation-refactoring-todo.md`

共通の命名規約は AIKernel.NET 側の documentation set を正典として扱います。

## Core Scope / Core の対象範囲

AIKernel.Core adds concept facades only. Existing public API, CTG contracts,
GateInput, gate evaluators, and reject taxonomy are not changed.

AIKernel.Core では concept facade の横追加のみを行います。既存 public API、
CTG contract、GateInput、gate evaluator、reject taxonomy は変更しません。

Added concept surfaces:

- `AIKernel.Core.Concepts.TelosObjective`
- `AIKernel.Core.Concepts.NomosCanon`
- `AIKernel.Core.Concepts.DikeSafetyBoundary`
- `AIKernel.Core.Concepts.EthosCouncil`
- `AIKernel.Core.Concepts.PathosCouncil`
- `AIKernel.Core.Concepts.LogosCouncil`

## Guardrails / 境界ルール

- Concept names are allowed only in the `Concepts` surface.
- DTO, request, result, mapper, adapter, serializer, converter, HTTP client,
  native bridge, and provider implementation names must keep technical names.
- GateInput remains Logos / Ethos / Pathos votes only.
- Continuous carriers such as confidence, score, probability, and risk are not
  allowed in GateInput.

概念名は `Concepts` surface のみに置きます。DTO / request / result / mapper /
adapter / serializer / converter / HTTP client / native bridge / provider
implementation では通常の技術名を維持します。

## Tests / テスト

`tests/AIKernel.Core.Tests/ConceptElevation/ConceptElevationArchitectureTests.cs`
guards the naming boundary and verifies that concept facades are available
without modifying CTG contracts.

## Remaining Work / 残タスク

- Internal migration to concept facades is optional and must stay add-only.
- Obsolete attributes for older technical names require a separate migration
  note and are not applied in this update.
