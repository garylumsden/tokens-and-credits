using TokensAndCredits.Web.Services.Image;

namespace TokensAndCredits.Web.Tests;

public sealed class ImageUsageParserTests
{
    [Fact]
    public void ParseRaw_SeparatesInputAndOutputTokenDetails()
    {
        var content = BinaryData.FromString(
            """
            {
              "usage": {
                "input_tokens": 192,
                "output_tokens": 1912,
                "input_tokens_details": {
                  "text_tokens": 192,
                  "image_tokens": 0
                },
                "output_tokens_details": {
                  "text_tokens": 856,
                  "image_tokens": 1056
                }
              }
            }
            """);

        var usage = ImageUsageParser.ParseRaw(content);

        Assert.NotNull(usage);
        Assert.Equal(192, usage.InputTokens);
        Assert.Equal(1912, usage.OutputTokens);
        Assert.Equal(192, usage.TextTokens);
        Assert.Equal(0, usage.ImageTokens);
        Assert.Equal(856, usage.OutputTextTokens);
        Assert.Equal(1056, usage.OutputImageTokens);
    }
}
