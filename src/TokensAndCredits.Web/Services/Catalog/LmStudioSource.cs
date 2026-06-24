using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>Discovers models from a locally running LM Studio OpenAI-compatible server.</summary>
public sealed class LmStudioSource : OpenAiCompatibleLocalSource
{
    /// <summary>Named HTTP client used for LM Studio discovery.</summary>
    public const string HttpClientName = "local-discovery-lm-studio";

    /// <summary>Default LM Studio OpenAI-compatible base endpoint.</summary>
    public const string BaseEndpoint = "http://localhost:1234/v1";

    /// <summary>Creates a source for LM Studio local model discovery.</summary>
    /// <param name="httpClientFactory">Factory used to create the named discovery HTTP client.</param>
    /// <param name="logger">Logger for debug-only discovery failures.</param>
    public LmStudioSource(IHttpClientFactory httpClientFactory, ILogger<LmStudioSource> logger)
        : base(httpClientFactory, logger, HttpClientName, ModelSource.LmStudio)
    {
    }
}
