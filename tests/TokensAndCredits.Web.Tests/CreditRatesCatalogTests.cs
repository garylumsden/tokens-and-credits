using System.Text.Json;
using TokensAndCredits.Web.Services.Credits;

namespace TokensAndCredits.Web.Tests;

/// <summary>
/// Validates the shipped <c>Credits</c> configuration deserialises into a usable catalogue:
/// non-empty model list, positive rates, a DefaultId that resolves, and the verified Copilot
/// Studio tier rates.
/// </summary>
public sealed class CreditRatesCatalogTests
{
    private static CreditRatesOptions LoadShippedCredits()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "webappsettings.json");
        Assert.True(File.Exists(path), $"Expected shipped appsettings at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var credits = doc.RootElement.GetProperty(CreditRatesOptions.SectionName);
        var options = JsonSerializer.Deserialize<CreditRatesOptions>(credits.GetRawText());
        Assert.NotNull(options);
        return options!;
    }

    [Fact]
    public void Credits_HasNonEmptyCatalogue_WithValidDefault()
    {
        var credits = LoadShippedCredits();
        var expectedIds = new[]
        {
            "gpt-5-mini", "gpt-5.3-codex", "gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano",
            "gpt-5.5", "gpt-5.6-luna", "gpt-5.6-sol", "gpt-5.6-terra",
            "claude-haiku-4.5", "claude-sonnet-4", "claude-sonnet-4.5", "claude-sonnet-4.6",
            "claude-opus-4.5", "claude-opus-4.6", "claude-opus-4.7", "claude-opus-4.8",
            "claude-opus-5", "claude-sonnet-5", "claude-opus-4.8-fast", "claude-fable-5",
            "claude-fable-5.1", "gemini-3.1-pro", "gemini-3.5-flash", "gemini-3.6-flash",
            "gemini-3.7-flash", "raptor-mini", "mai-code-1-flash", "mai-code-1.1-flash",
            "grok-4.5", "grok-4.6", "kimi-k2.7-code", "kimi-k3",
        };

        Assert.False(string.IsNullOrWhiteSpace(credits.AsOf));
        Assert.NotEmpty(credits.GitHub.Models);
        Assert.False(string.IsNullOrWhiteSpace(credits.GitHub.DefaultId));
        Assert.Contains(credits.GitHub.Models, m => m.Id == credits.GitHub.DefaultId);
        Assert.Equal(credits.GitHub.Models.Count, credits.GitHub.Models.Select(m => m.Id).Distinct().Count());
        Assert.Equal(expectedIds.Order(), credits.GitHub.Models.Select(m => m.Id).Order());
    }

    [Fact]
    public void Credits_EveryModel_HasIdLabelAndNonNegativeRates()
    {
        var credits = LoadShippedCredits();

        foreach (var model in credits.GitHub.Models)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id));
            Assert.False(string.IsNullOrWhiteSpace(model.Label));
            Assert.True(model.InputPerMillion > 0, $"{model.Id} input rate should be positive.");
            Assert.True(model.OutputPerMillion > 0, $"{model.Id} output rate should be positive.");
            Assert.True(model.CacheReadPerMillion >= 0);
            Assert.True(model.CacheWritePerMillion >= 0);

            var longContextRates = new decimal?[]
            {
                model.LongContextInputPerMillion,
                model.LongContextCacheReadPerMillion,
                model.LongContextCacheWritePerMillion,
                model.LongContextOutputPerMillion,
            };
            if (model.LongContextThreshold is null)
            {
                Assert.All(longContextRates, rate => Assert.Null(rate));
                continue;
            }

            Assert.True(model.LongContextThreshold > 0);
            Assert.All(longContextRates, rate => Assert.NotNull(rate));
            Assert.True(model.LongContextInputPerMillion > 0);
            Assert.True(model.LongContextCacheReadPerMillion >= 0);
            Assert.True(model.LongContextCacheWritePerMillion >= 0);
            Assert.True(model.LongContextOutputPerMillion > 0);
        }
    }

    [Fact]
    public void Credits_EveryModel_ProducesFinitePositiveEstimates()
    {
        var credits = LoadShippedCredits();

        foreach (var model in credits.GitHub.Models)
        {
            var defaultUsage = new Services.Models.UsageBreakdown(1_000, 500, 100, 100, 1_600);
            var estimate = CreditEstimator.EstimateGitHub(defaultUsage, model);
            Assert.True(estimate.Total > 0, $"{model.Id} should produce a positive estimate.");

            if (model.LongContextThreshold is long threshold)
            {
                var prompt = checked((int)threshold + 1);
                var longUsage = new Services.Models.UsageBreakdown(prompt, 500, 100, 100, prompt + 200);
                var longEstimate = CreditEstimator.EstimateGitHub(longUsage, model);
                Assert.True(longEstimate.Total > 0, $"{model.Id} long-context estimate should be positive.");
            }
        }
    }

    [Fact]
    public void Credits_CopilotStudioTiers_MatchVerifiedRates()
    {
        var credits = LoadShippedCredits();

        Assert.Equal(0.1m, credits.CopilotStudio.Basic);
        Assert.Equal(1.5m, credits.CopilotStudio.Standard);
        Assert.Equal(10m, credits.CopilotStudio.Premium);
    }
}
