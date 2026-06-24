using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>
/// Combines configured Azure deployments with discovered local models.
/// The local list is cached briefly because local discovery can be slow.
/// </summary>
public sealed class ModelCatalog : IModelCatalog
{
    private static readonly TimeSpan LocalCacheTtl = TimeSpan.FromSeconds(30);

    private readonly AzureDeploymentSource _azure;
    private readonly FoundryLocalSource _foundryLocal;
    private readonly LmStudioSource _lmStudio;
    private readonly OllamaSource _ollama;
    private readonly ILogger<ModelCatalog> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ModelDescriptor> _localCache = Array.Empty<ModelDescriptor>();
    private DateTimeOffset _localCachedAt = DateTimeOffset.MinValue;

    public ModelCatalog(
        AzureDeploymentSource azure,
        FoundryLocalSource foundryLocal,
        LmStudioSource lmStudio,
        OllamaSource ollama,
        ILogger<ModelCatalog> logger)
    {
        _azure = azure;
        _foundryLocal = foundryLocal;
        _lmStudio = lmStudio;
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModelDescriptor>> GetModelsAsync(CancellationToken ct)
    {
        var local = await GetLocalAsync(ct);
        return _azure.List().Concat(local).ToList();
    }

    public async Task<ModelDescriptor?> FindAsync(string id, CancellationToken ct)
    {
        var all = await GetModelsAsync(ct);
        return all.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<ModelDescriptor>> GetLocalAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _localCachedAt < LocalCacheTtl)
        {
            return _localCache;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - _localCachedAt < LocalCacheTtl)
            {
                return _localCache;
            }

            var localModels = await Task.WhenAll(
                ListQuietlyAsync("Foundry Local", _foundryLocal.ListAsync, ct),
                ListQuietlyAsync("LM Studio", _lmStudio.ListAsync, ct),
                ListQuietlyAsync("Ollama", _ollama.ListAsync, ct));

            _localCache = localModels.SelectMany(m => m).ToList();
            _localCachedAt = DateTimeOffset.UtcNow;
            return _localCache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ModelDescriptor>> ListQuietlyAsync(
        string sourceName,
        Func<CancellationToken, Task<IReadOnlyList<ModelDescriptor>>> listAsync,
        CancellationToken ct)
    {
        try
        {
            return await listAsync(ct);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "{SourceName} discovery cancelled.", sourceName);
            return Array.Empty<ModelDescriptor>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{SourceName} discovery failed.", sourceName);
            return Array.Empty<ModelDescriptor>();
        }
    }
}
