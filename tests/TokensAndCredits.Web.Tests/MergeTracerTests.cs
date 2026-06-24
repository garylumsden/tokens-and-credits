using Microsoft.ML.Tokenizers;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Tests;

public sealed class MergeTracerTests
{
    private static readonly TiktokenTokenizer O200k = TiktokenTokenizer.CreateForEncoding("o200k_base");

    [Fact]
    public void Trace_ReproducesTokenizerForSingleTokenWord()
    {
        var trace = new MergeTracer().Trace(O200k, "lowest");

        Assert.True(trace.Verified);
        Assert.True(trace.IdIsRank); // tiktoken: token id IS the merge rank, so order is exact.
        var token = Assert.Single(trace.FinalTokens);
        Assert.Equal("lowest", token.Text);
        // Every merge ends at the final token, so the last step's result is the whole word.
        Assert.Equal("lowest", trace.Steps[^1].Result);
        // Ranks are real ids and strictly identify each merged piece.
        Assert.All(trace.Steps, step => Assert.True(step.Rank >= 0));
        // A single final token means nothing was left unmerged.
        Assert.Empty(trace.RejectedPairs);
        // Each step records the field it chose from; the chosen pair is the lowest rank present.
        Assert.All(trace.Steps, step =>
        {
            var chosen = Assert.Single(step.Candidates, c => c.Chosen);
            Assert.Equal(step.Rank, chosen.Rank);
            Assert.Equal(step.Candidates.Min(c => c.Rank), chosen.Rank);
        });
    }

    [Fact]
    public void Trace_RecordsRejectedPairWhenWordStaysMultiToken()
    {
        var trace = new MergeTracer().Trace(O200k, "tokenization");

        Assert.True(trace.FinalTokens.Count > 1);
        // Merging stopped because the glued final pair is not a single vocabulary entry.
        var rejected = Assert.Single(trace.RejectedPairs);
        Assert.Equal(trace.FinalTokens[0].Text + trace.FinalTokens[1].Text, rejected.Glued);
        Assert.True(O200k.EncodeToIds(rejected.Glued).Count > 1);
    }

    [Fact]
    public void Trace_ReproducesTokenizerForMultiTokenWord()
    {
        var trace = new MergeTracer().Trace(O200k, "tokenization");

        Assert.True(trace.Verified);
        var expected = O200k.EncodeToTokens("tokenization", out _).Select(t => t.Id).ToArray();
        Assert.Equal(expected, trace.FinalTokens.Select(t => t.Id).ToArray());
        Assert.True(trace.FinalTokens.Count > 1);
    }

    [Fact]
    public void TracePhrase_TracesEachWordAndReproducesWholePhrase()
    {
        var traces = new MergeTracer().TracePhrase(O200k, "I love tokenization");

        Assert.Equal(3, traces.Count);
        Assert.All(traces, t => Assert.True(t.Verified));

        // Per-word traces (first word bare, later words with a leading space) must concatenate to
        // the tokenizer's own tokenization of the whole phrase.
        var perWordIds = traces.SelectMany(t => t.FinalTokens.Select(f => f.Id)).ToArray();
        var wholeIds = O200k.EncodeToTokens("I love tokenization", out _).Select(t => t.Id).ToArray();
        Assert.Equal(wholeIds, perWordIds);

        // The space before a later word is part of that word's first piece, not dropped.
        Assert.StartsWith(" ", traces[1].FinalTokens[0].Text);
    }
}
