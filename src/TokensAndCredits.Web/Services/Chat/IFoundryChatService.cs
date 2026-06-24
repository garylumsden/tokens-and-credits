using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Chat;

/// <summary>Result of a single chat completion: output text, token usage, why it stopped, call latency, and logprobs.</summary>
/// <param name="Output">Visible answer text returned by the model.</param>
/// <param name="Usage">Token usage reported by the model response.</param>
/// <param name="FinishReason">Provider finish reason, when available.</param>
/// <param name="LatencyMs">Elapsed wall-clock time for the model response call, in milliseconds.</param>
/// <param name="TtftMs">Elapsed wall-clock time to first streamed text token, in milliseconds.</param>
/// <param name="Logprobs">Per-output-token log probability details when the selected model supports them.</param>
/// <param name="LogprobStats">Aggregate confidence metrics when log probabilities are available.</param>
public sealed record ChatResult(
    string Output,
    UsageBreakdown Usage,
    string? FinishReason,
    long LatencyMs,
    long? TtftMs,
    IReadOnlyList<TokenLogprob>? Logprobs,
    LogprobStats? LogprobStats);

/// <summary>A streamed chat update containing either a text delta or the final result.</summary>
/// <param name="Delta">Incremental text emitted by the model.</param>
/// <param name="Final">Final reconciled chat result after the stream completes.</param>
public sealed record ChatStreamUpdate(string? Delta, ChatResult? Final);

/// <summary>Sends a prompt to a model and returns its output and token usage.</summary>
public interface IFoundryChatService
{
    Task<ChatResult> CompleteAsync(string prompt, ModelDescriptor model, int? maxOutputTokens, CancellationToken ct);

    IAsyncEnumerable<ChatStreamUpdate> StreamAsync(string prompt, ModelDescriptor model, int? maxOutputTokens, CancellationToken ct);
}
