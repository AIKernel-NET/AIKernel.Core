namespace AIKernel.Core.Execution;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Dtos.Execution;

/// <summary>
/// EN: Computes and validates the SHA-256 integrity chain for a replay dump.
/// JA: リプレイダンプの SHA-256 整合性チェーンを生成・検証します。
/// </summary>
public static class ReplayDumpIntegrity
{
    private const string Algorithm = "SHA256";
    private const string Format = "aikernel.replay-dump.v1";
    private static readonly UTF8Encoding Encoding = new(false, true);

    /// <summary>
    /// EN: Creates a chain from the exact stage outputs and original result to be replayed.
    /// JA: 各段階の出力と再生対象の実行結果からハッシュチェーンを生成します。
    /// </summary>
    /// <remarks>
    /// The v1 format hashes length-prefixed UTF-8 strings and little-endian primitives.
    /// The generation and polish digests include their parent digest. An untrusted
    /// chain can be rewritten in full, so authenticate the dump separately if needed.
    /// </remarks>
    public static HashChain CreateHashChain(
        string dumpId,
        DateTime createdAt,
        RawLogic structureOutput,
        string generationOutput,
        ExecutionResult originalResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpId);
        ArgumentNullException.ThrowIfNull(structureOutput);
        ArgumentNullException.ThrowIfNull(generationOutput);
        ArgumentNullException.ThrowIfNull(originalResult);

        var structureHash = ComputeHash("structure", writer =>
            writer.Write(structureOutput.SerializedRepresentation));
        var generationHash = ComputeHash("generation", writer =>
        {
            writer.Write(structureHash);
            writer.Write(generationOutput);
        });
        var polishHash = ComputeHash("polish", writer =>
        {
            writer.Write(generationHash);
            writer.Write(dumpId);
            writer.Write(createdAt.Ticks);
            writer.Write((byte)createdAt.Kind);
            writer.Write(originalResult.Logic.SerializedRepresentation);
            writer.Write(originalResult.FinalOutput);
            writer.Write(originalResult.IsSuccessful);
            writer.Write(originalResult.ErrorMessage is not null);
            if (originalResult.ErrorMessage is { } error)
            {
                writer.Write(error);
            }

            writer.Write(originalResult.ElapsedMilliseconds);
        });

        return new HashChain
        {
            HashAlgorithm = Algorithm,
            StructureHash = structureHash,
            GenerationParentHash = structureHash,
            GenerationHash = generationHash,
            PolishParentHash = generationHash,
            PolishHash = polishHash
        };
    }

    internal static bool IsValid(ReplayDump? dump)
    {
        if (dump is not
            {
                StructureOutput.SerializedRepresentation: not null,
                GenerationOutput: not null,
                OriginalResult: { Logic.SerializedRepresentation: not null, FinalOutput: not null },
                HashChain: { } chain
            }
            || string.IsNullOrWhiteSpace(dump.DumpId)
            || !string.Equals(chain.HashAlgorithm, Algorithm, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var expected = CreateHashChain(
                dump.DumpId, dump.CreatedAt, dump.StructureOutput, dump.GenerationOutput, dump.OriginalResult);
            return chain.StructureHash == expected.StructureHash
                && chain.GenerationParentHash == expected.GenerationParentHash
                && chain.GenerationHash == expected.GenerationHash
                && chain.PolishParentHash == expected.PolishParentHash
                && chain.PolishHash == expected.PolishHash;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static string ComputeHash(string stage, Action<BinaryWriter> writeFields)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding);
        writer.Write(Format);
        writer.Write(stage);
        writeFields(writer);
        writer.Flush();
        return "sha256:" + Convert.ToHexStringLower(
            SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))));
    }
}
