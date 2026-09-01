namespace TokensAndCredits.Web.Services.Embeddings;

public static class EmbeddingVectorMath
{
    public static double Dot(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        ValidateLengths(left, right);

        double value = 0;
        for (var index = 0; index < left.Length; index++)
        {
            if (!float.IsFinite(left[index]) || !float.IsFinite(right[index]))
            {
                throw new ArgumentException("Vectors must contain only finite values.");
            }

            value += (double)left[index] * right[index];
        }

        if (!double.IsFinite(value))
        {
            throw new ArgumentException("The dot product must be finite.");
        }

        return value;
    }

    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        if (vector.IsEmpty)
        {
            throw new ArgumentException("The vector must contain at least one value.", nameof(vector));
        }

        double squaredMagnitude = 0;
        foreach (var value in vector)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentException("The vector must contain only finite values.", nameof(vector));
            }

            squaredMagnitude += (double)value * value;
        }

        if (!double.IsFinite(squaredMagnitude) || squaredMagnitude <= 0)
        {
            throw new ArgumentException("The vector magnitude must be finite and greater than zero.", nameof(vector));
        }

        var magnitude = Math.Sqrt(squaredMagnitude);
        var normalized = new float[vector.Length];
        for (var index = 0; index < vector.Length; index++)
        {
            normalized[index] = (float)(vector[index] / magnitude);
        }

        return normalized;
    }

    public static double Cosine(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        ValidateLengths(left, right);

        double dot = 0;
        double leftSquaredMagnitude = 0;
        double rightSquaredMagnitude = 0;

        for (var index = 0; index < left.Length; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];
            if (!float.IsFinite(leftValue) || !float.IsFinite(rightValue))
            {
                throw new ArgumentException("Vectors must contain only finite values.");
            }

            dot += (double)leftValue * rightValue;
            leftSquaredMagnitude += (double)leftValue * leftValue;
            rightSquaredMagnitude += (double)rightValue * rightValue;
        }

        if (leftSquaredMagnitude <= 0 || rightSquaredMagnitude <= 0)
        {
            throw new ArgumentException("Vector magnitudes must be greater than zero.");
        }

        var cosine = dot / Math.Sqrt(leftSquaredMagnitude * rightSquaredMagnitude);
        if (!double.IsFinite(cosine))
        {
            throw new ArgumentException("The cosine result must be finite.");
        }

        return Math.Clamp(cosine, -1, 1);
    }

    public static double AngleDegrees(ReadOnlySpan<float> left, ReadOnlySpan<float> right) =>
        Math.Acos(Cosine(left, right)) * 180 / Math.PI;

    private static void ValidateLengths(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.IsEmpty || left.Length != right.Length)
        {
            throw new ArgumentException("Vectors must have the same nonzero dimensions.");
        }
    }
}
