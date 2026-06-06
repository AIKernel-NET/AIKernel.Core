using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Security;

public sealed class SecureCredentialNotFoundException(string key) : SecureCredentialException($"Secret was not found. Key='{key}'.")
{
    public string Key { get; } = key;
}