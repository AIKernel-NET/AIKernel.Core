using AIKernel.Core.Memory;
using AIKernel.Kernel.Inference;
using AIKernel.Kernel.Memory;

namespace AIKernel.Core.Tests.Inference;

public sealed class ExpertFrequencyTests
{
    private const int Page = 1 << 20;
    private readonly HashSet<long> hot = [];
    private ExpertPageRanges Ranges(int start)
    {
        var experts = Enumerable.Range(0, 32)
            .Select(expert => Enumerable.Range(start + expert * 3, 3)
                .Select(page => ((long)page, 1)).ToArray()).ToArray();
        return new(experts, (range, retain) =>
        {
            if (retain) Assert.True(hot.Add(range.First));
            else Assert.True(hot.Remove(range.First));
        });
    }

    [Fact]
    public void OptionsValidateAtHostBoundaryAndDefaultToDemandPaging()
    {
        var options = new GgufMemoryOptions { SwapDirectory = "." };
        Assert.False(options.ExpertFrequency.Enabled);
        var defaults = options.ExpertFrequency;
        Assert.Throws<ArgumentNullException>(() => options with { ExpertFrequency = null! });
        foreach (var invalid in new[] { defaults with { Window = 1 }, defaults with { Window = 257 },
            defaults with { DemoteAt = -1 }, defaults with { PromoteAt = 1 }, defaults with { PromoteAt = 9 },
            defaults with { MinimumResidency = 0 }, defaults with { MinimumResidency = 4097 },
            defaults with { MaximumBytes = Page - 1 } })
            Assert.Throws<ArgumentOutOfRangeException>(() => options with { ExpertFrequency = invalid });
    }

    [Fact]
    public void PerLayerHistoryProtectsThreeProjectionsAndSurvivesAnIdleVisit()
    {
        var policy = new ExpertFrequencyOptions { Window = 3, PromoteAt = 2, DemoteAt = 1, MinimumResidency = 4 };
        var engine = new ExpertRetention(policy, Page);
        var ranges = Ranges(0);
        void See(params int[] active) => engine.Observe(1, active, ranges, 4L * Page);
        See(2, 1); See(2, 1); See(2, 1);
        Assert.Equal(new long[] { 6, 7, 8 }, hot.Order().ToArray());
        See(1); See(1); See(1);
        var snapshot = engine.Snapshot();
        Assert.Equal(1, snapshot.TotalDecisions);
        Assert.Equal(snapshot.Decisions.ToArray(), engine.Snapshot().Decisions.ToArray());
        See(1);
        var decisions = engine.Snapshot().Decisions;
        Assert.Equal(new[] { 2, 2, 1 }, decisions.Select(x => x.Expert));
        Assert.Equal(new long[] { 3, 7, 7 }, decisions.Select(x => x.Visit));
        Assert.Equal(new[] { 3, 0, 3 }, decisions.Select(x => x.Selections));
        Assert.Equal(new long[] { 3, 4, 5 }, hot.Order().ToArray());
    }

    [Fact]
    public void ShrinkReconcilesOtherLayersImmediatelyEvenDuringMinimumResidency()
    {
        var engine = new ExpertRetention(new() { Window = 2, PromoteAt = 2, MinimumResidency = 90 }, Page);
        var first = Ranges(0);
        var second = Ranges(1000);
        for (int i = 0; i < 2; i++)
        {
            engine.Observe(0, [1], first, 7L * Page);
            engine.Observe(1, [2], second, 7L * Page);
        }
        Assert.Equal(6, hot.Count);
        engine.Limit(4L * Page);
        var snapshot = engine.Snapshot();
        Assert.Equal(3, snapshot.RetainedPages);
        Assert.True(snapshot.Decisions[^1].BudgetPressure);
        Assert.Equal(0, snapshot.Decisions[^1].Layer);
        engine.Limit(Page);
        Assert.Empty(hot);
        Assert.Equal(0, engine.Snapshot().RetainedPages);
        engine.Observe(1, [2], second, Page);
        Assert.Empty(hot);
    }

    [Fact]
    public void RetentionChangesEvictionAndHardTrimStillReleasesEveryPage()
    {
        var directory = Directory.CreateTempSubdirectory("aikernel-expert-retention-");
        try
        {
            using var space = new VirtualMemorySpace(3, 16, 2, directory.FullName);
            space.PreferRamPages(page => page == 0);
            for (int page = 0; page < 3; page++) space.WriteBlock(page, new byte[16]);
            Assert.Equal(VirtualMemoryTier.Ram, space.GetBlockTier(0));
            Assert.Equal(VirtualMemoryTier.Swap, space.GetBlockTier(1));
            space.TrimRam(0);
            Assert.Equal(0, space.GetRamUsage().AllocatedBytes);
        }
        finally { directory.Delete(true); }
    }
}
