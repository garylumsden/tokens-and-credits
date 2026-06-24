using Microsoft.Extensions.AI;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Chat;

/// <summary>
/// Maps the SDK <see cref="UsageDetails"/> to our UI-facing <see cref="UsageBreakdown"/>.
/// Reasoning/Cached are null when the selected model doesn't support them (shown as N/A),
/// so a returned 0 means "supported and zero" rather than "not available".
/// </summary>
public sealed class UsageExtractor
{
    public UsageBreakdown Extract(UsageDetails? usage, ModelDescriptor model)
    {
        var prompt = (int)(usage?.InputTokenCount ?? 0);
        var completion = (int)(usage?.OutputTokenCount ?? 0);
        var total = (int)(usage?.TotalTokenCount ?? prompt + completion);

        int? reasoning = model.SupportsReasoning ? (int)(usage?.ReasoningTokenCount ?? 0) : null;
        int? cached = model.SupportsCaching ? (int)(usage?.CachedInputTokenCount ?? 0) : null;

        // completion_tokens already includes reasoning_tokens; show the *visible* answer
        // tokens so Prompt + Reasoning + Output reconciles to Total (and a 0 here explains
        // an empty answer when a reasoning model spent its whole budget thinking).
        var output = reasoning is int r ? Math.Max(0, completion - r) : completion;

        return new UsageBreakdown(prompt, output, reasoning, cached, total);
    }
}
