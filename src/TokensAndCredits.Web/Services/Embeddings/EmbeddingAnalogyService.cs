namespace TokensAndCredits.Web.Services.Embeddings;

public sealed class EmbeddingAnalogyService(EmbeddingStore store)
{
    public EmbeddingAnalogyResult Calculate(
        string positiveA,
        string negative,
        string positiveB,
        string relationFrom,
        string relationTo)
    {
        var analogy = CalculateNearestNeighbours(positiveA, negative, positiveB);
        var relationship = CompareRelationships(negative, positiveB, relationFrom, relationTo);
        var vectorPreviews = new Dictionary<string, IReadOnlyList<float>>(
            analogy.VectorPreviews,
            StringComparer.Ordinal);
        foreach (var preview in relationship.VectorPreviews)
        {
            vectorPreviews.TryAdd(preview.Key, preview.Value);
        }

        return new EmbeddingAnalogyResult(
            analogy.Expression,
            analogy.Candidates,
            relationship.Relationship,
            vectorPreviews);
    }

    public EmbeddingNearestNeighboursResult CalculateNearestNeighbours(
        string positiveA,
        string negative,
        string positiveB)
    {
        var positiveAEntry = GetRequiredEntry(positiveA);
        var negativeEntry = GetRequiredEntry(negative);
        var positiveBEntry = GetRequiredEntry(positiveB);

        var target = new float[store.Dimensions];

        for (var dimension = 0; dimension < store.Dimensions; dimension++)
        {
            target[dimension] =
                positiveAEntry.Vector[dimension]
                - negativeEntry.Vector[dimension]
                + positiveBEntry.Vector[dimension];
        }

        var normalizedTarget = EmbeddingVectorMath.Normalize(target);
        var excludedWords = new HashSet<string>(
            [positiveAEntry.Word, negativeEntry.Word, positiveBEntry.Word],
            StringComparer.Ordinal);

        var candidates = store.Entries
            .Where(entry => !excludedWords.Contains(entry.Word))
            .Select(entry => new EmbeddingCandidate(
                entry.Word,
                EmbeddingVectorMath.Dot(normalizedTarget, entry.Vector)))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Word, StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        var vectorPreviews = new Dictionary<string, IReadOnlyList<float>>(StringComparer.Ordinal);
        foreach (var entry in new[]
                 {
                     positiveAEntry,
                     negativeEntry,
                     positiveBEntry,
                 })
        {
            vectorPreviews.TryAdd(entry.Word, entry.Preview);
        }

        return new EmbeddingNearestNeighboursResult(
            $"{positiveAEntry.Word} - {negativeEntry.Word} + {positiveBEntry.Word}",
            candidates,
            vectorPreviews);
    }

    public EmbeddingRelationshipResult CompareRelationships(
        string fromA,
        string toA,
        string fromB,
        string toB)
    {
        var fromAEntry = GetRequiredEntry(fromA);
        var toAEntry = GetRequiredEntry(toA);
        var fromBEntry = GetRequiredEntry(fromB);
        var toBEntry = GetRequiredEntry(toB);
        var relationA = new float[store.Dimensions];
        var relationB = new float[store.Dimensions];

        for (var dimension = 0; dimension < store.Dimensions; dimension++)
        {
            relationA[dimension] = toAEntry.Vector[dimension] - fromAEntry.Vector[dimension];
            relationB[dimension] = toBEntry.Vector[dimension] - fromBEntry.Vector[dimension];
        }

        var cosine = EmbeddingVectorMath.Cosine(relationA, relationB);
        var angle = Math.Acos(cosine) * 180 / Math.PI;
        var vectorPreviews = new Dictionary<string, IReadOnlyList<float>>(StringComparer.Ordinal);
        foreach (var entry in new[] { fromAEntry, toAEntry, fromBEntry, toBEntry })
        {
            vectorPreviews.TryAdd(entry.Word, entry.Preview);
        }

        return new EmbeddingRelationshipResult(
            new EmbeddingRelationship(
                $"{toAEntry.Word} - {fromAEntry.Word}",
                $"{toBEntry.Word} - {fromBEntry.Word}",
                cosine,
                angle),
            vectorPreviews);
    }

    private EmbeddingEntry GetRequiredEntry(string word)
    {
        if (!store.TryGetEntry(word, out var entry))
        {
            throw new EmbeddingWordNotFoundException(word);
        }

        return entry;
    }
}

public sealed record EmbeddingCandidate(string Word, double Score);

public sealed record EmbeddingRelationship(
    string ReferenceLabel,
    string ComparedLabel,
    double Cosine,
    double AngleDegrees);

public sealed record EmbeddingNearestNeighboursResult(
    string Expression,
    IReadOnlyList<EmbeddingCandidate> Candidates,
    IReadOnlyDictionary<string, IReadOnlyList<float>> VectorPreviews);

public sealed record EmbeddingRelationshipResult(
    EmbeddingRelationship Relationship,
    IReadOnlyDictionary<string, IReadOnlyList<float>> VectorPreviews);

public sealed record EmbeddingAnalogyResult(
    string Expression,
    IReadOnlyList<EmbeddingCandidate> Candidates,
    EmbeddingRelationship Relationship,
    IReadOnlyDictionary<string, IReadOnlyList<float>> VectorPreviews);

public sealed class EmbeddingWordNotFoundException(string word)
    : Exception($"The embedding vocabulary does not contain '{word}'.")
{
    public string Word { get; } = word;
}
