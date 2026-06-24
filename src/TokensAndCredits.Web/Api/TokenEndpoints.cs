using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TokensAndCredits.Web.Services.CacheDemo;
using TokensAndCredits.Web.Services.Catalog;
using TokensAndCredits.Web.Services.Chat;
using TokensAndCredits.Web.Services.Credits;
using TokensAndCredits.Web.Services.Image;
using TokensAndCredits.Web.Services.Models;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Api;

/// <summary>Maps the token/usage demo HTTP API. Local and model data are kept separate.</summary>
public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // All selectable models (Azure deployments + Foundry Local), with capability flags.
        group.MapGet("/models", async (IModelCatalog catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetModelsAsync(ct)));

        // LOCAL ONLY: tokenize text with no model call (powers live highlight, no cost).
        group.MapPost("/tokenize", async (
            TokenizeRequest req,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            TokenAnalyzer analyzer,
            CancellationToken ct) =>
        {
            if (req.Text is null)
            {
                return Results.BadRequest("Text is required.");
            }

            if (req.Text.Length > ApiLimits.MaxPromptChars)
            {
                return Results.BadRequest($"Text exceeds {ApiLimits.MaxPromptChars} characters.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            var resolved = resolver.Resolve(model);
            var tokens = analyzer.Analyze(resolved.Tokenizer, req.Text);
            return Results.Ok(new TokenizeResponse(
                "local", tokens, tokens.Count, resolved.Encoding, resolved.Exact, model.Source.ToString()));
        });


        // LOCAL ONLY: explain one token lazily so long prompts don't carry a large explanation payload.
        group.MapPost("/explain-token", async (
            ExplainTokenRequest req,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            TokenAnalyzer analyzer,
            TokenExplainer explainer,
            CancellationToken ct) =>
        {
            if (req.Text is null)
            {
                return Results.BadRequest("Text is required.");
            }

            if (req.Text.Length > ApiLimits.MaxPromptChars)
            {
                return Results.BadRequest($"Text exceeds {ApiLimits.MaxPromptChars} characters.");
            }

            if (req.TokenIndex < 0)
            {
                return Results.BadRequest("TokenIndex must be zero or greater.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            var resolved = resolver.Resolve(model);
            var tokens = analyzer.Analyze(resolved.Tokenizer, req.Text);
            var token = tokens.FirstOrDefault(t => t.Index == req.TokenIndex);
            if (token is null)
            {
                return Results.NotFound($"Token index {req.TokenIndex} was not found in the supplied text.");
            }

            return Results.Ok(explainer.Explain(model, resolved, req.Text, token));
        });

        // LOCAL ONLY: reconstruct the real greedy BPE merge sequence for one example word so the
        // UI can animate exactly how the selected model's tokenizer builds tokens.
        group.MapPost("/merge-trace", async (
            MergeTraceRequest req,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            MergeTracer tracer,
            CancellationToken ct) =>
        {
            var word = string.IsNullOrWhiteSpace(req.Word) ? "lowest" : req.Word.Trim();
            if (word.Length is 0 or > 40 || word.Any(char.IsWhiteSpace))
            {
                return Results.BadRequest("Word must be a single token-free word of 1-40 characters.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            var resolved = resolver.Resolve(model);
            var trace = tracer.Trace(resolved.Tokenizer, word);
            return Results.Ok(new
            {
                origin = "local",
                trace.Word,
                trace.Steps,
                trace.FinalTokens,
                trace.RejectedPairs,
                trace.Verified,
                trace.IdIsRank,
                resolved.Encoding,
                resolved.Exact,
            });
        });

        // LOCAL ONLY: per-word merge traces for a short phrase, so the UI can animate how multiple
        // words (and the spaces between them) become several tokens.
        group.MapPost("/merge-trace-phrase", async (
            MergePhraseRequest req,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            MergeTracer tracer,
            CancellationToken ct) =>
        {
            var phrase = string.IsNullOrWhiteSpace(req.Phrase) ? "I love tokenization" : req.Phrase.Trim();
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is < 2 or > 6 || words.Any(w => w.Length > 40) || !phrase.All(c => char.IsLetter(c) || c == ' '))
            {
                return Results.BadRequest("Phrase must be 2-6 letter-only words separated by single spaces.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            var resolved = resolver.Resolve(model);
            var traces = tracer.TracePhrase(resolved.Tokenizer, phrase);
            return Results.Ok(new
            {
                origin = "local",
                phrase,
                words = traces,
                tokenCount = traces.Sum(t => t.FinalTokens.Count),
                verified = traces.All(t => t.Verified),
                resolved.Encoding,
                resolved.Exact,
            });
        });

        // Local tokenization + live model call, returned as separate provenance blocks.
        group.MapPost("/analyze", async (
            AnalyzeRequest req,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            TokenAnalyzer analyzer,
            IFoundryChatService chat,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
            {
                return Results.BadRequest("Prompt is required.");
            }

            if (req.Prompt.Length > ApiLimits.MaxPromptChars)
            {
                return Results.BadRequest($"Prompt exceeds {ApiLimits.MaxPromptChars} characters.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            if (model.Modality != Modality.Text)
            {
                return Results.Conflict($"Model '{model.Id}' is an image model. Use /api/generate-image.");
            }

            if (req.MaxOutputTokens is int max && (max < 1 || max > ApiLimits.MaxOutputTokensCap))
            {
                return Results.BadRequest($"MaxOutputTokens must be between 1 and {ApiLimits.MaxOutputTokensCap}.");
            }

            var resolved = resolver.Resolve(model);
            var promptTokens = analyzer.Analyze(resolved.Tokenizer, req.Prompt);

            try
            {
                var result = await chat.CompleteAsync(req.Prompt, model, req.MaxOutputTokens, ct);
                var outputTokens = analyzer.Analyze(resolved.Tokenizer, result.Output);

                var local = new LocalAnalysis(
                    "local", promptTokens, promptTokens.Count, outputTokens, resolved.Encoding, resolved.Exact);
                var modelBlock = new ModelAnalysis(
                    "model",
                    result.Output,
                    result.Usage,
                    model.SupportsReasoning,
                    model.SupportsCaching,
                    model.SupportsLogprobs,
                    result.FinishReason,
                    result.LatencyMs,
                    result.TtftMs,
                    result.Logprobs,
                    result.LogprobStats);

                return Results.Ok(new AnalyzeResponse(local, modelBlock));
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Analyze").LogError(ex, "Model call failed for {ModelId}.", model.Id);
                return Results.Problem(title: "Model call failed", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapPost("/analyze-stream", async (
            AnalyzeRequest req,
            HttpContext context,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            TokenAnalyzer analyzer,
            IFoundryChatService chat,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
            {
                return Results.BadRequest("Prompt is required.");
            }

            if (req.Prompt.Length > ApiLimits.MaxPromptChars)
            {
                return Results.BadRequest($"Prompt exceeds {ApiLimits.MaxPromptChars} characters.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            if (model.Modality != Modality.Text)
            {
                return Results.Conflict($"Model '{model.Id}' is an image model. Use /api/generate-image.");
            }

            if (req.MaxOutputTokens is int max && (max < 1 || max > ApiLimits.MaxOutputTokensCap))
            {
                return Results.BadRequest($"MaxOutputTokens must be between 1 and {ApiLimits.MaxOutputTokensCap}.");
            }

            var resolved = resolver.Resolve(model);
            var promptTokens = analyzer.Analyze(resolved.Tokenizer, req.Prompt);
            var initialLocal = new LocalAnalysis(
                "local", promptTokens, promptTokens.Count, Array.Empty<TokenInfo>(), resolved.Encoding, resolved.Exact);

            var response = context.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache, no-transform";

            try
            {
                await WriteSseAsync(response, "meta", new { local = initialLocal }, ct);

                await foreach (var update in chat.StreamAsync(req.Prompt, model, req.MaxOutputTokens, ct).WithCancellation(ct))
                {
                    if (!string.IsNullOrEmpty(update.Delta))
                    {
                        await WriteSseAsync(response, "delta", new { text = update.Delta }, ct);
                    }

                    if (update.Final is null)
                    {
                        continue;
                    }

                    var outputTokens = analyzer.Analyze(resolved.Tokenizer, update.Final.Output);
                    var local = new LocalAnalysis(
                        "local", promptTokens, promptTokens.Count, outputTokens, resolved.Encoding, resolved.Exact);
                    var modelBlock = new ModelAnalysis(
                        "model",
                        update.Final.Output,
                        update.Final.Usage,
                        model.SupportsReasoning,
                        model.SupportsCaching,
                        model.SupportsLogprobs,
                        update.Final.FinishReason,
                        update.Final.LatencyMs,
                        update.Final.TtftMs,
                        update.Final.Logprobs,
                        update.Final.LogprobStats);

                    await WriteSseAsync(response, "done", new AnalyzeResponse(local, modelBlock), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Client disconnected.
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("AnalyzeStream").LogError(ex, "Streaming model call failed for {ModelId}.", model.Id);
                await WriteSseAsync(response, "error", new { message = ex.Message }, CancellationToken.None);
            }

            return Results.Empty;
        });

        // Local prompt tokenization + live image model call. Image outputs are billed in tokens.
        group.MapPost("/generate-image", async (
            GenerateImageRequest req,
            IModelCatalog catalog,
            ITokenizerResolver resolver,
            TokenAnalyzer analyzer,
            IImageGenerationService images,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
            {
                return Results.BadRequest("Prompt is required.");
            }

            if (req.Prompt.Length > ApiLimits.MaxPromptChars)
            {
                return Results.BadRequest($"Prompt exceeds {ApiLimits.MaxPromptChars} characters.");
            }

            if (!ImageGenerationService.IsSupportedSize(req.Size))
            {
                return Results.BadRequest("Unsupported image size. Use auto, 1024x1024, 1024x1536, or 1536x1024.");
            }

            if (!ImageGenerationService.IsSupportedQuality(req.Quality))
            {
                return Results.BadRequest("Unsupported image quality. Use auto, low, medium, or high.");
            }

            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            if (model.Modality != Modality.Image)
            {
                return Results.Conflict($"Model '{model.Id}' is a text model. Choose an image model.");
            }

            var resolved = resolver.Resolve(model);
            var promptTokens = analyzer.Analyze(resolved.Tokenizer, req.Prompt);
            var local = new ImageLocalAnalysis("local", promptTokens, promptTokens.Count, resolved.Encoding, resolved.Exact);

            try
            {
                var result = await images.GenerateAsync(model, req.Prompt, req.Size, req.Quality, ct);
                return Results.Ok(new GenerateImageResponse(
                    result.ImageBase64,
                    result.ImageMediaType,
                    local,
                    result.Usage));
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("GenerateImage").LogError(ex, "Image generation failed for {ModelId}.", model.Id);
                return Results.Problem(title: "Image generation failed", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        // Prompt-cache demonstration (caching-capable Azure models only).
        group.MapPost("/cache-demo", async (
            CacheDemoRequest req,
            IModelCatalog catalog,
            CacheDemoService demo,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var model = await catalog.FindAsync(req.ModelId, ct);
            if (model is null)
            {
                return Results.NotFound($"Unknown model '{req.ModelId}'.");
            }

            if (!model.SupportsCaching)
            {
                return Results.Conflict($"Model '{model.Id}' does not support prompt caching.");
            }

            try
            {
                var result = await demo.RunAsync(model, ct);
                return Results.Ok(new
                {
                    origin = "model",
                    firstUsage = result.FirstUsage,
                    secondUsage = result.SecondUsage,
                    cachedTokens = result.CachedTokens,
                    prefixTokenCount = result.PrefixTokenCount
                });
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("CacheDemo").LogError(ex, "Cache demo failed for {ModelId}.", model.Id);
                return Results.Problem(title: "Cache demo failed", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        // LOCAL ONLY: AI Credit rate catalogue (Copilot Studio tiers + GitHub model rate card).
        // Drives the billing-model selector; the live credit maths runs client-side from usage.
        group.MapGet("/credit-rates", (IOptions<CreditRatesOptions> options) =>
        {
            var c = options.Value;
            return Results.Ok(new
            {
                asOf = c.AsOf,
                copilotStudio = new
                {
                    basic = c.CopilotStudio.Basic,
                    standard = c.CopilotStudio.Standard,
                    premium = c.CopilotStudio.Premium
                },
                github = new
                {
                    defaultId = c.GitHub.DefaultId,
                    models = c.GitHub.Models.Select(m => new
                    {
                        id = m.Id,
                        label = m.Label,
                        input = m.InputPerMillion,
                        cacheRead = m.CacheReadPerMillion,
                        cacheWrite = m.CacheWritePerMillion,
                        output = m.OutputPerMillion
                    })
                }
            });
        });

        return app;
    }

    private static readonly JsonSerializerOptions SseJsonOptions = CreateSseJsonOptions();

    private static JsonSerializerOptions CreateSseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task WriteSseAsync(HttpResponse response, string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJsonOptions);
        await response.WriteAsync($"event: {eventName}\n", ct);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
