using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>Merged view of all selectable models (Azure Foundry + local sources).</summary>
public interface IModelCatalog
{
    Task<IReadOnlyList<ModelDescriptor>> GetModelsAsync(CancellationToken ct);

    Task<ModelDescriptor?> FindAsync(string id, CancellationToken ct);
}
