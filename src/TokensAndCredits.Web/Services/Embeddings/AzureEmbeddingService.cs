#pragma warning disable OPENAI001

using System.ClientModel.Primitives;
using System.Diagnostics;
using Azure.Core;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using TokensAndCredits.Web.Services.Catalog;

namespace TokensAndCredits.Web.Services.Embeddings;

public sealed class AzureEmbeddingService : IAzureEmbeddingService
{
    public const string Origin = "azure-live-embedding";

    private const string AzureScope = "https://cognitiveservices.azure.com/.default";
    private const int PreviewLength = 6;
    private static readonly string[] Labels = ["First", "Second", "Third", "Target"];
    private static readonly string[] Roles = ["first", "second", "third", "target"];

    private readonly EmbeddingClient? _client;

    public AzureEmbeddingService(
        IOptions<AzureFoundryOptions> options,
        TokenCredential credential)
    {
        var azureOptions = options.Value;
        IsAvailable = azureOptions.IsEmbeddingConfigured;
        Deployment = IsAvailable ? azureOptions.EmbeddingDeployment!.Trim() : null;

        if (!IsAvailable)
        {
            return;
        }

        var baseEndpoint = azureOptions.Endpoint!.TrimEnd('/');
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri($"{baseEndpoint}/openai/v1")
        };
        var authPolicy = new BearerTokenPolicy(credential, AzureScope);
        _client = new EmbeddingClient(Deployment, authPolicy, clientOptions);
    }

    public bool IsAvailable { get; }

    public string? Deployment { get; }

    public async Task<LiveEmbeddingComparisonResult> CompareAsync(
        EmbeddingComparisonInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_client is null || Deployment is null)
        {
            throw new InvalidOperationException("Live Azure embeddings are not configured.");
        }

        var inputs = new[] { input.First, input.Second, input.Third, input.Target };
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GenerateEmbeddingsAsync(
            inputs,
            options: null,
            cancellationToken);
        stopwatch.Stop();

        var vectors = response.Value
            .Select(embedding => embedding.ToFloats().ToArray())
            .ToArray();

        return BuildResult(inputs, vectors, Deployment, stopwatch.ElapsedMilliseconds);
    }

    public static LiveEmbeddingComparisonResult BuildResult(
        IReadOnlyList<string> inputs,
        IReadOnlyList<float[]> vectors,
        string deployment,
        long latencyMs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);

        if (inputs.Count != Labels.Length || vectors.Count != Labels.Length)
        {
            throw new ArgumentException("Exactly four inputs and vectors are required.");
        }

        var dimensions = vectors[0].Length;
        if (dimensions == 0 || vectors.Any(vector => vector.Length != dimensions))
        {
            throw new ArgumentException("All vectors must have the same nonzero dimensions.", nameof(vectors));
        }

        var comparisons = new List<EmbeddingPairwiseComparison>(6);
        for (var leftIndex = 0; leftIndex < vectors.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < vectors.Count; rightIndex++)
            {
                comparisons.Add(CreateComparison(
                    leftIndex,
                    rightIndex,
                    vectors[leftIndex],
                    vectors[rightIndex]));
            }
        }

        var arithmeticVector = new float[dimensions];
        for (var index = 0; index < dimensions; index++)
        {
            arithmeticVector[index] = vectors[0][index] - vectors[1][index] + vectors[2][index];
        }

        var arithmeticCosine = EmbeddingVectorMath.Cosine(arithmeticVector, vectors[3]);
        var previews = inputs
            .Select((text, index) => new EmbeddingInputPreview(
                index,
                Roles[index],
                Labels[index],
                text,
                vectors[index].Take(PreviewLength).ToArray()))
            .ToArray();

        return new LiveEmbeddingComparisonResult(
            Origin,
            deployment.Trim(),
            dimensions,
            latencyMs,
            previews,
            comparisons,
            new EmbeddingArithmeticComparison(
                "First - Second + Third",
                3,
                "Target",
                arithmeticCosine,
                Math.Acos(arithmeticCosine) * 180 / Math.PI));
    }

    private static EmbeddingPairwiseComparison CreateComparison(
        int leftIndex,
        int rightIndex,
        float[] left,
        float[] right)
    {
        var cosine = EmbeddingVectorMath.Cosine(left, right);
        return new EmbeddingPairwiseComparison(
            leftIndex,
            rightIndex,
            Roles[leftIndex],
            Roles[rightIndex],
            $"{Labels[leftIndex]} vs {Labels[rightIndex]}",
            cosine,
            Math.Acos(cosine) * 180 / Math.PI);
    }
}

public sealed record EmbeddingInputPreview(
    int Index,
    string Role,
    string Label,
    string Text,
    IReadOnlyList<float> Values);

public sealed record EmbeddingPairwiseComparison(
    int LeftIndex,
    int RightIndex,
    string Left,
    string Right,
    string Label,
    double Cosine,
    double AngleDegrees);

public sealed record EmbeddingArithmeticComparison(
    string Expression,
    int TargetIndex,
    string TargetLabel,
    double Cosine,
    double AngleDegrees);

public sealed record LiveEmbeddingComparisonResult(
    string Origin,
    string Model,
    int Dimensions,
    long LatencyMs,
    IReadOnlyList<EmbeddingInputPreview> Inputs,
    IReadOnlyList<EmbeddingPairwiseComparison> Comparisons,
    EmbeddingArithmeticComparison Arithmetic);
