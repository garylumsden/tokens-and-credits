using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace TokensAndCredits.Web.Services.Tokenize;

/// <summary>Tokenizer file paths discovered for a Foundry Local byte-level BPE model.</summary>
/// <param name="Directory">Directory that contains tokenizer files.</param>
/// <param name="VocabPath">Path to vocab.json.</param>
/// <param name="MergesPath">Path to merges.txt.</param>
public sealed record QwenTokenizerFiles(string Directory, string VocabPath, string MergesPath);

/// <summary>
/// Loads a byte-level BPE tokenizer for a Foundry Local model by reading the
/// vocab.json + merges.txt that Foundry Local caches on disk. Qwen2 uses GPT-2
/// style byte-level BPE, so <see cref="CodeGenTokenizer"/> is the closest match
/// (treated as approximate — not guaranteed byte-perfect to the runtime).
/// </summary>
public sealed class QwenBpeFactory
{
    private readonly ConcurrentDictionary<string, Tokenizer?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<QwenBpeFactory> _logger;

    /// <summary>Creates a Qwen BPE factory.</summary>
    /// <param name="logger">Logger for non-fatal tokenizer load failures.</param>
    public QwenBpeFactory(ILogger<QwenBpeFactory> logger) => _logger = logger;

    private static string CacheRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".foundry", "cache", "models");

    /// <summary>Directory holding the bundled Qwen tokenizer (copied next to the app at build).</summary>
    private static string BundledDirectory => Path.Combine(AppContext.BaseDirectory, "Resources", "qwen");

    /// <summary>Returns a tokenizer for the model id, or null if its files can't be found.</summary>
    public Tokenizer? TryLoad(string modelId) => _cache.GetOrAdd(modelId, Load);

    /// <summary>
    /// Returns the bundled Qwen byte-level BPE tokenizer (Qwen2/2.5/3 share this BPE), or null if
    /// the bundled files are missing. Used to tokenise Qwen models exactly regardless of host
    /// (Foundry Local, LM Studio, Ollama), with no model call needed.
    /// </summary>
    public Tokenizer? TryLoadBundledQwen() => _cache.GetOrAdd("__bundled_qwen__", _ => LoadFromDirectory(BundledDirectory));

    /// <summary>Returns the bundled Qwen tokenizer file paths, or null if they aren't present.</summary>
    public QwenTokenizerFiles? BundledQwenFiles
    {
        get
        {
            var vocab = Path.Combine(BundledDirectory, "vocab.json");
            var merges = Path.Combine(BundledDirectory, "merges.txt");
            return File.Exists(vocab) && File.Exists(merges)
                ? new QwenTokenizerFiles(BundledDirectory, vocab, merges)
                : null;
        }
    }

    /// <summary>Finds the Qwen tokenizer directory and required BPE files for a model id.</summary>
    /// <param name="modelId">Foundry Local model id.</param>
    /// <returns>Tokenizer file paths when vocab.json and merges.txt are present; otherwise null.</returns>
    public QwenTokenizerFiles? TryFindTokenizerFiles(string modelId)
    {
        if (!Directory.Exists(CacheRoot))
        {
            return null;
        }

        // Match a vocab.json whose directory path corresponds to this model id.
        var vocab = Directory
            .EnumerateFiles(CacheRoot, "vocab.json", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Replace('\\', '/').Contains(modelId, StringComparison.OrdinalIgnoreCase));

        if (vocab is null)
        {
            return null;
        }

        var dir = Path.GetDirectoryName(vocab)!;
        var merges = Path.Combine(dir, "merges.txt");
        return File.Exists(merges)
            ? new QwenTokenizerFiles(dir, vocab, merges)
            : null;
    }

    private Tokenizer? Load(string modelId)
    {
        var files = TryFindTokenizerFiles(modelId);
        return files is null ? null : LoadFromFiles(files);
    }

    private Tokenizer? LoadFromDirectory(string directory)
    {
        var vocab = Path.Combine(directory, "vocab.json");
        var merges = Path.Combine(directory, "merges.txt");
        return File.Exists(vocab) && File.Exists(merges)
            ? LoadFromFiles(new QwenTokenizerFiles(directory, vocab, merges))
            : null;
    }

    private Tokenizer? LoadFromFiles(QwenTokenizerFiles files)
    {
        try
        {
            // Qwen uses GPT-2 style byte-level BPE. Its special tokens (e.g. <|endoftext|>)
            // live in tokenizer_config.json, not vocab.json, so load them explicitly to
            // avoid CodeGenTokenizer's "unknown token not found" failure.
            var options = new BpeOptions(files.VocabPath, files.MergesPath)
            {
                ByteLevel = true,
                SpecialTokens = ReadSpecialTokens(Path.Combine(files.Directory, "tokenizer_config.json"))
            };

            return BpeTokenizer.Create(options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Qwen BPE tokenizer from {Directory}", files.Directory);
            return null;
        }
    }

    private static IReadOnlyDictionary<string, int>? ReadSpecialTokens(string tokenizerConfigPath)
    {
        if (!File.Exists(tokenizerConfigPath))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(tokenizerConfigPath));
        if (!doc.RootElement.TryGetProperty("added_tokens_decoder", out var added))
        {
            return null;
        }

        var specials = new Dictionary<string, int>();
        foreach (var entry in added.EnumerateObject())
        {
            if (int.TryParse(entry.Name, out var id) &&
                entry.Value.TryGetProperty("content", out var content) &&
                content.GetString() is { Length: > 0 } token)
            {
                specials[token] = id;
            }
        }

        return specials.Count > 0 ? specials : null;
    }
}
