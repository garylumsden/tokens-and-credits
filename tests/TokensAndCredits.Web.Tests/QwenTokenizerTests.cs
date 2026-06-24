using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.Tokenizers;
using TokensAndCredits.Web.Services.Tokenize;

namespace TokensAndCredits.Web.Tests;

public sealed class QwenTokenizerTests
{
    [Theory]
    [InlineData("qwen3:4b", ModelFamily.Qwen)]
    [InlineData("qwen/qwen3-8b", ModelFamily.Qwen)]
    [InlineData("Qwen2.5-Coder-7B", ModelFamily.Qwen)]
    [InlineData("llama3.2:3b", ModelFamily.Unknown)]
    [InlineData("google/gemma-3-4b", ModelFamily.Unknown)]
    [InlineData("", ModelFamily.Unknown)]
    public void Detect_IdentifiesQwenFamily(string id, ModelFamily expected)
    {
        Assert.Equal(expected, ModelFamilyDetector.Detect(id));
    }

    [Fact]
    public void BundledQwenTokenizer_LoadsAndTokenizesDistinctlyFromO200k()
    {
        var factory = new QwenBpeFactory(NullLogger<QwenBpeFactory>.Instance);
        var qwen = factory.TryLoadBundledQwen();

        Assert.NotNull(qwen);

        // It tokenizes ordinary text into a sensible number of tokens.
        var ids = qwen!.EncodeToIds("lowest tokenization");
        Assert.True(ids.Count is > 0 and < 10);

        // The Qwen vocabulary is different from OpenAI's o200k_base, so a CJK string that
        // o200k splits one way should generally differ under Qwen — proving we're really using
        // the model's own tokenizer rather than the OpenAI fallback.
        var o200k = TiktokenTokenizer.CreateForEncoding("o200k_base");
        var qwenCjk = qwen.EncodeToIds("\u4f60\u597d\u4e16\u754c");
        var o200kCjk = o200k.EncodeToIds("\u4f60\u597d\u4e16\u754c");
        Assert.NotEqual(o200kCjk, qwenCjk);
    }

    [Fact]
    public void MergeTrace_OverBundledQwen_IsNotRankExact()
    {
        var qwen = new QwenBpeFactory(NullLogger<QwenBpeFactory>.Instance).TryLoadBundledQwen();
        Assert.NotNull(qwen);

        // Qwen uses merges.txt BPE: the vocab id is a rank proxy, so IdIsRank must be false even
        // though the final tokens still verify.
        var trace = new MergeTracer().Trace(qwen!, "lowest");
        Assert.False(trace.IdIsRank);
    }
}
