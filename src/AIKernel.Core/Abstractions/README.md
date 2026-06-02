# Abstractions

# 抽象化レイヤー

This directory contains interfaces and abstract types that provide the abstraction layer for the AIKernel.NET project.

このディレクトリには、AIKernel.NET プロジェクトの抽象化レイヤーを提供する Interface および抽象型を配置しています。

---

## Purpose

## 目的

The interfaces and abstract classes in this directory are intended to be upstreamed into the official AIKernel.NET repository and adopted as part of the AIKernel.Abstractions package.

このディレクトリ配下の Interface および抽象クラスは、将来的に AIKernel.NET リポジトリへ取り込まれ、正式に `AIKernel.Abstractions` パッケージの一部として採用されることを想定しています。

This directory exists as a temporary staging area required for the development of AIKernel.Core.

このディレクトリは、AIKernel.Core の開発を進めるために必要となる、一時的なステージング領域です。

---

## Namespace Policy

## 名前空間ポリシー

The namespace is intentionally defined as `AIKernel.Abstractions`.

名前空間は、意図的に `AIKernel.Abstractions` としています。

This is because these interfaces are not project-specific temporary contracts, but candidates for the official abstraction layer of AIKernel.NET.

これは、これらの Interface がプロジェクト固有の一時的な契約ではなく、AIKernel.NET の正式な抽象化レイヤーへ移行する前提の候補であるためです。

---

## Lifecycle

## ライフサイクル

After the `AIKernel.Abstractions` package in the AIKernel.NET repository is updated, the interfaces in this directory will be removed.

AIKernel.NET リポジトリ側の `AIKernel.Abstractions` パッケージが更新された後、このディレクトリに配置されている Interface は削除されます。

At that point, AIKernel.Core will reference and use the official interfaces provided by the `AIKernel.Abstractions` package.

その時点で、AIKernel.Core は `AIKernel.Abstractions` パッケージから提供される正式な Interface を参照して使用します。

---

## Important Notes

## 注意事項

The types in this directory should be treated as provisional definitions.

このディレクトリ内の型は、暫定的な定義として扱ってください。

They should remain aligned with the design direction of AIKernel.NET and should not diverge from the official abstraction model.

これらの型は、AIKernel.NET の設計方針と整合する必要があり、正式な抽象化モデルから逸脱しないようにしてください。

Once the corresponding interfaces are available from the official `AIKernel.Abstractions` package, these local definitions must be removed to avoid duplication and type conflicts.

対応する Interface が正式な `AIKernel.Abstractions` パッケージから提供された後は、重複や型の競合を避けるため、このディレクトリ内のローカル定義は削除してください。