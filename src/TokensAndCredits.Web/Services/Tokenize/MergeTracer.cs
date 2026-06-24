using Microsoft.ML.Tokenizers;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>One token of a merge trace: the surface text and its vocabulary id/rank.</summary>
/// <param name="Text">Token surface text.</param>
/// <param name="Id">Tokenizer vocabulary id (equals the BPE rank for tiktoken encodings).</param>
public sealed record MergeToken(string Text, int Id);

/// <summary>One adjacent pair considered at a merge step, with the table rank that decided it.</summary>
/// <param name="Left">Left symbol.</param>
/// <param name="Right">Right symbol.</param>
/// <param name="Rank">Merge-table rank (lower = higher priority).</param>
/// <param name="Chosen">True for the lowest-ranked candidate that was actually merged.</param>
public sealed record MergeCandidate(string Left, string Right, int Rank, bool Chosen);

/// <summary>One greedy merge step plus all the candidates it chose between.</summary>
/// <param name="Order">One-based replay order.</param>
/// <param name="Rank">Rank of the chosen (lowest-ranked) pair.</param>
/// <param name="Left">Left symbol of the chosen pair.</param>
/// <param name="Right">Right symbol of the chosen pair.</param>
/// <param name="Result">Merged piece.</param>
/// <param name="Candidates">Every adjacent pair found in the table at this step (the field of contenders).</param>
public sealed record MergeStepDetail(
    int Order,
    int Rank,
    string Left,
    string Right,
    string Result,
    IReadOnlyList<MergeCandidate> Candidates);

/// <summary>An adjacent final pair that was NOT merged because its glued form has no table entry.</summary>
/// <param name="Left">Left token.</param>
/// <param name="Right">Right token.</param>
/// <param name="Glued">The two glued together (the sequence the table was checked for).</param>
public sealed record RejectedPair(string Left, string Right, string Glued);

/// <summary>
/// A real, replayable byte-pair-encoding trace for a single word, reconstructed from the
/// live tokenizer so the UI can animate exactly how the model splits text.
/// </summary>
/// <param name="Word">The word that was traced.</param>
/// <param name="Steps">Ordered greedy merge steps actually taken, each with its candidate field.</param>
/// <param name="FinalTokens">Final tokens with their ids.</param>
/// <param name="RejectedPairs">Adjacent final pairs whose glued form isn't in the table (why merging stopped).</param>
/// <param name="Verified">True when the replay reproduces the tokenizer's own output exactly.</param>
/// <param name="IdIsRank">
/// True only for tiktoken encodings, where a token's vocabulary id IS its BPE merge rank, so the
/// reconstructed merge order is exact. False for merges.txt BPE (e.g. Qwen), where the id is used
/// as a rank proxy: the final tokens are still verified, but the intermediate merge order is a
/// faithful reconstruction rather than a guaranteed-identical replay.
/// </param>
public sealed record MergeTrace(
    string Word,
    IReadOnlyList<MergeStepDetail> Steps,
    IReadOnlyList<MergeToken> FinalTokens,
    IReadOnlyList<RejectedPair> RejectedPairs,
    bool Verified,
    bool IdIsRank);

/// <summary>
/// Reconstructs the greedy BPE merge sequence for a word using only the public tokenizer
/// surface. A candidate merged piece is a real vocabulary token when it encodes to exactly one
/// id; that id is its rank. Greedily merging the lowest-ranked adjacent pair reproduces the
/// tokenizer's own algorithm for both tiktoken (GPT) and merges.txt (Qwen) BPE — and the result
/// is verified against the tokenizer so the animation is always faithful.
/// </summary>
public sealed class MergeTracer
{
    public MergeTrace Trace(Tokenizer tokenizer, string word)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(word);

        var symbols = word.Select(c => c.ToString()).ToList();
        var steps = new List<MergeStepDetail>();
        var rankCache = new Dictionary<string, int?>(StringComparer.Ordinal);
        var order = 0;

        int? Rank(string piece)
        {
            if (rankCache.TryGetValue(piece, out var cached))
            {
                return cached;
            }

            var ids = tokenizer.EncodeToIds(piece);
            var rank = ids.Count == 1 ? ids[0] : (int?)null;
            rankCache[piece] = rank;
            return rank;
        }

        while (symbols.Count > 1)
        {
            // Gather every adjacent pair the table knows (the field of contenders), then pick the
            // lowest rank. Recording the whole field lets the UI show what "lowest" is lower than.
            var candidates = new List<(int Index, string Left, string Right, int Rank)>();
            for (var i = 0; i < symbols.Count - 1; i++)
            {
                if (Rank(symbols[i] + symbols[i + 1]) is int rank)
                {
                    candidates.Add((i, symbols[i], symbols[i + 1], rank));
                }
            }

            if (candidates.Count == 0)
            {
                break;
            }

            var winner = candidates.MinBy(c => c.Rank);
            var candidateList = candidates
                .OrderBy(c => c.Rank)
                .Select(c => new MergeCandidate(c.Left, c.Right, c.Rank, c.Index == winner.Index))
                .ToList();

            var bestMerged = winner.Left + winner.Right;
            steps.Add(new MergeStepDetail(++order, winner.Rank, winner.Left, winner.Right, bestMerged, candidateList));
            symbols[winner.Index] = bestMerged;
            symbols.RemoveAt(winner.Index + 1);
        }

        var finalTokens = symbols
            .Select(symbol => new MergeToken(symbol, Rank(symbol) ?? -1))
            .ToList();

        // Why it stopped: every remaining adjacent pair, glued together, is absent from the table.
        var rejectedPairs = new List<RejectedPair>();
        for (var i = 0; i < symbols.Count - 1; i++)
        {
            rejectedPairs.Add(new RejectedPair(symbols[i], symbols[i + 1], symbols[i] + symbols[i + 1]));
        }

        var actual = tokenizer.EncodeToTokens(word, out _);
        var verified = actual.Count == finalTokens.Count &&
            actual.Select(t => t.Id).SequenceEqual(finalTokens.Select(f => f.Id));

        // tiktoken stores mergeable ranks as the token ids, so "lowest id wins" is the true merge
        // order. Other BPE (e.g. Qwen's merges.txt) only happens to verify on the final tokens.
        var idIsRank = tokenizer is TiktokenTokenizer;

        return new MergeTrace(word, steps, finalTokens, rejectedPairs, verified, idIsRank);
    }

    /// <summary>
    /// Traces each word of a short phrase as a separate pre-token. The real tokenizer attaches a
    /// leading space to every word after the first, so word <c>n</c> is traced as " word"; this
    /// reproduces the model's own per-word tokenization for simple space-separated input.
    /// </summary>
    public IReadOnlyList<MergeTrace> TracePhrase(Tokenizer tokenizer, string phrase)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(phrase);

        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var traces = new List<MergeTrace>(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            var preToken = i == 0 ? words[i] : " " + words[i];
            traces.Add(Trace(tokenizer, preToken));
        }

        return traces;
    }
}
