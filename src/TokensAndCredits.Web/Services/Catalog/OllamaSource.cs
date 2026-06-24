using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>Discovers models from a locally running Ollama OpenAI-compatible server.</summary>
public sealed class OllamaSource : OpenAiCompatibleLocalSource
{
    /// <summary>Named HTTP client used for Ollama discovery.</summary>
    public const string HttpClientName = "local-discovery-ollama";

    /// <summary>Default Ollama OpenAI-compatible base endpoint.</summary>
    public const string BaseEndpoint = "http://localhost:11434/v1";

    /// <summary>Creates a source for Ollama local model discovery.</summary>
    /// <param name="httpClientFactory">Factory used to create the named discovery HTTP client.</param>
    /// <param name="logger">Logger for debug-only discovery failures.</param>
    public OllamaSource(IHttpClientFactory httpClientFactory, ILogger<OllamaSource> logger)
        : base(httpClientFactory, logger, HttpClientName, ModelSource.Ollama)
    {
    }
}
