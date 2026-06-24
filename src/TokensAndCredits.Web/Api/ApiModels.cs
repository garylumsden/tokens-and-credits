using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Api;

internal static class ApiLimits
{
    /// <summary>Max characters accepted for interactive prompt/tokenize input.</summary>
    public const int MaxPromptChars = 20_000;

    /// <summary>Server-side ceiling for requested model output tokens (mirrors the UI's max).</summary>
    public const int MaxOutputTokensCap = 32_768;
}

// ----- Requests -----

/// <summary>Local-only tokenization request (no model call).</summary>
public sealed record TokenizeRequest(string Text, string ModelId);

/// <summary>Prompt analysis request (local tokenization + live model call).</summary>
public sealed record AnalyzeRequest(string Prompt, string ModelId, int? MaxOutputTokens);

/// <summary>Lazy per-token explanation request.</summary>
public sealed record ExplainTokenRequest(string ModelId, string Text, int TokenIndex);

/// <summary>Prompt-cache demonstration request.</summary>
public sealed record CacheDemoRequest(string ModelId);

/// <summary>Request to reconstruct a real BPE merge trace for one example word.</summary>
public sealed record MergeTraceRequest(string ModelId, string? Word);

/// <summary>Request to reconstruct per-word BPE merge traces for a short example phrase.</summary>
public sealed record MergePhraseRequest(string ModelId, string? Phrase);

/// <summary>Image generation request (local prompt tokenization + live image model call).</summary>
public sealed record GenerateImageRequest(string ModelId, string Prompt, string Size, string Quality);

// ----- Responses -----

/// <summary>Tokenization result computed locally (origin = "local").</summary>
public sealed record TokenizeResponse(
    string Origin,
    IReadOnlyList<TokenInfo> Tokens,
    int Count,
    string Encoding,
    bool Exact,
    string Source);

/// <summary>Everything determined locally for /analyze (origin = "local").</summary>
public sealed record LocalAnalysis(
    string Origin,
    IReadOnlyList<TokenInfo> PromptTokens,
    int PromptTokenCount,
    IReadOnlyList<TokenInfo> OutputTokens,
    string Encoding,
    bool Exact);

/// <summary>Everything returned by the live model for /analyze (origin = "model").</summary>
public sealed record ModelAnalysis(
    string Origin,
    string Output,
    UsageBreakdown Usage,
    bool SupportsReasoning,
    bool SupportsCaching,
    bool SupportsLogprobs,
    string? FinishReason,
    long LatencyMs,
    long? TtftMs,
    IReadOnlyList<TokenLogprob>? Logprobs,
    LogprobStats? LogprobStats);

/// <summary>Combined response keeping local and model data in separate, labeled blocks.</summary>
public sealed record AnalyzeResponse(LocalAnalysis Local, ModelAnalysis Model);

/// <summary>Everything determined locally for /generate-image (origin = "local").</summary>
public sealed record ImageLocalAnalysis(
    string Origin,
    IReadOnlyList<TokenInfo> PromptTokens,
    int PromptTokenCount,
    string Encoding,
    bool Exact);

/// <summary>Combined image response. Usage null properties mean the service did not report that field.</summary>
public sealed record GenerateImageResponse(
    string ImageBase64,
    string ImageMediaType,
    ImageLocalAnalysis Local,
    ImageUsageBreakdown Usage);
