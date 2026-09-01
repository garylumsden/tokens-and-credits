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
        var positiveAEntry = GetRequiredEntry(positiveA);
        var negativeEntry = GetRequiredEntry(negative);
        var positiveBEntry = GetRequiredEntry(positiveB);
        var relationFromEntry = GetRequiredEntry(relationFrom);
        var relationToEntry = GetRequiredEntry(relationTo);

        var target = new float[store.Dimensions];
        var referenceRelation = new float[store.Dimensions];
        var comparedRelation = new float[store.Dimensions];

        for (var dimension = 0; dimension < store.Dimensions; dimension++)
        {
            target[dimension] =
                positiveAEntry.Vector[dimension]
                - negativeEntry.Vector[dimension]
                + positiveBEntry.Vector[dimension];
            referenceRelation[dimension] =
                positiveBEntry.Vector[dimension] - negativeEntry.Vector[dimension];
            comparedRelation[dimension] =
                relationToEntry.Vector[dimension] - relationFromEntry.Vector[dimension];
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

        var relationshipCosine = EmbeddingVectorMath.Cosine(referenceRelation, comparedRelation);
        var relationshipAngle = Math.Acos(relationshipCosine) * 180 / Math.PI;
        var vectorPreviews = new Dictionary<string, IReadOnlyList<float>>(StringComparer.Ordinal);
        foreach (var entry in new[]
                 {
                     positiveAEntry,
                     negativeEntry,
                     positiveBEntry,
                     relationFromEntry,
                     relationToEntry,
                 })
        {
            vectorPreviews.TryAdd(entry.Word, entry.Preview);
        }

        return new EmbeddingAnalogyResult(
            $"{positiveAEntry.Word} - {negativeEntry.Word} + {positiveBEntry.Word}",
            candidates,
            new EmbeddingRelationship(
                $"{positiveBEntry.Word} - {negativeEntry.Word}",
                $"{relationToEntry.Word} - {relationFromEntry.Word}",
                relationshipCosine,
                relationshipAngle),
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
