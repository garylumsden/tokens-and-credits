#pragma warning disable OPENAI001

using System.Text.Json;
using OpenAI.Images;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Image;

internal static class ImageUsageParser
{
    public static ImageUsageBreakdown Empty { get; } = new(null, null, null, null);

    public static ImageUsageBreakdown? FromTyped(ImageTokenUsage? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return new ImageUsageBreakdown(
            ToNullableInt(usage.InputTokenCount),
            ToNullableInt(usage.OutputTokenCount),
            ToNullableInt(usage.InputTokenDetails?.TextTokenCount),
            ToNullableInt(usage.InputTokenDetails?.ImageTokenCount));
    }

    public static ImageUsageBreakdown? ParseRaw(BinaryData? content)
    {
        if (content is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("usage", out var usage) ||
                usage.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            int? textTokens = null;
            int? imageTokens = null;
            if (usage.TryGetProperty("input_tokens_details", out var details) &&
                details.ValueKind == JsonValueKind.Object)
            {
                textTokens = GetInt(details, "text_tokens");
                imageTokens = GetInt(details, "image_tokens");
            }

            return new ImageUsageBreakdown(
                GetInt(usage, "input_tokens"),
                GetInt(usage, "output_tokens"),
                textTokens,
                imageTokens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return ToNullableInt(value);
        }

        return null;
    }

    private static int? ToNullableInt(long? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value.Value;
    }
}
