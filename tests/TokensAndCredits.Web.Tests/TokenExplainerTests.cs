using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.Tokenizers;
using TokensAndCredits.Web.Services.Models;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Tests;

public sealed class TokenExplainerTests
{
    [Fact]
    public void ByteLevelBpeReplay_ReconstructsLeadingSpaceEnglishToken()
    {
        var ranks = new Dictionary<BpePair, int>
        {
            [new BpePair("Ġ", "d")] = 0,
            [new BpePair("Ġd", "o")] = 1,
            [new BpePair("Ġdo", "g")] = 2
        };
        var vocab = new Dictionary<string, int> { ["Ġdog"] = 123 };

        var replay = ByteLevelBpe.Replay(" dog", ranks, vocab);

        Assert.Equal("Ġdog", replay.FinalPiece);
        Assert.True(replay.MatchesVocabId(123));
        Assert.Collection(
            replay.MergeSteps,
            step => Assert.Equal("Ġd", step.Result),
            step => Assert.Equal("Ġdo", step.Result),
            step => Assert.Equal("Ġdog", step.Result));
    }

    [Fact]
    public void Explain_ForTiktokenEncoding_ProvidesNeighbourSplitProof()
    {
        var tokenizer = TiktokenTokenizer.CreateForEncoding("o200k_base");
        const string text = "hello world";
        var token = new TokenAnalyzer()
            .Analyze(tokenizer, text)
            .First(t => t.Value == "hello");
        var model = new ModelDescriptor(
            "test-gpt-4o",
            "Test GPT-4o",
            ModelSource.AzureFoundry,
            "cloud",
            "o200k_base",
            SupportsReasoning: false,
            SupportsCaching: false,
            Exact: true);
        var explainer = new TokenExplainer(
            new QwenBpeFactory(NullLogger<QwenBpeFactory>.Instance),
            NullLogger<TokenExplainer>.Instance);

        var explanation = explainer.Explain(
            model,
            new ResolvedTokenizer(tokenizer, "o200k_base", Exact: true),
            text,
            token);

        Assert.Empty(explanation.MergeSteps);
        var endProof = Assert.Single(explanation.SplitProofs, p => p.Direction == "end");
        Assert.True(endProof.TokenIds.Count > 1);
        Assert.Contains(token.Id, endProof.TokenIds);
        Assert.Contains("no single token", endProof.Explanation);
    }
}

