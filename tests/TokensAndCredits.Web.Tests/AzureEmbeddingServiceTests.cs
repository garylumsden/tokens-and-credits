using Azure.Core;
using Microsoft.Extensions.Options;
using TokensAndCredits.Web.Services.Catalog;
using TokensAndCredits.Web.Services.Embeddings;

namespace TokensAndCredits.Web.Tests;

public sealed class AzureEmbeddingServiceTests
{
    [Fact]
    public void BuildResult_ReturnsSixComparisonsAndSixValuePreviews()
    {
        string[] inputs = ["king", "man", "woman", "queen"];
        float[][] vectors =
        [
            [1, 0, 0, 1, 2, 3, 4, 5],
            [0, 1, 0, 1, 2, 3, 4, 5],
            [0, 0, 1, 1, 2, 3, 4, 5],
            [1, -1, 1, 1, 2, 3, 4, 5],
        ];

        var result = AzureEmbeddingService.BuildResult(
            inputs,
            vectors,
            "text-embedding-3-small",
            latencyMs: 42);

        Assert.Equal(AzureEmbeddingService.Origin, result.Origin);
        Assert.Equal("text-embedding-3-small", result.Model);
        Assert.Equal(8, result.Dimensions);
        Assert.Equal(42, result.LatencyMs);
        Assert.Equal(6, result.Comparisons.Count);
        Assert.Equal(
            [
                "First vs Second",
                "First vs Third",
                "First vs Target",
                "Second vs Third",
                "Second vs Target",
                "Third vs Target",
            ],
            result.Comparisons.Select(comparison => comparison.Label));
        Assert.Equal("first", result.Inputs[0].Role);
        Assert.All(result.Inputs, preview => Assert.Equal(6, preview.Values.Count));
        Assert.Equal(0, result.Arithmetic.AngleDegrees, 10);
        Assert.Equal(1, result.Arithmetic.Cosine, 10);
    }

    [Fact]
    public void BuildResult_RejectsNonFiniteVectors()
    {
        string[] inputs = ["a", "b", "c", "d"];
        float[][] vectors =
        [
            [1, float.NaN],
            [0, 1],
            [1, 1],
            [1, -1],
        ];

        Assert.Throws<ArgumentException>(() =>
            AzureEmbeddingService.BuildResult(inputs, vectors, "deployment", 1));
    }

    [Fact]
    public async Task CompareAsync_WhenNotConfigured_FailsWithoutNetworkAccess()
    {
        var service = new AzureEmbeddingService(
            Options.Create(new AzureFoundryOptions()),
            new StubTokenCredential());
        var input = new EmbeddingComparisonInput("a", "b", "c", "d");

        Assert.False(service.IsAvailable);
        Assert.Null(service.Deployment);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompareAsync(input, CancellationToken.None));
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Token access is not expected.");

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Token access is not expected.");
    }
}
