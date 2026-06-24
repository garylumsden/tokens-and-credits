using System.Text.Json;
using System.Text.Json.Serialization;
using TokensAndCredits.Web.Services.Models;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>Base implementation for local OpenAI-compatible model discovery endpoints.</summary>
public abstract class OpenAiCompatibleLocalSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly string _httpClientName;
    private readonly ModelSource _source;

    /// <summary>Creates a local OpenAI-compatible source for a named HTTP client and model source.</summary>
    /// <param name="httpClientFactory">Factory used to create the named discovery HTTP client.</param>
    /// <param name="logger">Logger for debug-only discovery failures.</param>
    /// <param name="httpClientName">Named HTTP client configured with the local OpenAI-compatible base endpoint.</param>
    /// <param name="source">Model source assigned to discovered descriptors.</param>
    protected OpenAiCompatibleLocalSource(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        string httpClientName,
        ModelSource source)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _httpClientName = httpClientName;
        _source = source;
    }

    /// <summary>Lists local models, returning an empty list when the endpoint is unavailable.</summary>
    /// <param name="ct">Cancellation token for the discovery request.</param>
    /// <returns>Discovered model descriptors, or an empty list when discovery fails.</returns>
    public async Task<IReadOnlyList<ModelDescriptor>> ListAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(_httpClientName);
            using var response = await client.GetAsync("models", HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("{Source} model discovery returned HTTP {StatusCode}.", _source, response.StatusCode);
                return Array.Empty<ModelDescriptor>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return OpenAiCompatibleModelParser.Parse(json, _source);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "{Source} model discovery cancelled or timed out.", _source);
            return Array.Empty<ModelDescriptor>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Source} model discovery failed.", _source);
            return Array.Empty<ModelDescriptor>();
        }
    }
}

internal static class OpenAiCompatibleModelParser
{
    internal const string ApproximateEncoding = "o200k_base (approx)";
    internal const string QwenEncoding = "Qwen byte-level BPE";

    internal static IReadOnlyList<ModelDescriptor> Parse(string json, ModelSource source)
    {
        var parsed = JsonSerializer.Deserialize<OpenAiModelsResponse>(json);
        var models = parsed?.Data ?? Array.Empty<OpenAiModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var descriptors = new List<ModelDescriptor>();

        foreach (var model in models)
        {
            var id = model.Id?.Trim();
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            // Qwen models are tokenised exactly with the bundled Qwen BPE; everything else falls
            // back to an approximate o200k_base count (flagged not exact in the UI).
            var isQwen = ModelFamilyDetector.Detect(id) == ModelFamily.Qwen;

            descriptors.Add(new ModelDescriptor(
                Id: id,
                Label: id,
                Source: source,
                Device: "local",
                Encoding: isQwen ? QwenEncoding : ApproximateEncoding,
                SupportsReasoning: false,
                SupportsCaching: false,
                Exact: isQwen));
        }

        return descriptors;
    }

    private sealed record OpenAiModelsResponse([property: JsonPropertyName("data")] OpenAiModel[]? Data);

    private sealed record OpenAiModel([property: JsonPropertyName("id")] string? Id);
}
