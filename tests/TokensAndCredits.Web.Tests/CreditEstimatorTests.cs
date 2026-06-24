using TokensAndCredits.Web.Services.Credits;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Tests;

public sealed class CreditEstimatorTests
{
    // Claude Opus 4.8 rates (credits per 1M tokens): input 500, cache-read 50, cache-write 625, output 2500.
    private static readonly GitHubModelRate Opus = new()
    {
        Id = "claude-opus-4.8",
        Label = "Claude Opus 4.8",
        InputPerMillion = 500,
        CacheReadPerMillion = 50,
        CacheWritePerMillion = 625,
        OutputPerMillion = 2500,
    };

    [Fact]
    public void EstimateGitHub_MapsTokenClasses_AndReasoningBilledAsOutput()
    {
        // Prompt 1,000,000 incl. 200,000 cached; output 1,000,000 visible + 500,000 reasoning.
        var usage = new UsageBreakdown(Prompt: 1_000_000, Output: 1_000_000, Reasoning: 500_000, Cached: 200_000, Total: 2_500_000);

        var result = CreditEstimator.EstimateGitHub(usage, Opus);

        // input (non-cached) = 800,000 → 0.8 * 500 = 400
        Assert.Equal(400m, result.Input);
        // cache-read = 200,000 → 0.2 * 50 = 10
        Assert.Equal(10m, result.CacheRead);
        // cache-write always 0 (Azure/local report reads only)
        Assert.Equal(0m, result.CacheWrite);
        // output = 1,500,000 (incl. reasoning) → 1.5 * 2500 = 3750
        Assert.Equal(3750m, result.Output);
        // total = sum of classes
        Assert.Equal(4160m, result.Total);
        Assert.Equal(result.Input + result.CacheRead + result.CacheWrite + result.Output, result.Total);
    }

    [Fact]
    public void EstimateGitHub_TreatsNullCachedAndReasoning_AsZero()
    {
        var usage = new UsageBreakdown(Prompt: 1_000_000, Output: 1_000_000, Reasoning: null, Cached: null, Total: 2_000_000);

        var result = CreditEstimator.EstimateGitHub(usage, Opus);

        Assert.Equal(500m, result.Input);   // full prompt billed as input
        Assert.Equal(0m, result.CacheRead);
        Assert.Equal(2500m, result.Output); // no reasoning added
    }

    [Fact]
    public void EstimateCopilotStudio_AppliesTierRatePer1000Tokens_ToTotal()
    {
        var usage = new UsageBreakdown(Prompt: 0, Output: 0, Reasoning: null, Cached: null, Total: 10_000);
        var rates = new CopilotStudioRates { Basic = 0.1m, Standard = 1.5m, Premium = 10m };

        var result = CreditEstimator.EstimateCopilotStudio(usage, rates);

        // 10,000 / 1000 = 10 thousands
        Assert.Equal(1m, result.Basic);
        Assert.Equal(15m, result.Standard);
        Assert.Equal(100m, result.Premium);
    }

    [Fact]
    public void EstimateGitHub_DifferentModel_ChangesOnlyGitHubFigure()
    {
        var usage = new UsageBreakdown(Prompt: 1_000_000, Output: 1_000_000, Reasoning: null, Cached: null, Total: 2_000_000);
        var mini = new GitHubModelRate
        {
            Id = "gpt-5-mini",
            Label = "GPT-5 mini",
            InputPerMillion = 25,
            CacheReadPerMillion = 2.5m,
            CacheWritePerMillion = 0,
            OutputPerMillion = 200,
        };

        var opus = CreditEstimator.EstimateGitHub(usage, Opus);
        var cheap = CreditEstimator.EstimateGitHub(usage, mini);

        Assert.Equal(3000m, opus.Total); // 500 + 2500
        Assert.Equal(225m, cheap.Total); // 25 + 200
    }
}
