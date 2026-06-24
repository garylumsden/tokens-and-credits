namespace TokensAndCredits.Web.Services.Models;

/// <summary>Detailed, local explanation for why one substring became one token.</summary>
/// <param name="Index">Zero-based token position in the analyzed text.</param>
/// <param name="Id">Tokenizer vocabulary id, also the token rank for tiktoken encodings.</param>
/// <param name="Value">Decoded token text.</param>
/// <param name="Start">Inclusive character offset into the source text.</param>
/// <param name="End">Exclusive character offset into the source text.</param>
/// <param name="Bytes">UTF-8 bytes plus their byte-level BPE symbols.</param>
/// <param name="LeadingSpace">True when this token includes a leading space marker.</param>
/// <param name="Why">Plain-language deterministic explanation for the token boundary.</param>
/// <param name="MergeSteps">Greedy byte-level BPE merge chain when merges.txt is available.</param>
/// <param name="SplitProofs">Neighbour re-encode checks used when merge ranks are unavailable.</param>
/// <param name="Encoding">Encoding label used by the local tokenizer.</param>
/// <param name="Exact">True if local tokenization is exact for the selected model.</param>
public sealed record TokenExplanation(
    int Index,
    int Id,
    string Value,
    int Start,
    int End,
    IReadOnlyList<TokenByteInfo> Bytes,
    bool LeadingSpace,
    string Why,
    IReadOnlyList<BpeMergeStep> MergeSteps,
    IReadOnlyList<SplitProof> SplitProofs,
    string Encoding,
    bool Exact);

/// <summary>One UTF-8 byte from token text and the byte-level BPE symbol it maps to.</summary>
/// <param name="Index">Zero-based byte position inside the token.</param>
/// <param name="Decimal">Byte value as a decimal integer.</param>
/// <param name="Hex">Byte value as hexadecimal text.</param>
/// <param name="Utf8">Human-readable byte description.</param>
/// <param name="ByteLevel">GPT-2 byte-level unicode symbol used by byte-level BPE.</param>
public sealed record TokenByteInfo(int Index, int Decimal, string Hex, string Utf8, string ByteLevel);

/// <summary>One greedy BPE merge step from the model's merges.txt ranking.</summary>
/// <param name="Order">One-based replay order.</param>
/// <param name="Rank">Merge rank from merges.txt; lower ranks merge first.</param>
/// <param name="Left">Left symbol or previously merged piece.</param>
/// <param name="Right">Right symbol or previously merged piece.</param>
/// <param name="Result">Merged byte-level BPE piece.</param>
public sealed record BpeMergeStep(int Order, int Rank, string Left, string Right, string Result);

/// <summary>Proof that a token boundary stays split when the substring is extended by a neighbour.</summary>
/// <param name="Direction">Boundary tested: start or end.</param>
/// <param name="Neighbor">Visible neighbouring character used for the test.</param>
/// <param name="ExtendedText">Token substring plus the neighbouring character.</param>
/// <param name="TokenIds">Token ids returned when re-encoding the extended substring.</param>
/// <param name="Explanation">Plain-language result of the re-encode check.</param>
public sealed record SplitProof(
    string Direction,
    string Neighbor,
    string ExtendedText,
    IReadOnlyList<int> TokenIds,
    string Explanation);
