using System.Globalization;
using System.Text;
using Adjudge.Providers;

namespace Adjudge.OpenAI;

internal static class OpenAIPromptBuilder
{
    public const string SystemMessage =
        "You are a classifier. You answer with a single label from the list you are given and nothing else. " +
        "No punctuation, no explanation, no restatement of the label's meaning.";

    public const string TrueLabel = "yes";

    public const string FalseLabel = "no";

    private const int MaximumOptions = 26;

    private const int MinimumLevels = 2;

    private const int MaximumLevels = 10;

    public static QuestionPlan Build(QuestionSpec question, string state) => question switch
    {
        ClassifySpec classify => Classify(classify, state),
        RateSpec rate => Rate(rate, state),
        AssertSpec assert => Assert(assert, state),
        _ => throw new DecisionDefinitionException(
            $"The OpenAI provider does not support question '{question.Name}' of type '{question.GetType().Name}'."),
    };

    private static QuestionPlan Classify(ClassifySpec classify, string state)
    {
        if (classify.Options.Count is 0 or > MaximumOptions)
        {
            throw new DecisionDefinitionException(
                $"Question '{classify.Name}' must declare between 1 and {MaximumOptions} options but declares {classify.Options.Count}. " +
                "The OpenAI provider labels options A to Z so that each label is a single token.");
        }

        var labels = new List<LabelledChoice>(classify.Options.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < classify.Options.Count; index++)
        {
            var option = classify.Options[index];
            if (!seen.Add(option.Key))
            {
                throw new DecisionDefinitionException($"Question '{classify.Name}' declares option '{option.Key}' more than once.");
            }

            labels.Add(new LabelledChoice(((char)('A' + index)).ToString(), option.Key));
        }

        var builder = Header(classify.Instructions);
        builder.AppendLine("Options:");
        for (var index = 0; index < classify.Options.Count; index++)
        {
            AppendChoice(builder, labels[index].Label, classify.Options[index].Rubric, $"{classify.Name}.{classify.Options[index].Key}");
        }

        builder.AppendLine();
        AppendState(builder, state);
        AppendFooter(builder, labels);
        return new QuestionPlan(classify, builder.ToString(), labels);
    }

    private static QuestionPlan Rate(RateSpec rate, string state)
    {
        if (rate.Levels.Count is < MinimumLevels or > MaximumLevels)
        {
            throw new DecisionDefinitionException(
                $"Question '{rate.Name}' must declare between {MinimumLevels} and {MaximumLevels} levels but declares {rate.Levels.Count}.");
        }

        var labels = new List<LabelledChoice>(rate.Levels.Count);
        for (var index = 0; index < rate.Levels.Count; index++)
        {
            labels.Add(new LabelledChoice(index.ToString(CultureInfo.InvariantCulture), rate.Levels[index].Key));
        }

        var builder = Header(rate.Instructions);
        builder.AppendLine("Levels, from lowest to highest:");
        for (var index = 0; index < rate.Levels.Count; index++)
        {
            AppendChoice(builder, labels[index].Label, rate.Levels[index].Rubric, $"{rate.Name}.{rate.Levels[index].Key}");
        }

        builder.AppendLine();
        AppendState(builder, state);
        AppendFooter(builder, labels);
        return new QuestionPlan(rate, builder.ToString(), labels);
    }

    private static QuestionPlan Assert(AssertSpec assert, string state)
    {
        var labels = new List<LabelledChoice>
        {
            new(TrueLabel, TrueLabel),
            new(FalseLabel, FalseLabel),
        };

        var builder = Header(assert.Instructions);
        if (assert.TrueMeans is not null || assert.FalseMeans is not null)
        {
            builder.AppendLine("Answers:");
            if (assert.TrueMeans is { } trueMeans)
            {
                builder.Append(TrueLabel).Append(" = ").AppendLine(Flatten(trueMeans));
            }

            if (assert.FalseMeans is { } falseMeans)
            {
                builder.Append(FalseLabel).Append(" = ").AppendLine(Flatten(falseMeans));
            }

            builder.AppendLine();
        }

        AppendState(builder, state);
        AppendFooter(builder, labels);
        return new QuestionPlan(assert, builder.ToString(), labels);
    }

    // The instructions and the options come first and the state last, so the label line the model is
    // meant to obey is the last thing it reads.
    private static StringBuilder Header(string instructions)
    {
        var builder = new StringBuilder();
        builder.Append("Question: ").AppendLine(Flatten(instructions));
        builder.AppendLine();
        return builder;
    }

    private static void AppendState(StringBuilder builder, string state)
    {
        builder.AppendLine("State:");
        builder.AppendLine("```json");
        builder.AppendLine(state);
        builder.AppendLine("```");
        builder.AppendLine();
    }

    private static void AppendChoice(StringBuilder builder, string label, object rubric, string path)
    {
        switch (rubric)
        {
            case string text:
                builder.Append(label).Append(" = ").AppendLine(Flatten(text));
                break;
            case OptionRubric option:
                builder.Append(label).Append(" = ").AppendLine(Flatten(option.Description));
                if (option.NotFor is { } notFor)
                {
                    builder.Append("    Not for: ").AppendLine(Flatten(notFor));
                }

                if (option.Examples is { Count: > 0 } examples)
                {
                    builder.Append("    Examples: ").AppendLine(string.Join("; ", examples.Select(Flatten)));
                }

                break;
            default:
                throw new DecisionDefinitionException(
                    $"Cannot render '{path}' of type '{rubric.GetType()}'. Supply a string or an {nameof(OptionRubric)}.");
        }
    }

    private static void AppendFooter(StringBuilder builder, IReadOnlyList<LabelledChoice> labels)
    {
        builder.Append("Reply with exactly one label: ").Append(string.Join(", ", labels.Select(label => label.Label))).Append('.');
    }

    // A rubric written as a multi-line verbatim string would otherwise break the one-line-per-label
    // shape the prompt relies on.
    private static string Flatten(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = true;
                continue;
            }

            if (space && builder.Length > 0)
            {
                builder.Append(' ');
            }

            space = false;
            builder.Append(character);
        }

        return builder.ToString();
    }
}
