using System.Security.Cryptography;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

internal static class ProfileReader
{
    internal static DenseRegistrationDocument ReadVerified(string json, string registeredModelId)
    {
        ArgumentNullException.ThrowIfNull(json);
        var result = ReadDocument(json);
        result.Validate(registeredModelId);
        return result;
    }

    private static DenseRegistrationDocument ReadDocument(string json)
    {
        try
        {
            return Deserialize(DenseProfileResolver.Resolve(json));
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Invalid model registration.", error);
        }
    }

    private static DenseRegistrationDocument Deserialize(string resolved) =>
        JsonSerializer.Deserialize(resolved, DenseRegistrationJson.Default.DenseRegistrationDocument)
        ?? throw new InvalidDataException("A model registration is required.");

    internal static void VerifyWeights(Stream stream, string expected)
    {
        var digest = SHA256.HashData(stream);
        stream.Position = 0;
        Verify(digest, expected);
    }

    internal static void VerifyTokenizer(GgufWeights weights, string expected) =>
        Verify(EmbeddedTokenizerDigest.Compute(weights), expected);

    internal static void Verify(byte[] actual, string expected)
    {
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected)))
            throw new InvalidDataException("Registered artifact digest mismatch.");
    }
}
