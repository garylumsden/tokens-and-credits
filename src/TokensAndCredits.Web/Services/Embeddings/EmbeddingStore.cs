using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TokensAndCredits.Web.Services.Embeddings;

public sealed class EmbeddingStore
{
    public const string DatasetName = "GloVe Wikipedia 2014 + Gigaword 5";
    public const string Origin = "local-static-embedding";
    public const string AssetFileName = "glove-wiki-gigaword-50.top10000.txt.gz";

    private readonly Dictionary<string, EmbeddingEntry> _byWord;
    private readonly IReadOnlyList<EmbeddingEntry> _entries;

    private EmbeddingStore(
        int dimensions,
        Dictionary<string, EmbeddingEntry> byWord,
        IReadOnlyList<EmbeddingEntry> entries)
    {
        Dimensions = dimensions;
        _byWord = byWord;
        _entries = entries;
    }

    public int Dimensions { get; }

    public int VocabularyCount => _entries.Count;

    public static EmbeddingStore Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(
            gzip,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);

        var header = reader.ReadLine()
            ?? throw new InvalidDataException("The embedding asset does not contain a header.");
        var headerParts = SplitFields(header);
        if (headerParts.Length != 2
            || !int.TryParse(headerParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var expectedCount)
            || !int.TryParse(headerParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var dimensions)
            || expectedCount <= 0
            || dimensions <= 0)
        {
            throw new InvalidDataException("The embedding asset header must contain a positive vocabulary count and dimension count.");
        }

        var entries = new List<EmbeddingEntry>(expectedCount);
        var byWord = new Dictionary<string, EmbeddingEntry>(expectedCount, StringComparer.Ordinal);

        for (var entryIndex = 0; entryIndex < expectedCount; entryIndex++)
        {
            var lineNumber = entryIndex + 2;
            var line = reader.ReadLine()
                ?? throw new InvalidDataException(
                    $"The embedding asset ended at line {lineNumber} before all {expectedCount} entries were read.");
            var fields = SplitFields(line);
            if (fields.Length != dimensions + 1)
            {
                throw new InvalidDataException(
                    $"Embedding line {lineNumber} must contain one word and {dimensions} values.");
            }

            var word = fields[0];
            if (word.Length == 0)
            {
                throw new InvalidDataException($"Embedding line {lineNumber} contains an empty word.");
            }

            if (byWord.ContainsKey(word))
            {
                throw new InvalidDataException($"Embedding line {lineNumber} contains duplicate word '{word}'.");
            }

            var values = new float[dimensions];
            for (var dimension = 0; dimension < dimensions; dimension++)
            {
                if (!float.TryParse(
                        fields[dimension + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var value)
                    || !float.IsFinite(value))
                {
                    throw new InvalidDataException(
                        $"Embedding line {lineNumber} contains an invalid value at dimension {dimension + 1}.");
                }

                values[dimension] = value;
            }

            float[] normalized;
            try
            {
                normalized = EmbeddingVectorMath.Normalize(values);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Embedding line {lineNumber} contains a vector that cannot be normalized.",
                    exception);
            }

            var preview = values[..Math.Min(6, values.Length)];
            var entry = new EmbeddingEntry(word, normalized, preview);
            byWord.Add(word, entry);
            entries.Add(entry);
        }

        if (reader.ReadLine() is not null)
        {
            throw new InvalidDataException(
                $"The embedding asset contains more entries than the header count of {expectedCount}.");
        }

        return new EmbeddingStore(dimensions, byWord, entries);
    }

    public IReadOnlyList<string> Search(string query, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (limit is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The limit must be between 1 and 20.");
        }

        return _entries
            .Where(entry => entry.Word.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Word)
            .OrderBy(word => word, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    internal IReadOnlyList<EmbeddingEntry> Entries => _entries;

    internal bool TryGetEntry(
        string word,
        [NotNullWhen(true)] out EmbeddingEntry? entry) =>
        _byWord.TryGetValue(word, out entry);

    private static string[] SplitFields(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

internal sealed record EmbeddingEntry(string Word, float[] Vector, float[] Preview);
