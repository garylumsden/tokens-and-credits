#pragma warning disable OPENAI001

using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Images;
using TokensAndCredits.Web.Services.Catalog;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Image;

public sealed class ImageGenerationService : IImageGenerationService
{
    private const string AzureScope = "https://cognitiveservices.azure.com/.default";
    private const string PngMediaType = "image/png";

    private readonly AzureFoundryOptions _azureOptions;
    private readonly ConcurrentDictionary<string, ImageClient> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly DefaultAzureCredential _credential = new();

    public ImageGenerationService(IOptions<AzureFoundryOptions> azureOptions)
    {
        _azureOptions = azureOptions.Value;
    }

    public async Task<ImageGenerationResult> GenerateAsync(
        ModelDescriptor model,
        string prompt,
        string size,
        string quality,
        CancellationToken ct)
    {
        if (model.Source != ModelSource.AzureFoundry)
        {
            throw new NotSupportedException("Image generation is available for Azure Foundry deployments only.");
        }

        var client = CreateAzure(model);
        // gpt-image models always return base64 image data and REJECT the response_format
        // parameter (it's a DALL·E-only option), so we must not set ResponseFormat here.
        var options = new ImageGenerationOptions
        {
            Size = ParseSize(size),
            Quality = ParseQuality(quality)
        };

        var response = await client.GenerateImagesAsync(prompt, imageCount: 1, options, ct);
        var images = response.Value;
        var image = images.FirstOrDefault()
            ?? throw new InvalidOperationException("Image generation returned no images.");

        var imageBytes = image.ImageBytes?.ToArray();
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new InvalidOperationException("Image generation response did not include image bytes.");
        }

        var usage = TryParseRawUsage(response.GetRawResponse().Content)
            ?? ImageUsageParser.FromTyped(images.Usage)
            ?? ImageUsageParser.Empty;

        return new ImageGenerationResult(
            Convert.ToBase64String(imageBytes),
            PngMediaType,
            usage);
    }

    public static bool IsSupportedSize(string? size) => TryParseSize(size, out _);

    public static bool IsSupportedQuality(string? quality) => TryParseQuality(quality, out _);

    private ImageClient CreateAzure(ModelDescriptor model)
    {
        return _cache.GetOrAdd($"azure:{model.Id}", _ =>
        {
            if (string.IsNullOrWhiteSpace(_azureOptions.Endpoint))
            {
                throw new InvalidOperationException("Azure Foundry endpoint is not configured.");
            }

            var baseEndpoint = _azureOptions.Endpoint.TrimEnd('/');
            var options = new OpenAIClientOptions { Endpoint = new Uri($"{baseEndpoint}/openai/v1") };
            var authPolicy = new BearerTokenPolicy(_credential, AzureScope);
            return new ImageClient(model.Id, authPolicy, options);
        });
    }

    private static ImageUsageBreakdown? TryParseRawUsage(BinaryData content)
    {
        try
        {
            return ImageUsageParser.ParseRaw(content);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static GeneratedImageSize ParseSize(string size)
    {
        if (TryParseSize(size, out var parsed))
        {
            return parsed!.Value;
        }

        throw new ArgumentException($"Unsupported image size '{size}'.", nameof(size));
    }

    private static bool TryParseSize(string? size, out GeneratedImageSize? parsed)
    {
        parsed = null;
        switch (Normalize(size))
        {
            case "auto":
                parsed = GeneratedImageSize.Auto;
                return true;
            case "1024x1024":
                parsed = GeneratedImageSize.W1024xH1024;
                return true;
            case "1024x1536":
                parsed = GeneratedImageSize.W1024xH1536;
                return true;
            case "1536x1024":
                parsed = GeneratedImageSize.W1536xH1024;
                return true;
            default:
                return false;
        }
    }

    private static GeneratedImageQuality ParseQuality(string quality)
    {
        if (TryParseQuality(quality, out var parsed))
        {
            return parsed!.Value;
        }

        throw new ArgumentException($"Unsupported image quality '{quality}'.", nameof(quality));
    }

    private static bool TryParseQuality(string? quality, out GeneratedImageQuality? parsed)
    {
        parsed = null;
        switch (Normalize(quality))
        {
            case "auto":
                parsed = GeneratedImageQuality.Auto;
                return true;
            case "low":
                parsed = GeneratedImageQuality.LowQuality;
                return true;
            case "medium":
                parsed = GeneratedImageQuality.MediumQuality;
                return true;
            case "high":
                parsed = GeneratedImageQuality.HighQuality;
                return true;
            default:
                return false;
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
}
