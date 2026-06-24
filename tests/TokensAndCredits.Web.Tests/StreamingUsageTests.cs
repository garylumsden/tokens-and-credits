using Microsoft.Extensions.AI;
using TokensAndCredits.Web.Services.Chat;

namespace TokensAndCredits.Web.Tests;

public sealed class StreamingUsageTests
{
    [Fact]
    public void Final_TakesLastUsage_NotTheSumOfPerChunkUsage()
    {
        // Simulate a local server (llama.cpp / Foundry Local) that reports usage in EVERY chunk:
        // prompt is constant per chunk and output is cumulative. Summing these (what
        // ToChatResponse does) would massively inflate the totals.
        var updates = new List<ChatResponseUpdate>();
        for (var output = 1; output <= 100; output++)
        {
            updates.Add(new ChatResponseUpdate
            {
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = 245,
                    OutputTokenCount = output,
                    TotalTokenCount = 245 + output,
                })],
            });
        }

        var usage = StreamingUsage.Final(updates);

        Assert.NotNull(usage);
        Assert.Equal(245, usage!.InputTokenCount);   // not 245 * 100
        Assert.Equal(100, usage.OutputTokenCount);   // not 1+2+...+100 = 5050
        Assert.Equal(345, usage.TotalTokenCount);
    }

    [Fact]
    public void Final_ReturnsNull_WhenNoUsageReported()
    {
        var updates = new List<ChatResponseUpdate>
        {
            new() { Contents = [new TextContent("hello")] },
            new() { Contents = [new TextContent(" world")] },
        };

        Assert.Null(StreamingUsage.Final(updates));
    }
}
