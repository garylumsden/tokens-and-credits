namespace TokensAndCredits.Web.Services.Embeddings;

public interface IAzureEmbeddingService
{
    bool IsAvailable { get; }

    string? Deployment { get; }

    Task<LiveEmbeddingComparisonResult> CompareAsync(
        EmbeddingComparisonInput input,
        CancellationToken cancellationToken);
}
