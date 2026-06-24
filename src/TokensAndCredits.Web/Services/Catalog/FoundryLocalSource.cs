using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TokensAndCredits.Web.Services.Models;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Services.Catalog;

/// <summary>
/// Talks to the locally running Foundry Local daemon (managed by the `foundry` CLI).
/// Lists downloaded models via `foundry cache list` (also boots the daemon), resolves
/// the daemon's OpenAI-compatible endpoint via `foundry server status`, and loads a
/// model via `foundry model load` before inference. All calls are defensive and
/// time-bounded: if the CLI/daemon is unavailable, the source returns nothing so the
/// cloud path still works.
/// </summary>
public sealed class FoundryLocalSource
{
    private static readonly TimeSpan EndpointTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(60);
    // Loading a large model onto the NPU (copy + first-time OpenVINO compile) can take a
    // very long time, so give it a generous ceiling.
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromMinutes(30);

    private readonly ILogger<FoundryLocalSource> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _endpoint;
    private DateTimeOffset _endpointAt = DateTimeOffset.MinValue;

    public FoundryLocalSource(ILogger<FoundryLocalSource> logger) => _logger = logger;

    /// <summary>Returns the daemon's OpenAI-compatible base endpoint, or null if unavailable.</summary>
    public async Task<string?> GetEndpointAsync(CancellationToken ct)
    {
        if (_endpoint is not null && DateTimeOffset.UtcNow - _endpointAt < EndpointTtl)
        {
            return _endpoint;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_endpoint is not null && DateTimeOffset.UtcNow - _endpointAt < EndpointTtl)
            {
                return _endpoint;
            }

            _endpoint = await ResolveEndpointAsync(ct);
            _endpointAt = DateTimeOffset.UtcNow;
            return _endpoint;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lists downloaded (cached) local models as selectable descriptors.</summary>
    public async Task<IReadOnlyList<ModelDescriptor>> ListAsync(CancellationToken ct)
    {
        try
        {
            // Ensure the daemon is up first so `cache list` doesn't have to boot it
            // (a booting CLI keeps the daemon attached to stdout and would block capture).
            var endpoint = await GetEndpointAsync(ct);
            if (endpoint is null)
            {
                return Array.Empty<ModelDescriptor>();
            }

            var (exitCode, output) = await RunFoundryAsync(
                new[] { "cache", "list", "--output", "json" }, ct, captureOutput: true);
            if (exitCode != 0)
            {
                return Array.Empty<ModelDescriptor>();
            }

            var json = ExtractJsonObject(output);
            if (json is null)
            {
                return Array.Empty<ModelDescriptor>();
            }

            var parsed = JsonSerializer.Deserialize<CacheListResponse>(json);
            var models = parsed?.Models ?? Array.Empty<CacheModel>();

            return models
                .Where(m => m.Cached && !string.IsNullOrWhiteSpace(m.DisplayName))
                .Select(ToDescriptor)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            // Expected when the Foundry Local daemon isn't running (or is slow to boot) and the
            // triggering request is cancelled. Not an error — cloud models still load.
            _logger.LogDebug("Foundry Local discovery cancelled (daemon unavailable or request aborted).");
            return Array.Empty<ModelDescriptor>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list Foundry Local models.");
            return Array.Empty<ModelDescriptor>();
        }
    }

    /// <summary>Loads the model into memory via the CLI (required before inference).</summary>
    public async Task EnsureLoadedAsync(string modelId, CancellationToken ct)
    {
        // No output capture: `model load` keeps the daemon attached to stdout, which would
        // otherwise block a captured read. We only need the exit code here. Large models can
        // take minutes to load onto the NPU, so use the longer load timeout.
        var (exitCode, output) = await RunFoundryAsync(
            new[] { "model", "load", modelId }, ct, captureOutput: false, timeout: LoadTimeout);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to load local model '{modelId}': {output}");
        }
    }

    private static ModelDescriptor ToDescriptor(CacheModel model)
    {
        var device = (model.Device ?? "").ToUpperInvariant() switch
        {
            "NPU" => "NPU",
            "GPU" => "GPU",
            _ => "CPU"
        };

        // displayName matches the id the OpenAI-compatible endpoint expects. Qwen models are
        // tokenised exactly with the bundled Qwen BPE; other local models are approximate.
        var id = model.DisplayName!;
        var isQwen = ModelFamilyDetector.Detect(id) == ModelFamily.Qwen;
        return new ModelDescriptor(
            Id: id,
            Label: model.Alias ?? id,
            Source: ModelSource.FoundryLocal,
            Device: device,
            Encoding: isQwen ? "Qwen byte-level BPE" : "o200k_base (approx)",
            SupportsReasoning: false,
            SupportsCaching: false,
            Exact: isQwen);
    }

    private async Task<string?> ResolveEndpointAsync(CancellationToken ct)
    {
        var url = await ReadRunningEndpointAsync(ct);
        if (url is not null)
        {
            return url;
        }

        // Boot the daemon without the (blocking) `server start`: `cache list` starts it
        // and returns. No capture: a booting CLI keeps the daemon attached to stdout.
        _logger.LogInformation("Foundry Local daemon not running; booting it via cache list.");
        await RunFoundryAsync(new[] { "cache", "list", "--output", "json" }, ct, captureOutput: false);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            url = await ReadRunningEndpointAsync(ct);
            if (url is not null)
            {
                return url;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        return null;
    }

    private async Task<string?> ReadRunningEndpointAsync(CancellationToken ct)
    {
        var (exitCode, output) = await RunFoundryAsync(
            new[] { "server", "status", "--output", "json" }, ct, captureOutput: true);
        if (exitCode != 0)
        {
            return null;
        }

        var json = ExtractJsonObject(output);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // webUrls is reported even when the daemon is stopped, so gate on `running`.
            var running = root.TryGetProperty("running", out var r) && r.ValueKind == JsonValueKind.True;
            if (running && root.TryGetProperty("webUrls", out var urls) && urls.GetArrayLength() > 0)
            {
                return urls[0].GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse Foundry Local status output.");
        }

        return null;
    }

    private static string? ExtractJsonObject(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        return start >= 0 && end > start ? output[start..(end + 1)] : null;
    }

    private async Task<(int ExitCode, string Output)> RunFoundryAsync(string[] args, CancellationToken ct, bool captureOutput, TimeSpan? timeout = null)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout ?? CliTimeout);

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo("foundry")
            {
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start 'foundry'.");

            if (!captureOutput)
            {
                // The CLI may leave a spawned daemon attached to the inherited streams;
                // wait for the CLI itself to exit and return only the exit code.
                await process.WaitForExitAsync(timeoutCts.Token);
                return (process.ExitCode, string.Empty);
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            return (process.ExitCode, string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Foundry CLI invocation failed: foundry {Args}", string.Join(' ', args));
            TryKill(process);
            return (-1, ex.Message);
        }
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private sealed record CacheListResponse([property: JsonPropertyName("models")] CacheModel[]? Models);

    private sealed record CacheModel(
        [property: JsonPropertyName("alias")] string? Alias,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("device")] string? Device,
        [property: JsonPropertyName("cached")] bool Cached,
        [property: JsonPropertyName("loaded")] bool Loaded);
}
