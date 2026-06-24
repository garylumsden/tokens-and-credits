namespace TokensAndCredits.Web.Services.Models;

/// <summary>Alternative token considered by the model for one output position.</summary>
/// <param name="Token">Token text returned by the provider (may be an escaped byte fragment).</param>
/// <param name="Prob">Probability derived from the provider log probability (0..1), or null if the provider value was unreliable.</param>
/// <param name="Bytes">Raw UTF-8 bytes of the token, used to reassemble multi-token characters (e.g. emoji).</param>
public sealed record TokenAlternativeLogprob(string Token, double? Prob, IReadOnlyList<int> Bytes);

/// <summary>Log probability details for one output token.</summary>
/// <param name="Token">Chosen output token text (may be an escaped byte fragment).</param>
/// <param name="Prob">Chosen token probability derived from the provider log probability (0..1), or null if the provider value was unreliable (seen on some long streamed responses).</param>
/// <param name="Bytes">Raw UTF-8 bytes of the token, used to reassemble multi-token characters (e.g. emoji).</param>
/// <param name="Top">Top alternatives returned by the provider for this token position.</param>
public sealed record TokenLogprob(string Token, double? Prob, IReadOnlyList<int> Bytes, IReadOnlyList<TokenAlternativeLogprob> Top);

/// <summary>Aggregate confidence metrics computed from output token log probabilities.</summary>
/// <param name="Perplexity">Perplexity computed as exp(-mean(logprob)).</param>
/// <param name="AverageConfidence">Mean chosen-token probability across output tokens, in the range 0..1.</param>
public sealed record LogprobStats(double Perplexity, double AverageConfidence);
