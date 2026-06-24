using System.Collections.Concurrent;
using Microsoft.ML.Tokenizers;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>
/// Builds and caches tiktoken tokenizers (OpenAI families). The matching
/// Microsoft.ML.Tokenizers.Data.* package must be referenced for each encoding.
/// </summary>
public sealed class TiktokenFactory
{
    private readonly ConcurrentDictionary<string, Tokenizer> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns a cached tokenizer for an encoding such as "o200k_base" or "cl100k_base".</summary>
    public Tokenizer ForEncoding(string encoding) =>
        _cache.GetOrAdd(encoding, static e => TiktokenTokenizer.CreateForEncoding(e));
}
