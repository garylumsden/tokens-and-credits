namespace TokensAndCredits.Web.Services.Models;

/// <summary>
/// Token usage as reported by the live model response (origin = "model").
/// Reasoning and Cached are null when the model/service does not report them
/// (e.g. Foundry Local returns prompt/output/total only).
/// </summary>
/// <param name="Prompt">prompt_tokens (REST) / InputTokenCount.</param>
/// <param name="Output">Visible answer tokens (completion_tokens minus reasoning_tokens).</param>
/// <param name="Reasoning">completion_tokens_details.reasoning_tokens; null if unsupported.</param>
/// <param name="Cached">prompt_tokens_details.cached_tokens; null if unsupported.</param>
/// <param name="Total">total_tokens / TotalTokenCount.</param>
public sealed record UsageBreakdown(
    int Prompt,
    int Output,
    int? Reasoning,
    int? Cached,
    int Total);
