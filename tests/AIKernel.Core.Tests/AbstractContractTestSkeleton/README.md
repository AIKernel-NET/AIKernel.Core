# AbstractContractTestSkeleton

# 抽象コントラクトテストスケルトン

This directory contains abstract contract test skeletons used by the AIKernel.Core test project.

このディレクトリには、AIKernel.Core のテストプロジェクトで使用する抽象コントラクトテストスケルトンを配置しています。

---

## Purpose

## 目的

The types in this directory define reusable abstract test structures for verifying that implementations conform to AIKernel.NET contracts.

このディレクトリ内の型は、各実装が AIKernel.NET の Contract に準拠していることを検証するための、再利用可能な抽象テスト構造を定義します。

They are intended to reduce duplicated test logic and provide a consistent testing model for providers, services, and other contract-based components.

Provider、Service、その他の Contract ベースのコンポーネントに対して、重複したテストロジックを削減し、一貫したテストモデルを提供することを目的としています。

---

## Temporary Staging Area

## 一時的なステージング領域

This directory is a temporary staging area for contract test skeletons required during the development of AIKernel.Core.

このディレクトリは、AIKernel.Core の開発中に必要となる Contract Test Skeleton を一時的に配置するためのステージング領域です。

The test skeletons defined here are expected to be upstreamed into the AIKernel.NET repository in the future.

ここで定義されたテストスケルトンは、将来的に AIKernel.NET リポジトリ側へ取り込まれることを想定しています。

After the official `ContractTestSkeleton` package or directory is added to AIKernel.NET, these local definitions should be removed and replaced with the official ones.

AIKernel.NET 側に正式な `ContractTestSkeleton` パッケージまたはディレクトリが追加された後、このローカル定義は削除し、正式版を参照する形へ移行します。

---

## Namespace Policy

## 名前空間ポリシー

The namespace is intentionally aligned with the AIKernel.Core test project.

名前空間は、意図的に AIKernel.Core のテストプロジェクトに合わせています。

This is because the current definitions are used only as local test infrastructure for Core development.

これは、現在の定義が Core 開発におけるローカルなテスト基盤としてのみ使用されるためです。

When these test skeletons are upstreamed into AIKernel.NET, their namespace should be reviewed and adjusted according to the official repository structure.

これらのテストスケルトンを AIKernel.NET 側へ取り込む際には、正式なリポジトリ構成に合わせて名前空間を見直してください。

---

## Design Policy

## 設計方針

Contract test skeletons should describe the expected behavior of an interface or abstract contract, not the internal details of a specific implementation.

Contract Test Skeleton は、特定の実装の内部構造ではなく、Interface や抽象 Contract が満たすべき振る舞いを記述します。

Tests in this directory should remain implementation-agnostic.

このディレクトリ内のテストは、特定の実装に依存しない形を維持してください。

Concrete test classes should inherit from these skeletons and provide only the implementation-specific setup required to execute the shared contract tests.

具象テストクラスは、これらのスケルトンを継承し、共通の Contract テストを実行するために必要な実装固有のセットアップのみを提供します。

---

## Expected Usage

## 想定される利用方法

A typical contract test skeleton defines shared tests for a contract and exposes abstract factory methods or setup hooks for concrete implementations.

一般的な Contract Test Skeleton は、Contract に対する共通テストを定義し、具象実装向けに抽象ファクトリメソッドやセットアップ用のフックを提供します。

Example pattern:

例:

```csharp
public abstract class SomeContractTestSkeleton
{
    protected abstract ISomeContract CreateTarget();

    [Fact]
    public void Method_Should_Satisfy_Contract()
    {
        var target = CreateTarget();

        // Assert contract behavior here.
    }
}