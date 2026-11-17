using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Fact]
    public void ResponsibilityDefinitionsAreVerbatimAndDoNotReplaceAnyExistingCriterion()
    {
        foreach(var pair in Rules())
        {
            var original=BonsaiCtgFocusedPrompt.Build(pair.Value,pair.Key,"{}","\"candidate\"","untrusted");
            var defined=BonsaiCtgFocusedPrompt.Build(pair.Value,pair.Key,"{}","\"candidate\"","untrusted",true);
            int start=pair.Value.Text.IndexOf("## 2. Core Responsibilities",StringComparison.Ordinal);
            int end=pair.Value.Text.IndexOf("## 3.",start,StringComparison.Ordinal);
            string section=pair.Value.Text[start..end];
            Assert.Equal(original.System,defined.System.Replace(section+"\n","",StringComparison.Ordinal));
            Assert.Equal(original.Excerpt,defined.Excerpt.Replace(section+"\n","",StringComparison.Ordinal));
            Assert.Equal(original.User,defined.User);
            Assert.Equal(original.Reference.ContentHash,defined.Reference.ContentHash);
            Assert.Contains("2. Core Responsibilities",defined.Reference.Section!);
            Assert.DoesNotContain("2. Core Responsibilities",original.Reference.Section!);
            if(pair.Key==CouncilKind.Ethos)
                Assert.Contains("Ethos must reject decisions that rely on opaque, unverifiable, or non-auditable reasoning.",defined.System);
        }
    }
}
