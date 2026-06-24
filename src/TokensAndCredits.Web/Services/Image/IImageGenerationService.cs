using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Image;

public interface IImageGenerationService
{
    Task<ImageGenerationResult> GenerateAsync(
        ModelDescriptor model,
        string prompt,
        string size,
        string quality,
        CancellationToken ct);
}

public sealed record ImageGenerationResult(
    string ImageBase64,
    string ImageMediaType,
    ImageUsageBreakdown Usage);
