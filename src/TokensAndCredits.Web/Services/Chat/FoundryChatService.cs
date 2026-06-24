using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TokensAndCredits.Web.Services.Models;
using OpenAIChatCompletion = OpenAI.Chat.ChatCompletion;
using OpenAIChatCompletionOptions = OpenAI.Chat.ChatCompletionOptions;
using OpenAIChatTokenLogProbabilityDetails = OpenAI.Chat.ChatTokenLogProbabilityDetails;
using OpenAIStreamingChatCompletionUpdate = OpenAI.Chat.StreamingChatCompletionUpdate;

namespace TokensAndCredits.Web.Services.Chat;

/// <summary>Sends a single-turn prompt through the unified IChatClient and reports usage.</summary>
public sealed class FoundryChatService : IFoundryChatService
{
    private readonly IChatClientFactory _factory;
    private readonly UsageExtractor _usage;

    public FoundryChatService(IChatClientFactory factory, UsageExtractor usage)
    {
        _factory = factory;
        _usage = usage;
    }

    public async Task<ChatResult> CompleteAsync(string prompt, ModelDescriptor model, int? maxOutputTokens, CancellationToken ct)
    {
        var client = await _factory.CreateAsync(model, ct);

        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var options = CreateOptions(model, maxOutputTokens);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetResponseAsync(messages, options, ct);
        stopwatch.Stop();

        var logprobResult = model.SupportsLogprobs
            ? ExtractLogprobs(response.RawRepresentation)
            : null;

        return new ChatResult(
            response.Text,
            _usage.Extract(response.Usage, model),
            response.FinishReason?.ToString(),
            stopwatch.ElapsedMilliseconds,
            null,
            logprobResult?.Tokens,
            logprobResult?.Stats);
    }

    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        string prompt,
        ModelDescriptor model,
        int? maxOutputTokens,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var client = await _factory.CreateAsync(model, ct);
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var options = CreateOptions(model, maxOutputTokens);
        var updates = new List<ChatResponseUpdate>();
        long? ttftMs = null;

        var stopwatch = Stopwatch.StartNew();
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct).WithCancellation(ct))
        {
            updates.Add(update);

            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            ttftMs ??= stopwatch.ElapsedMilliseconds;
            yield return new ChatStreamUpdate(delta, null);
        }

        stopwatch.Stop();
        var response = updates.ToChatResponse();

        var logprobResult = model.SupportsLogprobs
            ? ExtractLogprobs(response.RawRepresentation) ?? ExtractLogprobs(updates.Select(static update => update.RawRepresentation))
            : null;

        // Use the final per-chunk usage, not response.Usage: ToChatResponse() sums usage across
        // updates, and local servers that report usage in every chunk would inflate it hugely.
        var usage = StreamingUsage.Final(updates) ?? response.Usage;

        yield return new ChatStreamUpdate(
            null,
            new ChatResult(
                response.Text,
                _usage.Extract(usage, model),
                response.FinishReason?.ToString(),
                stopwatch.ElapsedMilliseconds,
                ttftMs,
                logprobResult?.Tokens,
                logprobResult?.Stats));
    }

    private static ChatOptions CreateOptions(ModelDescriptor model, int? maxOutputTokens)
    {
        var options = new ChatOptions();
        if (maxOutputTokens is > 0)
        {
            options.MaxOutputTokens = maxOutputTokens;
        }

        if (model.SupportsLogprobs)
        {
            options.RawRepresentationFactory = _ => new OpenAIChatCompletionOptions
            {
                IncludeLogProbabilities = true,
                TopLogProbabilityCount = 5,
                MaxOutputTokenCount = maxOutputTokens is > 0 ? maxOutputTokens : null
            };
        }

        return options;
    }

    private static LogprobResult? ExtractLogprobs(object? rawRepresentation)
    {
        if (rawRepresentation is not OpenAIChatCompletion completion ||
            completion.ContentTokenLogProbabilities is not { Count: > 0 } details)
        {
            return rawRepresentation is OpenAIStreamingChatCompletionUpdate update &&
                update.ContentTokenLogProbabilities is { Count: > 0 } streamingDetails
                ? BuildLogprobResult(streamingDetails)
                : null;
        }

        return BuildLogprobResult(details);
    }

    private static LogprobResult? ExtractLogprobs(IEnumerable<object?> rawRepresentations)
    {
        var details = rawRepresentations
            .OfType<OpenAIStreamingChatCompletionUpdate>()
            .SelectMany(static update => update.ContentTokenLogProbabilities ?? Array.Empty<OpenAIChatTokenLogProbabilityDetails>())
            .ToArray();

        return details.Length > 0 ? BuildLogprobResult(details) : null;
    }

    private static LogprobResult? BuildLogprobResult(IReadOnlyCollection<OpenAIChatTokenLogProbabilityDetails> details)
    {
        var tokens = new List<TokenLogprob>(details.Count);
        var logProbabilitySum = 0.0;
        var confidenceSum = 0.0;
        var reliableCount = 0;

        foreach (var detail in details)
        {
            var probability = ToProbability(detail.LogProbability);
            var top = detail.TopLogProbabilities?
                .Select(topDetail => new TokenAlternativeLogprob(
                    topDetail.Token,
                    ToProbability(topDetail.LogProbability),
                    ToByteList(topDetail.Utf8Bytes)))
                .ToArray() ?? Array.Empty<TokenAlternativeLogprob>();

            tokens.Add(new TokenLogprob(detail.Token, probability, ToByteList(detail.Utf8Bytes), top));

            // Only fold reliable values into the aggregate stats. Some long streamed responses
            // report a garbage (implausibly negative) log-prob for the chosen token, which would
            // otherwise drive perplexity to infinity.
            if (probability is double p)
            {
                logProbabilitySum += detail.LogProbability;
                confidenceSum += p;
                reliableCount++;
            }
        }

        if (tokens.Count == 0)
        {
            return null;
        }

        if (reliableCount == 0)
        {
            // Tokens are still useful for the heatmap, but no trustworthy aggregate is possible.
            return new LogprobResult(tokens, null);
        }

        var meanLogProbability = logProbabilitySum / reliableCount;
        var stats = new LogprobStats(
            Perplexity: ToPerplexity(meanLogProbability),
            AverageConfidence: confidenceSum / reliableCount);

        return new LogprobResult(tokens, stats);
    }

    // A real chosen/alternative token never has a log-prob this low; values below the floor (or
    // NaN/infinite) indicate a corrupt provider value, so the probability is reported as unknown.
    private const float MinReliableLogProbability = -100f;

    private static double? ToProbability(float logProbability)
    {
        if (float.IsNaN(logProbability) || float.IsInfinity(logProbability) || logProbability < MinReliableLogProbability)
        {
            return null;
        }

        return Math.Clamp(Math.Exp(logProbability), 0, 1);
    }

    private static IReadOnlyList<int> ToByteList(ReadOnlyMemory<byte>? utf8Bytes)
    {
        if (utf8Bytes is not { IsEmpty: false } bytesMemory)
        {
            return Array.Empty<int>();
        }

        var span = bytesMemory.Span;
        var bytes = new int[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            bytes[i] = span[i];
        }

        return bytes;
    }

    private static double ToPerplexity(double meanLogProbability)
    {
        var exponent = -meanLogProbability;
        if (double.IsNaN(exponent))
        {
            return 0;
        }

        if (double.IsPositiveInfinity(exponent) || exponent >= Math.Log(double.MaxValue))
        {
            return double.MaxValue;
        }

        return Math.Exp(exponent);
    }

    private sealed record LogprobResult(IReadOnlyList<TokenLogprob> Tokens, LogprobStats? Stats);
}
