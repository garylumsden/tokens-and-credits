using TokensAndCredits.Web.Services.Embeddings;

namespace TokensAndCredits.Web.Tests;

public sealed class EmbeddingVectorMathTests
{
    [Fact]
    public void Normalize_ProducesUnitVector()
    {
        var normalized = EmbeddingVectorMath.Normalize([3, 4]);

        Assert.Equal(0.6f, normalized[0], 6);
        Assert.Equal(0.8f, normalized[1], 6);
        Assert.Equal(1, EmbeddingVectorMath.Dot(normalized, normalized), 6);
    }

    [Fact]
    public void CosineAndAngle_UseDoubleAccumulation()
    {
        float[] horizontal = [1, 0];
        float[] vertical = [0, 1];

        Assert.Equal(0, EmbeddingVectorMath.Cosine(horizontal, vertical), 12);
        Assert.Equal(90, EmbeddingVectorMath.AngleDegrees(horizontal, vertical), 12);
    }
}
