using System.Text;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Tokenize;

internal readonly record struct BpePair(string Left, string Right);

internal sealed record BpeReplayResult(
    IReadOnlyList<BpeMergeStep> MergeSteps,
    IReadOnlyList<string> FinalPieces,
    int? FinalVocabId)
{
    public string? FinalPiece => FinalPieces.Count == 1 ? FinalPieces[0] : null;

    public bool MatchesVocabId(int expectedId) => FinalVocabId == expectedId;
}

internal static class ByteLevelBpe
{
    private static readonly char[] ByteEncoder = BuildByteEncoder();

    public static IReadOnlyList<TokenByteInfo> DescribeBytes(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var result = new List<TokenByteInfo>(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            result.Add(new TokenByteInfo(i, b, $"0x{b:X2}", DescribeByte(b), ByteEncoder[b].ToString()));
        }

        return result;
    }

    public static string ToByteLevelText(string value) =>
        string.Concat(Encoding.UTF8.GetBytes(value).Select(b => ByteEncoder[b]));

    public static BpeReplayResult Replay(
        string value,
        IReadOnlyDictionary<BpePair, int> mergeRanks,
        IReadOnlyDictionary<string, int> vocab)
    {
        var symbols = Encoding.UTF8.GetBytes(value).Select(b => ByteEncoder[b].ToString()).ToList();
        var steps = new List<BpeMergeStep>();
        var order = 0;

        while (symbols.Count > 1)
        {
            BpePair? bestPair = null;
            var bestRank = int.MaxValue;
            for (var i = 0; i < symbols.Count - 1; i++)
            {
                var pair = new BpePair(symbols[i], symbols[i + 1]);
                if (mergeRanks.TryGetValue(pair, out var rank) && rank < bestRank)
                {
                    bestPair = pair;
                    bestRank = rank;
                }
            }

            if (bestPair is null)
            {
                break;
            }

            var pairToMerge = bestPair.Value;
            var next = new List<string>(symbols.Count);
            for (var i = 0; i < symbols.Count; i++)
            {
                if (i < symbols.Count - 1 &&
                    symbols[i] == pairToMerge.Left &&
                    symbols[i + 1] == pairToMerge.Right)
                {
                    var merged = pairToMerge.Left + pairToMerge.Right;
                    next.Add(merged);
                    steps.Add(new BpeMergeStep(++order, bestRank, pairToMerge.Left, pairToMerge.Right, merged));
                    i++;
                }
                else
                {
                    next.Add(symbols[i]);
                }
            }

            symbols = next;
        }

        int? finalVocabId = null;
        if (symbols.Count == 1 && vocab.TryGetValue(symbols[0], out var id))
        {
            finalVocabId = id;
        }

        return new BpeReplayResult(steps, symbols, finalVocabId);
    }

    private static char[] BuildByteEncoder()
    {
        var visibleBytes = new List<int>();
        AddRange(visibleBytes, '!', '~');
        AddRange(visibleBytes, '¡', '¬');
        AddRange(visibleBytes, '®', 'ÿ');

        var byteToChar = new char[256];
        var next = 0;
        foreach (var b in visibleBytes)
        {
            byteToChar[b] = (char)b;
        }

        for (var b = 0; b < 256; b++)
        {
            if (byteToChar[b] == '\0')
            {
                byteToChar[b] = (char)(256 + next);
                next++;
            }
        }

        return byteToChar;
    }

    private static void AddRange(List<int> target, char start, char end)
    {
        for (var c = start; c <= end; c++)
        {
            target.Add(c);
        }
    }

    private static string DescribeByte(byte b) => b switch
    {
        0x09 => "tab",
        0x0A => "line feed",
        0x0D => "carriage return",
        0x20 => "space",
        >= 0x21 and <= 0x7E => ((char)b).ToString(),
        _ => "UTF-8 byte"
    };
}
