using Microsoft.ML.Tokenizers;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>
/// Turns text into a sequence of <see cref="TokenInfo"/> using a tokenizer's
/// character offsets, so the front end can highlight which substring is which token.
/// </summary>
public sealed class TokenAnalyzer
{
    public IReadOnlyList<TokenInfo> Analyze(Tokenizer tokenizer, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TokenInfo>();
        }

        var encoded = tokenizer.EncodeToTokens(text, out _);
        var result = new List<TokenInfo>(encoded.Count);
        for (var i = 0; i < encoded.Count; i++)
        {
            var token = encoded[i];
            var start = token.Offset.Start.Value;
            var end = token.Offset.End.Value;
            result.Add(new TokenInfo(i, token.Id, token.Value, start, end));
        }

        return result;
    }
}
