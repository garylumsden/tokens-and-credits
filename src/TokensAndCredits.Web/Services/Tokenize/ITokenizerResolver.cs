using Microsoft.ML.Tokenizers;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>A tokenizer plus the metadata the UI needs to label its output.</summary>
/// <param name="Tokenizer">The resolved local tokenizer.</param>
/// <param name="Encoding">Encoding label to show (e.g. "o200k_base").</param>
/// <param name="Exact">True if byte-exact for the target model; false if approximate.</param>
public sealed record ResolvedTokenizer(Tokenizer Tokenizer, string Encoding, bool Exact);

/// <summary>Resolves the local tokenizer to use for a given model.</summary>
public interface ITokenizerResolver
{
    ResolvedTokenizer Resolve(ModelDescriptor model);
}
