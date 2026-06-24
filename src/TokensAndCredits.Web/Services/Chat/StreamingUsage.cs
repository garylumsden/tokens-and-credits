using Microsoft.Extensions.AI;

namespace TokensAndCredits.Web.Services.Chat;

/// <summary>Helpers for deriving usage from a streamed chat response.</summary>
public static class StreamingUsage
{
    /// <summary>
    /// Returns the final reported usage across streamed updates. Some local servers
    /// (llama.cpp / Foundry Local) emit a <c>usage</c> block in EVERY streamed chunk, so
    /// <c>ToChatResponse().Usage</c> adds them all together and massively inflates the totals
    /// (e.g. a ~4k-token reply showing millions of output tokens). The usage in the last chunk is
    /// the correct cumulative total, so take that rather than the sum.
    /// </summary>
    /// <param name="updates">The streamed response updates, in order.</param>
    /// <returns>The last <see cref="UsageDetails"/> seen, or null if no usage was reported.</returns>
    public static UsageDetails? Final(IEnumerable<ChatResponseUpdate> updates)
    {
        UsageDetails? last = null;
        foreach (var update in updates)
        {
            foreach (var content in update.Contents)
            {
                if (content is UsageContent usage)
                {
                    last = usage.Details;
                }
            }
        }

        return last;
    }
}
