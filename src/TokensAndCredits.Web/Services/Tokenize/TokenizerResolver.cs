using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>
/// Picks the local tokenizer for a model. Cloud OpenAI families use their exact tiktoken
/// encoding. Local models (Foundry Local, LM Studio, Ollama) use their own byte-level BPE when
/// we can: Foundry's cached per-model files if present, otherwise the bundled Qwen tokenizer for
/// any Qwen model. When no exact tokenizer is available the count is approximated with
/// o200k_base and flagged as not exact.
/// </summary>
public sealed class TokenizerResolver : ITokenizerResolver
{
    private const string FallbackEncoding = "o200k_base";

    private readonly TiktokenFactory _tiktoken;
    private readonly QwenBpeFactory _qwen;

    public TokenizerResolver(TiktokenFactory tiktoken, QwenBpeFactory qwen)
    {
        _tiktoken = tiktoken;
        _qwen = qwen;
    }

    public ResolvedTokenizer Resolve(ModelDescriptor model)
    {
        // Cloud OpenAI deployments: exact tiktoken for the declared encoding.
        if (model.Source == ModelSource.AzureFoundry)
        {
            return new ResolvedTokenizer(_tiktoken.ForEncoding(model.Encoding), model.Encoding, Exact: true);
        }

        // Foundry Local sometimes caches the model's own vocab.json + merges.txt.
        if (model.Source == ModelSource.FoundryLocal && _qwen.TryLoad(model.Id) is { } perModel)
        {
            return new ResolvedTokenizer(perModel, "byte-level BPE (model files)", Exact: true);
        }

        // Any Qwen model (any local host) → bundled Qwen byte-level BPE (Qwen2/2.5/3 share it).
        if (ModelFamilyDetector.Detect(model.Id) == ModelFamily.Qwen && _qwen.TryLoadBundledQwen() is { } qwen)
        {
            return new ResolvedTokenizer(qwen, "Qwen byte-level BPE", Exact: true);
        }

        // No exact local tokenizer: approximate with o200k_base, clearly flagged as not exact.
        return new ResolvedTokenizer(_tiktoken.ForEncoding(FallbackEncoding), $"{FallbackEncoding} (approx)", Exact: false);
    }
}
