using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Credits;

/// <summary>
/// Converts a run's <see cref="UsageBreakdown"/> into AI Credit estimates for a chosen GitHub
/// Copilot billing model and the three Copilot Studio tiers.
/// </summary>
/// <remarks>
/// The live UI computes the same maths in JavaScript so changing the billing model recomputes
/// instantly without a re-run; this type exists to keep the rate model honest and unit-tested.
/// Token-class mapping: input = Prompt − Cached, cache-read = Cached, cache-write = 0 (Azure
/// OpenAI / local report cache reads only), output = Output + Reasoning (reasoning billed as
/// output).
/// </remarks>
public static class CreditEstimator
{
    private const decimal PerMillion = 1_000_000m;
    private const decimal PerThousand = 1_000m;

    /// <summary>Estimates GitHub Copilot credits for the supplied usage and model rates.</summary>
    /// <param name="usage">Token usage from the run.</param>
    /// <param name="model">Per-million credit rates for the selected billing model.</param>
    /// <returns>The per-class and total GitHub credit estimate.</returns>
    public static GitHubCreditEstimate EstimateGitHub(UsageBreakdown usage, GitHubModelRate model)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(model);

        var cached = usage.Cached ?? 0;
        var inputNonCached = Math.Max(0, usage.Prompt - cached);
        var output = usage.Output + (usage.Reasoning ?? 0);

        var input = inputNonCached / PerMillion * model.InputPerMillion;
        var cacheRead = cached / PerMillion * model.CacheReadPerMillion;
        var cacheWrite = 0m; // Azure OpenAI / local report cache reads only.
        var outputCost = output / PerMillion * model.OutputPerMillion;

        return new GitHubCreditEstimate(
            model.Id,
            model.Label,
            input,
            cacheRead,
            cacheWrite,
            outputCost,
            input + cacheRead + cacheWrite + outputCost);
    }

    /// <summary>Estimates Copilot Studio credits across all three tiers for the supplied usage.</summary>
    /// <param name="usage">Token usage from the run.</param>
    /// <param name="rates">Per-1,000-token tier rates.</param>
    /// <returns>The Basic/Standard/Premium credit estimates.</returns>
    public static CopilotStudioEstimate EstimateCopilotStudio(UsageBreakdown usage, CopilotStudioRates rates)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(rates);

        var thousands = usage.Total / PerThousand;
        return new CopilotStudioEstimate(
            thousands * rates.Basic,
            thousands * rates.Standard,
            thousands * rates.Premium);
    }
}

/// <summary>A GitHub Copilot credit estimate broken down by token class.</summary>
/// <param name="ModelId">Selected billing-model id.</param>
/// <param name="ModelLabel">Selected billing-model label.</param>
/// <param name="Input">Credits for non-cached input tokens.</param>
/// <param name="CacheRead">Credits for cache-read tokens.</param>
/// <param name="CacheWrite">Credits for cache-write tokens (always 0 here).</param>
/// <param name="Output">Credits for output tokens (incl. reasoning).</param>
/// <param name="Total">Sum of all classes.</param>
public sealed record GitHubCreditEstimate(
    string ModelId,
    string ModelLabel,
    decimal Input,
    decimal CacheRead,
    decimal CacheWrite,
    decimal Output,
    decimal Total);

/// <summary>Copilot Studio credit estimates for each tier.</summary>
/// <param name="Basic">Credits at the Basic tier rate.</param>
/// <param name="Standard">Credits at the Standard tier rate.</param>
/// <param name="Premium">Credits at the Premium tier rate.</param>
public sealed record CopilotStudioEstimate(
    decimal Basic,
    decimal Standard,
    decimal Premium);
