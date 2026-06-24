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

        Assert.False(string.IsNullOrWhiteSpace(credits.AsOf));
        Assert.NotEmpty(credits.GitHub.Models);
        Assert.False(string.IsNullOrWhiteSpace(credits.GitHub.DefaultId));
        Assert.Contains(credits.GitHub.Models, m => m.Id == credits.GitHub.DefaultId);
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
