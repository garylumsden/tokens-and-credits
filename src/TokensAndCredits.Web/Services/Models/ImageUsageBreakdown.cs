namespace TokensAndCredits.Web.Services.Models;

/// <summary>
/// Token usage reported by image generation. Image outputs are billed as output tokens.
/// Null means the service response did not include that usage field.
/// </summary>
public sealed record ImageUsageBreakdown(
    int? InputTokens,
    int? OutputTokens,
    int? TextTokens,
    int? ImageTokens);
