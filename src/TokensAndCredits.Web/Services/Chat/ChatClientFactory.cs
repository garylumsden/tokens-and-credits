using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using TokensAndCredits.Web.Services.Catalog;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Chat;

/// <summary>
/// Builds an <see cref="IChatClient"/> for Azure Foundry deployments or local
/// OpenAI-compatible models. All sources go through the official OpenAI ChatClient so
/// usage is reported uniformly. Foundry Local models are loaded before first use.
/// </summary>
public sealed class ChatClientFactory : IChatClientFactory
{
    // The Azure OpenAI v1 API is version-agnostic (no api-version juggling) and supports
    // newer models such as the o-series. Token scope for the AI Services data plane.
    private const string AzureScope = "https://cognitiveservices.azure.com/.default";

    private readonly AzureFoundryOptions _azureOptions;
    private readonly FoundryLocalSource _local;
    private readonly ConcurrentDictionary<string, IChatClient> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly DefaultAzureCredential _credential = new();

    public ChatClientFactory(IOptions<AzureFoundryOptions> azureOptions, FoundryLocalSource local)
    {
        _azureOptions = azureOptions.Value;
        _local = local;
    }

    public async Task<IChatClient> CreateAsync(ModelDescriptor model, CancellationToken ct)
    {
        return model.Source switch
        {
            ModelSource.AzureFoundry => CreateAzure(model),
            ModelSource.FoundryLocal => await CreateLocalAsync(model, ct),
            ModelSource.LmStudio => CreateOpenAiCompatibleLocal(model, "lmstudio", LmStudioSource.BaseEndpoint),
            ModelSource.Ollama => CreateOpenAiCompatibleLocal(model, "ollama", OllamaSource.BaseEndpoint),
            _ => throw new NotSupportedException($"Unknown model source '{model.Source}'.")
        };
    }

    private IChatClient CreateAzure(ModelDescriptor model)
    {
        return _cache.GetOrAdd($"azure:{model.Id}", _ =>
        {
            if (string.IsNullOrWhiteSpace(_azureOptions.Endpoint))
            {
                throw new InvalidOperationException("Azure Foundry endpoint is not configured.");
            }

            var baseEndpoint = _azureOptions.Endpoint.TrimEnd('/');
            var options = new OpenAIClientOptions { Endpoint = new Uri($"{baseEndpoint}/openai/v1") };

#pragma warning disable OPENAI001 // Entra token auth on the OpenAI client is an evaluation feature.
            var authPolicy = new BearerTokenPolicy(_credential, AzureScope);
            var chatClient = new ChatClient(model.Id, authPolicy, options);
#pragma warning restore OPENAI001

            return chatClient.AsIChatClient();
        });
    }

    private async Task<IChatClient> CreateLocalAsync(ModelDescriptor model, CancellationToken ct)
    {
        await _local.EnsureLoadedAsync(model.Id, ct);

        var endpoint = await _local.GetEndpointAsync(ct)
            ?? throw new InvalidOperationException("Foundry Local endpoint is not available.");

        return _cache.GetOrAdd($"local:{model.Id}:{endpoint}", _ =>
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri($"{endpoint.TrimEnd('/')}/v1") };
            // Local endpoint ignores the key but the client requires a non-empty credential.
            var client = new OpenAIClient(new ApiKeyCredential("not-needed"), options);
            return client.GetChatClient(model.Id).AsIChatClient();
        });
    }

    private IChatClient CreateOpenAiCompatibleLocal(ModelDescriptor model, string cachePrefix, string endpoint)
    {
        return _cache.GetOrAdd($"{cachePrefix}:{model.Id}:{endpoint}", _ =>
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint.TrimEnd('/')) };
            // Local endpoints ignore the key but the client requires a non-empty credential.
            var client = new OpenAIClient(new ApiKeyCredential("not-needed"), options);
            return client.GetChatClient(model.Id).AsIChatClient();
        });
    }
}
