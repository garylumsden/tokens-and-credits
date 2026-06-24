namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>Tokenizer family inferred from a model id, used to pick an exact local tokenizer.</summary>
public enum ModelFamily
{
    /// <summary>No bundled exact tokenizer; fall back to an approximate encoding.</summary>
    Unknown,

    /// <summary>Qwen 2 / 2.5 / 3 family, which share one byte-level BPE tokenizer.</summary>
    Qwen
}

/// <summary>Infers the tokenizer family from a model id so locally-hosted models can be tokenised exactly.</summary>
public static class ModelFamilyDetector
{
    /// <summary>Detects the tokenizer family for a model id (e.g. "qwen3:4b", "qwen/qwen3-8b").</summary>
    public static ModelFamily Detect(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return ModelFamily.Unknown;
        }

        return modelId.Contains("qwen", StringComparison.OrdinalIgnoreCase)
            ? ModelFamily.Qwen
            : ModelFamily.Unknown;
    }
}
