using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Credits;

/// <summary>
/// Converts a run's <see cref="UsageBreakdown"/> into AI Credit estimates for a chosen GitHub
/// Copilot billing model and the three Copilot Studio tiers.
/// </summary>
/// <remarks>
/// The live UI computes the same maths in JavaScript so changing the billing model recomputes
/// instantly without a re-run; this type exists to keep the rate model honest and unit-tested.
/// Token-class mapping: fresh input = Prompt + additional prompt − cache read − cache write;
/// output = Output + Reasoning (reasoning is billed as output). Cache-write tokens are supplied
/// separately because Azure OpenAI and local usage responses do not report them.
/// </remarks>
public static class CreditEstimator
{
    private const decimal PerMillion = 1_000_000m;
    private const decimal PerThousand = 1_000m;

    /// <summary>Estimates GitHub Copilot credits for one request.</summary>
    /// <param name="usage">Token usage from the run.</param>
    /// <param name="model">Per-million credit rates for the selected billing model.</param>
    /// <param name="additionalPromptTokens">Prompt tokens added by the Copilot agent.</param>
    /// <param name="cacheWriteTokens">Prompt tokens written to the provider cache.</param>
    /// <returns>The per-class and total GitHub credit estimate.</returns>
    public static GitHubCreditEstimate EstimateGitHub(
        UsageBreakdown usage,
        GitHubModelRate model,
        long additionalPromptTokens = 0,
        long cacheWriteTokens = 0)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(model);

        var prompt = Math.Max(0L, usage.Prompt);
        var additionalPrompt = Math.Max(0L, additionalPromptTokens);
        var billedPrompt = prompt + additionalPrompt;
        var cacheReadTokens = Math.Min(Math.Max(0L, usage.Cached ?? 0), billedPrompt);
        var billedCacheWriteTokens = Math.Min(
            Math.Max(0L, cacheWriteTokens),
            billedPrompt - cacheReadTokens);
        var freshInputTokens = billedPrompt - cacheReadTokens - billedCacheWriteTokens;
        var outputTokens = Math.Max(0L, (long)usage.Output + (usage.Reasoning ?? 0));
        var useLongContext = model.LongContextThreshold is long threshold && billedPrompt > threshold;
        var inputRate = useLongContext ? model.LongContextInputPerMillion ?? model.InputPerMillion : model.InputPerMillion;
        var cacheReadRate = useLongContext ? model.LongContextCacheReadPerMillion ?? model.CacheReadPerMillion : model.CacheReadPerMillion;
        var cacheWriteRate = useLongContext ? model.LongContextCacheWritePerMillion ?? model.CacheWritePerMillion : model.CacheWritePerMillion;
        var outputRate = useLongContext ? model.LongContextOutputPerMillion ?? model.OutputPerMillion : model.OutputPerMillion;

        var input = freshInputTokens / PerMillion * inputRate;
        var cacheRead = cacheReadTokens / PerMillion * cacheReadRate;
        var cacheWrite = billedCacheWriteTokens / PerMillion * cacheWriteRate;
        var outputCost = outputTokens / PerMillion * outputRate;

        return new GitHubCreditEstimate(
            model.Id,
            model.Label,
            input,
            cacheRead,
            cacheWrite,
            outputCost,
            input + cacheRead + cacheWrite + outputCost);
    }

    /// <summary>Estimates the Copilot Studio AI-tool token charge across all three tiers.</summary>
    /// <param name="usage">Token usage from the run.</param>
    /// <param name="rates">Per-1,000-token tier rates.</param>
    /// <param name="additionalTokens">Unreported tokens added by the Copilot Studio prompt.</param>
    /// <returns>The Basic/Standard/Premium credit estimates.</returns>
    public static CopilotStudioEstimate EstimateCopilotStudio(
        UsageBreakdown usage,
        CopilotStudioRates rates,
        long additionalTokens = 0)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(rates);

        var billedTokens = Math.Max(0L, usage.Total) + Math.Max(0L, additionalTokens);
        var thousands = Math.Ceiling(billedTokens / PerThousand);
        return new CopilotStudioEstimate(
            thousands * rates.Basic,
            thousands * rates.Standard,
            thousands * rates.Premium);
    }
}

/// <summary>A GitHub Copilot credit estimate broken down by token class.</summary>
/// <param name="ModelId">Selected billing-model id.</param>
/// <param name="ModelLabel">Selected billing-model label.</param>
/// <param name="Input">Credits for fresh input tokens.</param>
/// <param name="CacheRead">Credits for cache-read tokens.</param>
/// <param name="CacheWrite">Credits for cache-write tokens.</param>
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
