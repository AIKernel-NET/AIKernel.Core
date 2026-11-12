namespace AIKernel.Core.Memory;

/// <summary>Controls authenticated swap encryption and clearing of released storage.</summary>
public readonly record struct SwapEncryptionPolicy(bool Encrypt, bool ZeroWipe);
