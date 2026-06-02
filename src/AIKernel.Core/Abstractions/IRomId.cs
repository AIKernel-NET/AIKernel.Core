#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

/// <summary>
/// ROM の論理識別子を表す抽象契約です。
///
/// 設計意図:
/// ContextAssembler や GovernancePolicy が RomId という具象 record に依存すると、
/// 利用者が独自の ROM ID 実装を差し込めなくなります。
///
/// 抽象層では「値を持つ ROM 識別子である」という最小契約だけを扱い、
/// 実際の record / class / struct の選択は実装側へ委ねます。
/// </summary>
public interface IRomId
{
    string Value { get; }
}