using TokensAndCredits.Web.Services.Catalog;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Tests;

public sealed class OpenAiCompatibleModelParserTests
{
    [Fact]
    public void Parse_OpenAiModelsResponse_MapsDescriptors()
    {
        const string json = """
            {
              "data": [
                { "id": "llama3" },
                { "id": " qwen2.5 " },
                { "id": "" },
                { "id": "llama3" }
              ]
            }
            """;

        var descriptors = OpenAiCompatibleModelParser.Parse(json, ModelSource.Ollama);

        Assert.Collection(
            descriptors,
            first =>
            {
                Assert.Equal("llama3", first.Id);
                Assert.Equal("llama3", first.Label);
                Assert.Equal(ModelSource.Ollama, first.Source);
                Assert.Equal("local", first.Device);
                Assert.Equal("o200k_base (approx)", first.Encoding);
                Assert.False(first.SupportsReasoning);
                Assert.False(first.SupportsCaching);
                Assert.False(first.SupportsLogprobs);
                Assert.False(first.Exact);
                Assert.Equal(Modality.Text, first.Modality);
            },
            second => Assert.Equal("qwen2.5", second.Id));
    }
}
