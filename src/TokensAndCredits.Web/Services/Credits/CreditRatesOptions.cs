namespace TokensAndCredits.Web.Services.Credits;

/// <summary>
/// Configuration for converting token usage into AI Credits, bound from the
/// <c>Credits</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// Rates are list prices that change over time, hence the <see cref="AsOf"/> label and the
/// fully-configurable catalogue. 1 AI Credit = $0.01 USD across both GitHub Copilot and
/// Copilot Studio.
/// </remarks>
public sealed class CreditRatesOptions
{
    public const string SectionName = "Credits";

    /// <summary>Human-readable "rates as of" label shown in the UI (e.g. "June 2026").</summary>
    public string AsOf { get; set; } = "";

    /// <summary>Copilot Studio / Microsoft 365 Copilot per-1,000-token tier rates.</summary>
    public CopilotStudioRates CopilotStudio { get; set; } = new();

    /// <summary>GitHub Copilot per-model billing catalogue used by the billing-model selector.</summary>
    public GitHubCreditCatalog GitHub { get; set; } = new();
}

/// <summary>
/// Copilot Studio "Text and generative AI tools" rates, expressed as credits per 1,000 tokens.
/// The tier that applies is set by the model the AI tool uses, so all three are surfaced.
/// </summary>
public sealed class CopilotStudioRates
{
    /// <summary>Basic tier credits per 1,000 tokens.</summary>
    public decimal Basic { get; set; } = 0.1m;

    /// <summary>Standard tier credits per 1,000 tokens.</summary>
    public decimal Standard { get; set; } = 1.5m;

    /// <summary>Premium tier credits per 1,000 tokens.</summary>
    public decimal Premium { get; set; } = 10m;
}

/// <summary>The GitHub Copilot model catalogue offered by the billing-model selector.</summary>
public sealed class GitHubCreditCatalog
{
    /// <summary>Id of the model selected by default; must match one of <see cref="Models"/>.</summary>
    public string DefaultId { get; set; } = "";

    /// <summary>Per-model credit rates the user can price their tokens against.</summary>
    public List<GitHubModelRate> Models { get; set; } = new();
}

/// <summary>
/// Per-model GitHub Copilot credit rates. All values are credits per 1,000,000 tokens
/// (1 AI Credit = $0.01 USD).
/// </summary>
public sealed class GitHubModelRate
{
    /// <summary>Stable identifier used by the selector and DefaultId.</summary>
    public string Id { get; set; } = "";

    /// <summary>Friendly label shown in the selector.</summary>
    public string Label { get; set; } = "";

    /// <summary>Credits per 1M input (non-cached) tokens.</summary>
    public decimal InputPerMillion { get; set; }

    /// <summary>Credits per 1M cache-read tokens.</summary>
    public decimal CacheReadPerMillion { get; set; }

    /// <summary>Credits per 1M cache-write tokens (0 for models with no cache-write charge).</summary>
    public decimal CacheWritePerMillion { get; set; }

    /// <summary>Credits per 1M output tokens (reasoning is billed at this rate).</summary>
    public decimal OutputPerMillion { get; set; }
}
