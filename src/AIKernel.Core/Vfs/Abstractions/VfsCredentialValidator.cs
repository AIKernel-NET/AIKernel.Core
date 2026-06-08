namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Vfs;

/// <summary>Gets the VfsCredentialValidator value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される VfsCredentialValidator 値を取得します。</summary>
public delegate bool VfsCredentialValidator(IVfsCredentials credentials);
