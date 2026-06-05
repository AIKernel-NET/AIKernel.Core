namespace AIKernel.Core.Tests.ChatHistory;

using AIKernel.Core.ChatHistory;
using ContractHistory = AIKernel.Abstractions.History;
using HistoryDtos = AIKernel.Dtos.History;

public sealed class HistoryContractInterfaceTests
{
    [Fact]
    public async Task Chat_history_exporter_can_be_used_through_contract_interface()
    {
        ContractHistory.IChatHistoryRomExporter exporter =
            new ChatHistoryRomExporter();

        var markdown = await exporter.ToRomMarkdownAsync(
            [
                new HistoryDtos.ChatHistoryRomRecord(
                    "user",
                    "hello",
                    DateTimeOffset.UnixEpoch)
            ],
            new HistoryDtos.ChatHistoryRomOptions(
                "history://agent/session1",
                DateTimeOffset.UnixEpoch),
            TestContext.Current.CancellationToken);

        Assert.Contains("# ROM:ChatHistory", markdown, StringComparison.Ordinal);
        Assert.Contains("@role: user", markdown, StringComparison.Ordinal);
    }
}
