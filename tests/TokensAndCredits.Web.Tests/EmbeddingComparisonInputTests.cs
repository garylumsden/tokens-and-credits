using TokensAndCredits.Web.Services.Embeddings;
using TokensAndCredits.Web.Services.Catalog;

namespace TokensAndCredits.Web.Tests;

public sealed class EmbeddingComparisonInputTests
{
    [Fact]
    public void TryCreate_TrimsAndAcceptsDistinctInputs()
    {
        var valid = EmbeddingComparisonInput.TryCreate(
            " first ",
            "second",
            "third",
            "target",
            out var input,
            out var error);

        Assert.True(valid);
        Assert.NotNull(input);
        Assert.Equal("first", input.First);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_RejectsMissingValues(string? first)
    {
        var valid = EmbeddingComparisonInput.TryCreate(
            first,
            "second",
            "third",
            "target",
            out var input,
            out var error);

        Assert.False(valid);
        Assert.Null(input);
        Assert.Equal("Field 'first' is required.", error);
    }

    [Fact]
    public void TryCreate_RejectsTrimmedValuesOverLimit()
    {
        var valid = EmbeddingComparisonInput.TryCreate(
            new string('a', EmbeddingComparisonInput.MaxInputLength + 1),
            "second",
            "third",
            "target",
            out _,
            out var error);

        Assert.False(valid);
        Assert.Contains("must not exceed 512 characters", error);
    }

    [Fact]
    public void TryCreate_UsesOrdinalComparisonForDistinctValues()
    {
        Assert.True(EmbeddingComparisonInput.TryCreate(
            "value",
            "Value",
            "third",
            "target",
            out _,
            out _));

        Assert.False(EmbeddingComparisonInput.TryCreate(
            " value ",
            "value",
            "third",
            "target",
            out _,
            out var error));
        Assert.Equal("First, second, third, and target must be distinct.", error);
    }

    [Fact]
    public void AzureOptions_RequireEndpointAndEmbeddingDeployment()
    {
        var options = new AzureFoundryOptions
        {
            Endpoint = "https://example.openai.azure.com",
        };

        Assert.False(options.IsEmbeddingConfigured);

        options.EmbeddingDeployment = "text-embedding-3-small";

        Assert.True(options.IsEmbeddingConfigured);
        Assert.False(options.IsConfigured);
    }
}
