namespace TokensAndCredits.Web.Services.Embeddings;

public sealed record EmbeddingComparisonInput(
    string First,
    string Second,
    string Third,
    string Target)
{
    public const int MaxInputLength = 512;

    public static bool TryCreate(
        string? first,
        string? second,
        string? third,
        string? target,
        out EmbeddingComparisonInput? input,
        out string? error)
    {
        var values = new[]
        {
            (Name: "first", Value: first?.Trim()),
            (Name: "second", Value: second?.Trim()),
            (Name: "third", Value: third?.Trim()),
            (Name: "target", Value: target?.Trim()),
        };

        foreach (var value in values)
        {
            if (string.IsNullOrEmpty(value.Value))
            {
                input = null;
                error = $"Field '{value.Name}' is required.";
                return false;
            }

            if (value.Value.Length > MaxInputLength)
            {
                input = null;
                error = $"Field '{value.Name}' must not exceed {MaxInputLength} characters.";
                return false;
            }
        }

        if (values.Select(value => value.Value!).Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            input = null;
            error = "First, second, third, and target must be distinct.";
            return false;
        }

        input = new EmbeddingComparisonInput(
            values[0].Value!,
            values[1].Value!,
            values[2].Value!,
            values[3].Value!);
        error = null;
        return true;
    }
}
