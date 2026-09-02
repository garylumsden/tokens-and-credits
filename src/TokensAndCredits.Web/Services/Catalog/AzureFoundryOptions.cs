using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>Configuration for the Azure AI Foundry connection and its deployments.</summary>
public sealed class AzureFoundryOptions
{
    public const string SectionName = "AzureFoundry";

    /// <summary>Base endpoint, e.g. https://your-resource.openai.azure.com/openai/v1 (set by azd output).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Microsoft Entra tenant used by local developer credentials.</summary>
    public string? TenantId { get; set; }

    /// <summary>Configured model deployments to expose in the selector.</summary>
    public List<AzureDeploymentOptions> Deployments { get; set; } = new();

    /// <summary>Optional embedding deployment used by the live embedding comparison.</summary>
    public string? EmbeddingDeployment { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && Deployments.Count > 0;

    public bool IsEmbeddingConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(EmbeddingDeployment);
}

/// <summary>A single Azure deployment plus the capabilities the UI needs.</summary>
public sealed class AzureDeploymentOptions
{
    /// <summary>Deployment name as created in Foundry (used as the model id for the call).</summary>
    public string Name { get; set; } = "";

    /// <summary>Friendly label for the selector; defaults to Name.</summary>
    public string? Label { get; set; }

    /// <summary>tiktoken encoding (o200k_base for GPT-5 and o-series; cl100k_base for older models).</summary>
    public string Encoding { get; set; } = "o200k_base";

    /// <summary>True for GPT-5 or o-series models that report reasoning_tokens.</summary>
    public bool SupportsReasoning { get; set; }

    /// <summary>True for models that report cached_tokens.</summary>
    public bool SupportsCaching { get; set; } = true;

    /// <summary>True for models that return per-token logprobs. Reasoning models do not support them.</summary>
    public bool SupportsLogprobs { get; set; }

    /// <summary>Text completion (default) or image generation output.</summary>
    public Modality Modality { get; set; } = Modality.Text;
}
