using System.Text.Json.Nodes;
using Adjudge.Providers;

namespace Adjudge.Jev;

internal static class JevRequestMapper
{
    public const int MinimumLevels = 2;

    public const int MaximumLevels = 10;

    public const int MaximumOptions = 255;

    public static JevRequestPayload Map(ProviderRequest request, JevOptions options)
    {
        var payload = new JevRequestPayload
        {
            State = request.Context.AsJson(),
            Model = options.ResolveModel(),
        };

        foreach (var question in request.Questions)
        {
            if (!payload.Questions.TryAdd(question.Name, MapQuestion(question)))
            {
                throw new DecisionDefinitionException($"Question '{question.Name}' was supplied more than once.");
            }
        }

        return payload;
    }

    private static JevQuestionPayload MapQuestion(QuestionSpec question)
    {
        var instructions = JsonValue.Create(question.Instructions);

        return question switch
        {
            ClassifySpec classify => new JevQuestionPayload
            {
                Type = "choice",
                Instructions = instructions,
                Criteria = Choice(classify),
            },
            RateSpec rate => new JevQuestionPayload
            {
                Type = "score",
                Instructions = instructions,
                Criteria = Score(rate),
            },
            AssertSpec assert => new JevQuestionPayload
            {
                Type = "noul",
                Instructions = instructions,
                Criteria = Noul(assert),
            },
            _ => throw new DecisionDefinitionException(
                $"The Jev provider does not support question '{question.Name}' of type '{question.GetType().Name}'."),
        };
    }

    private static JsonObject Choice(ClassifySpec classify)
    {
        if (classify.Options.Count is 0 or > MaximumOptions)
        {
            throw new DecisionDefinitionException(
                $"Question '{classify.Name}' must declare between 1 and {MaximumOptions} options but declares {classify.Options.Count}.");
        }

        var criteria = new JsonObject();
        foreach (var option in classify.Options)
        {
            if (!criteria.TryAdd(option.Key, JevRubric.ToNode(option.Rubric, $"{classify.Name}.{option.Key}")))
            {
                throw new DecisionDefinitionException($"Question '{classify.Name}' declares option '{option.Key}' more than once.");
            }
        }

        return criteria;
    }

    private static JsonArray Score(RateSpec rate)
    {
        if (rate.Levels.Count is < MinimumLevels or > MaximumLevels)
        {
            throw new DecisionDefinitionException(
                $"Question '{rate.Name}' must declare between {MinimumLevels} and {MaximumLevels} levels but declares {rate.Levels.Count}.");
        }

        var criteria = new JsonArray();
        foreach (var level in rate.Levels)
        {
            criteria.Add(JevRubric.ToNode(level.Rubric, $"{rate.Name}.{level.Key}"));
        }

        return criteria;
    }

    private static JsonObject? Noul(AssertSpec assert)
    {
        if (assert.TrueMeans is null && assert.FalseMeans is null)
        {
            return null;
        }

        var criteria = new JsonObject();
        if (assert.TrueMeans is { } trueMeans)
        {
            criteria["true"] = JsonValue.Create(trueMeans);
        }

        if (assert.FalseMeans is { } falseMeans)
        {
            criteria["false"] = JsonValue.Create(falseMeans);
        }

        return criteria;
    }
}
