namespace TokensAndCredits.Web.Services.Models;

/// <summary>
/// One token from a local tokenizer (origin = "local"). The Start/End character
/// offsets map the token back to the exact substring of the original text, which
/// powers the "which part of the prompt is this token" highlight.
/// </summary>
/// <param name="Index">Zero-based position in the token sequence.</param>
/// <param name="Id">Tokenizer vocabulary id.</param>
/// <param name="Value">Decoded token text.</param>
/// <param name="Start">Inclusive character offset into the source text.</param>
/// <param name="End">Exclusive character offset into the source text.</param>
public sealed record TokenInfo(
    int Index,
    int Id,
    string Value,
    int Start,
    int End);
