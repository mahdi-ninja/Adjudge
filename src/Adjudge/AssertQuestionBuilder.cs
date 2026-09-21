namespace Adjudge;

/// <summary>Refines a proposition after <c>Assert</c> has declared it. Every method returns the same builder, so calls chain.</summary>
public sealed class AssertQuestionBuilder
{
    private readonly AssertBinding _binding;

    internal AssertQuestionBuilder(AssertBinding binding) => _binding = binding;

    /// <summary>Spells out what counts as a yes, which sharpens the boundary far more than the question alone does.</summary>
    /// <exception cref="ArgumentException"><paramref name="meaning"/> is null, empty or whitespace.</exception>
    public AssertQuestionBuilder True(string meaning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meaning);

        _binding.TrueMeans = meaning;
        return this;
    }

    /// <summary>Spells out what counts as a no.</summary>
    /// <exception cref="ArgumentException"><paramref name="meaning"/> is null, empty or whitespace.</exception>
    public AssertQuestionBuilder False(string meaning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meaning);

        _binding.FalseMeans = meaning;
        return this;
    }
}
