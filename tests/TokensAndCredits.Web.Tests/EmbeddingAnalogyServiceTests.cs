using TokensAndCredits.Web.Services.Embeddings;

namespace TokensAndCredits.Web.Tests;

public sealed class EmbeddingAnalogyServiceTests
{
    private static readonly Lazy<EmbeddingStore> Store = new(() =>
        EmbeddingStore.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "embeddings",
            EmbeddingStore.AssetFileName)));

    [Fact]
    public void Calculate_KingMinusManPlusWoman_ReturnsQueenFirst()
    {
        var service = new EmbeddingAnalogyService(Store.Value);

        var result = service.Calculate("king", "man", "woman", "king", "queen");

        Assert.Equal("queen", result.Candidates[0].Word);
    }

    [Fact]
    public void Calculate_ExcludesAnalogySourceWords()
    {
        var service = new EmbeddingAnalogyService(Store.Value);

        var result = service.Calculate("king", "man", "woman", "king", "queen");

        Assert.DoesNotContain(
            result.Candidates,
            candidate => candidate.Word is "king" or "man" or "woman");
    }

    [Fact]
    public void Calculate_ReturnsOnlySixRawValuesForEachDistinctInputWord()
    {
        var service = new EmbeddingAnalogyService(Store.Value);

        var result = service.Calculate("king", "man", "woman", "king", "queen");

        Assert.Equal(["king", "man", "woman", "queen"], result.VectorPreviews.Keys);
        Assert.All(result.VectorPreviews.Values, preview => Assert.Equal(6, preview.Count));
        Assert.Equal(0.50451f, result.VectorPreviews["king"][0]);
    }

    [Fact]
    public void Calculate_ThrowsExplicitErrorForMissingWord()
    {
        var service = new EmbeddingAnalogyService(Store.Value);

        var exception = Assert.Throws<EmbeddingWordNotFoundException>(() =>
            service.Calculate("not-in-this-vocabulary", "man", "woman", "king", "queen"));

        Assert.Equal("not-in-this-vocabulary", exception.Word);
    }

    [Fact]
    public void Search_ReturnsMatchingWordsWithinLimit()
    {
        var words = Store.Value.Search("quee", 2);

        Assert.Contains("queen", words);
        Assert.True(words.Count <= 2);
        Assert.All(words, word => Assert.StartsWith("quee", word, StringComparison.OrdinalIgnoreCase));
    }
}
