using System.Numerics;

namespace AIKernel.Kernel.Inference;

/// <summary>One bit per expert; each ring entry represents exactly one router decision at this layer.</summary>
internal sealed class ExpertSelectionWindow(int size)
{
    private readonly uint[] history = new uint[size];
    private readonly int[] counts = new int[32];
    private readonly long[] promotedAt = new long[32];
    internal long Visits { get; private set; }
    internal int Count(int expert) => counts[expert];
    internal bool IsRetained(int expert) => promotedAt[expert] != 0;
    internal long Age(int expert) => Visits - promotedAt[expert];
    internal void SetRetained(int expert, bool value) => promotedAt[expert] = value ? Visits : 0;

    internal void Advance(ReadOnlySpan<int> selection)
    {
        uint mask = 0;
        foreach (int expert in selection) mask |= 1u << expert;
        int slot = (int)(Visits % history.Length);
        Adjust(history[slot], -1);
        history[slot] = mask;
        Adjust(mask, 1);
        Visits++;
    }

    internal void Rank(Span<int> ranked)
    {
        for (int i = 1; i < ranked.Length; i++)
        {
            int expert = ranked[i], j = i;
            while (j > 0 && Count(ranked[j - 1]) < Count(expert))
            {
                ranked[j] = ranked[j - 1];
                j--;
            }
            ranked[j] = expert;
        }
    }

    private void Adjust(uint mask, int delta)
    {
        while (mask != 0)
        {
            counts[BitOperations.TrailingZeroCount(mask)] += delta;
            mask &= mask - 1;
        }
    }
}
