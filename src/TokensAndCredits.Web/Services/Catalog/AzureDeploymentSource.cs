using Microsoft.Extensions.Options;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>Produces model descriptors for configured Azure Foundry deployments.</summary>
public sealed class AzureDeploymentSource
{
    private readonly AzureFoundryOptions _options;

    public AzureDeploymentSource(IOptions<AzureFoundryOptions> options) => _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public IReadOnlyList<ModelDescriptor> List()
    {
        if (!_options.IsConfigured)
        {
            return Array.Empty<ModelDescriptor>();
        }

        return _options.Deployments
            .Select(d => new ModelDescriptor(
                Id: d.Name,
                Label: string.IsNullOrWhiteSpace(d.Label) ? d.Name : d.Label!,
                Source: ModelSource.AzureFoundry,
                Device: "cloud",
                Encoding: d.Encoding,
                SupportsReasoning: d.SupportsReasoning,
                SupportsCaching: d.SupportsCaching,
                Exact: true,
                SupportsLogprobs: d.SupportsLogprobs,
                Modality: d.Modality))
            .ToList();
    }
}
