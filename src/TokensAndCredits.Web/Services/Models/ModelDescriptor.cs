namespace TokensAndCredits.Web.Services.Models;

/// <summary>Where a model is hosted and called from.</summary>
public enum ModelSource
{
    /// <summary>Azure AI Foundry (cloud) deployment.</summary>
    AzureFoundry,

    /// <summary>Foundry Local on-device model (e.g. NPU).</summary>
    FoundryLocal,

    /// <summary>LM Studio local OpenAI-compatible model.</summary>
    LmStudio,

    /// <summary>Ollama local OpenAI-compatible model.</summary>
    Ollama
}

/// <summary>What kind of output a model produces (drives which UI panels apply).</summary>
public enum Modality
{
    /// <summary>Text completion model (default).</summary>
    Text,

    /// <summary>Image generation model (e.g. gpt-image-1.5); output billed in image tokens.</summary>
    Image
}

/// <summary>
/// A selectable model plus the capabilities the UI needs to decide what to show.
/// </summary>
/// <param name="Id">Identifier used for routing and tokenizer resolution (Azure deployment name or Foundry Local model id).</param>
/// <param name="Label">Human-friendly name for the selector.</param>
/// <param name="Source">Cloud Azure Foundry vs local OpenAI-compatible sources.</param>
/// <param name="Device">Execution device shown as a badge (e.g. "cloud", "NPU").</param>
/// <param name="Encoding">Tokenizer encoding label (e.g. "o200k_base", "qwen2-bpe").</param>
/// <param name="SupportsReasoning">True if the model reports reasoning_tokens (o-series).</param>
/// <param name="SupportsCaching">True if the model reports cached_tokens (GPT-4o+).</param>
/// <param name="Exact">True if local tokenization is byte-exact for this model; false if approximate.</param>
/// <param name="SupportsLogprobs">True if the model returns per-token logprobs (GPT-4o/4.1; not o-series or local).</param>
/// <param name="Modality">Text completion vs image generation output.</param>
public sealed record ModelDescriptor(
    string Id,
    string Label,
    ModelSource Source,
    string Device,
    string Encoding,
    bool SupportsReasoning,
    bool SupportsCaching,
    bool Exact,
    bool SupportsLogprobs = false,
    Modality Modality = Modality.Text);
