using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.ML.Tokenizers;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>Builds deterministic per-token explanations for local BPE tokenization.</summary>
public sealed class TokenExplainer
{
    private readonly ConcurrentDictionary<string, QwenBpeData> _qwenData = new(StringComparer.OrdinalIgnoreCase);
    private readonly QwenBpeFactory _qwen;
    private readonly ILogger<TokenExplainer> _logger;

    /// <summary>Creates a token explainer backed by tokenizer file discovery.</summary>
    /// <param name="qwen">Qwen tokenizer factory used to locate vocab.json and merges.txt.</param>
    /// <param name="logger">Logger for non-fatal explanation failures.</param>
    public TokenExplainer(QwenBpeFactory qwen, ILogger<TokenExplainer> logger)
    {
        _qwen = qwen ?? throw new ArgumentNullException(nameof(qwen));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Explains why the selected token is a deterministic BPE token.</summary>
    /// <param name="model">Selected model metadata.</param>
    /// <param name="resolved">Resolved tokenizer and encoding metadata.</param>
    /// <param name="text">Source text that was tokenized.</param>
    /// <param name="token">Token to explain.</param>
    /// <returns>Byte breakdown, boundary proof, and merge chain when available.</returns>
    public TokenExplanation Explain(ModelDescriptor model, ResolvedTokenizer resolved, string text, TokenInfo token)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(token);

        var surface = SafeSlice(text, token.Start, token.End);
        if (surface.Length == 0 && token.Value.Length > 0)
        {
            surface = token.Value;
        }

        var bytes = ByteLevelBpe.DescribeBytes(surface);
        var leadingSpace = surface.StartsWith(' ');
        var qwenData = model.Source == ModelSource.FoundryLocal ? TryGetQwenData(model.Id) : null;
        qwenData ??= ModelFamilyDetector.Detect(model.Id) == ModelFamily.Qwen ? TryGetBundledQwenData() : null;

        if (qwenData is not null)
        {
            var replay = ByteLevelBpe.Replay(surface, qwenData.MergeRanks, qwenData.Vocab);
            var finalPiece = replay.FinalPiece ?? string.Join(" + ", replay.FinalPieces);
            var matched = replay.MatchesVocabId(token.Id);
            var why = matched
                ? $"UTF-8 bytes map to byte-level symbols (a space becomes \u0120), then the merge table greedily merges them into vocab piece '{finalPiece}' with id {token.Id}."
                : $"Replayed the merge table for this surface and ended at '{finalPiece}'; the runtime token id is {token.Id}, so this may involve tokenizer normalisation or special-token handling.";

            return new TokenExplanation(
                token.Index,
                token.Id,
                token.Value,
                token.Start,
                token.End,
                bytes,
                leadingSpace,
                AddBoundaryNote(why, text, token),
                replay.MergeSteps,
                Array.Empty<SplitProof>(),
                resolved.Encoding,
                resolved.Exact);
        }

        var splitProofs = BuildSplitProofs(resolved.Tokenizer, text, token, surface);
        var tiktokenWhy = resolved.Exact
            ? (splitProofs.Count > 0
                ? $"No public merge list ships for {resolved.Encoding}, so the boundary is shown by re-encoding this token plus a neighbouring character. Token id {token.Id} is its vocab rank/id."
                : $"Token id {token.Id} is its vocab rank/id in {resolved.Encoding}; this token sits at the text edge, so no neighbour split proof is possible.")
            : $"This model's own tokenizer isn't available locally, so this breakdown is an approximation using {resolved.Encoding} and may differ from the model. The exact token count is shown after you run the model.";

        return new TokenExplanation(
            token.Index,
            token.Id,
            token.Value,
            token.Start,
            token.End,
            bytes,
            leadingSpace,
            tiktokenWhy,
            Array.Empty<BpeMergeStep>(),
            splitProofs,
            resolved.Encoding,
            resolved.Exact);
    }

    private QwenBpeData? TryGetQwenData(string modelId)
    {
        var files = _qwen.TryFindTokenizerFiles(modelId);
        return TryLoadQwenData(files, modelId);
    }

    private QwenBpeData? TryGetBundledQwenData() => TryLoadQwenData(_qwen.BundledQwenFiles, "bundled-qwen");

    private QwenBpeData? TryLoadQwenData(QwenTokenizerFiles? files, string sourceLabel)
    {
        if (files is null)
        {
            return null;
        }

        try
        {
            return _qwenData.GetOrAdd(files.Directory, _ => LoadQwenData(files));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load BPE explanation data for {Source}", sourceLabel);
            return null;
        }
    }

    private static QwenBpeData LoadQwenData(QwenTokenizerFiles files)
    {
        using var vocabDoc = JsonDocument.Parse(File.ReadAllText(files.VocabPath));
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in vocabDoc.RootElement.EnumerateObject())
        {
            vocab[entry.Name] = entry.Value.GetInt32();
        }

        var ranks = new Dictionary<BpePair, int>();
        var rank = 0;
        foreach (var line in File.ReadLines(files.MergesPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                ranks[new BpePair(parts[0], parts[1])] = rank++;
            }
        }

        return new QwenBpeData(vocab, ranks);
    }

    private static IReadOnlyList<SplitProof> BuildSplitProofs(Tokenizer tokenizer, string text, TokenInfo token, string surface)
    {
        var proofs = new List<SplitProof>(2);
        if (token.End < text.Length)
        {
            var neighbor = text[token.End].ToString();
            proofs.Add(BuildSplitProof(tokenizer, "end", neighbor, surface + neighbor));
        }

        if (token.Start > 0)
        {
            var neighbor = text[token.Start - 1].ToString();
            proofs.Add(BuildSplitProof(tokenizer, "start", neighbor, neighbor + surface));
        }

        return proofs;
    }

    private static SplitProof BuildSplitProof(Tokenizer tokenizer, string direction, string neighbor, string extendedText)
    {
        var encoded = tokenizer.EncodeToTokens(extendedText, out _);
        var ids = encoded.Select(t => t.Id).ToArray();
        var explanation = encoded.Count > 1
            ? $"Re-encoding across the {direction} boundary produces {encoded.Count} tokens ({string.Join(", ", ids)}), proving this encoding has no single token covering the token plus neighbour here."
            : $"Re-encoding across the {direction} boundary still produces one token ({string.Join(", ", ids)}); this boundary depends on wider BPE context not exposed as merges.txt.";

        return new SplitProof(direction, neighbor, extendedText, ids, explanation);
    }

    private static string AddBoundaryNote(string why, string text, TokenInfo token)
    {
        if (token.End >= text.Length)
        {
            return why + " It ends at the end of the text.";
        }

        var next = text[token.End].ToString();
        return why + $" It ends before the next character '{next}' because BPE starts a new highest-ranked piece there.";
    }

    private static string SafeSlice(string text, int start, int end)
    {
        if (start < 0 || end < start || start > text.Length)
        {
            return string.Empty;
        }

        var safeEnd = Math.Min(end, text.Length);
        return text[start..safeEnd];
    }

    private sealed record QwenBpeData(
        IReadOnlyDictionary<string, int> Vocab,
        IReadOnlyDictionary<BpePair, int> MergeRanks);
}
