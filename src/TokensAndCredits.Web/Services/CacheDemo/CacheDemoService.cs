using System.Text;
using TokensAndCredits.Web.Services.Chat;
using TokensAndCredits.Web.Services.Models;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Services.CacheDemo;

/// <summary>Result of the prompt-cache demonstration (two identical calls).</summary>
/// <param name="FirstUsage">Usage from the first (cache-priming) call.</param>
/// <param name="SecondUsage">Usage from the second call, which should report cached tokens.</param>
/// <param name="CachedTokens">cached_tokens reported on the second call.</param>
/// <param name="PrefixTokenCount">Local token count of the shared prefix.</param>
public sealed record CacheDemoResult(
    UsageBreakdown FirstUsage,
    UsageBreakdown SecondUsage,
    int CachedTokens,
    int PrefixTokenCount);

/// <summary>
/// Demonstrates prompt caching: Azure OpenAI caches an identical leading prefix of
/// at least 1,024 tokens, so the second of two identical requests reports cached_tokens.
/// </summary>
public sealed class CacheDemoService
{
    // Aim comfortably above the 1,024-token minimum so caching is guaranteed to engage.
    private const int TargetPrefixTokens = 1_200;
    private const string FillerSentence =
        "Prompt caching reuses the computation of identical leading tokens to reduce latency and cost. ";

    private readonly IFoundryChatService _chat;
    private readonly ITokenizerResolver _resolver;
    private readonly TokenAnalyzer _analyzer;

    public CacheDemoService(IFoundryChatService chat, ITokenizerResolver resolver, TokenAnalyzer analyzer)
    {
        _chat = chat;
        _resolver = resolver;
        _analyzer = analyzer;
    }

    public async Task<CacheDemoResult> RunAsync(ModelDescriptor model, CancellationToken ct)
    {
        if (!model.SupportsCaching)
        {
            throw new InvalidOperationException($"Model '{model.Id}' does not support prompt caching.");
        }

        var (prefix, prefixTokens) = BuildPrefix(model);
        var prompt = prefix + "\n\nSummarize the text above in one short sentence.";

        // Two identical requests: the second should hit the prompt cache.
        var first = await _chat.CompleteAsync(prompt, model, 64, ct);
        var second = await _chat.CompleteAsync(prompt, model, 64, ct);

        return new CacheDemoResult(first.Usage, second.Usage, second.Usage.Cached ?? 0, prefixTokens);
    }

    private (string Prefix, int TokenCount) BuildPrefix(ModelDescriptor model)
    {
        var tokenizer = _resolver.Resolve(model).Tokenizer;
        var builder = new StringBuilder();
        var count = 0;

        while (count < TargetPrefixTokens)
        {
            builder.Append(FillerSentence);
            count = _analyzer.Analyze(tokenizer, builder.ToString()).Count;
        }

        return (builder.ToString(), count);
    }
}
