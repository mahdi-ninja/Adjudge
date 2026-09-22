using System.Globalization;
using System.Text;

namespace Adjudge.OpenAI.Tests;

internal static class ChatResponses
{
    public const string DefaultModel = "gpt-4o-mini-2024-07-18";

    public static string Plain(
        string content,
        string model = DefaultModel,
        long input = 10,
        long output = 1,
        string finishReason = "stop",
        bool reportUsage = true) =>
        Build(content, tokens: null, model, input, output, finishReason, reportUsage);

    public static string WithLogProbabilities(
        string content,
        (string Token, double LogProbability)[] top,
        (string Token, double LogProbability)? chosen = null,
        string model = DefaultModel,
        long input = 10,
        long output = 1,
        string[]? leadingTokens = null,
        bool reportUsage = true) =>
        Build(
            content,
            [.. (leadingTokens ?? []).Select(token => new Token(token, 0, [(token, 0)])), new Token((chosen ?? top[0]).Token, (chosen ?? top[0]).LogProbability, top)],
            model,
            input,
            output,
            finishReason: "stop",
            reportUsage);

    public static string WithoutLogProbabilities(string content, string finishReason) =>
        Build(content, tokens: [], DefaultModel, input: 10, output: 4, finishReason, reportUsage: true);

    public static (string Token, double LogProbability)[] Tops(params (string Token, double Probability)[] candidates) =>
        [.. candidates.Select(candidate => (candidate.Token, Math.Log(candidate.Probability)))];

    private static string Build(
        string content,
        Token[]? tokens,
        string model,
        long input,
        long output,
        string finishReason,
        bool reportUsage)
    {
        var builder = new StringBuilder();
        builder.Append("{\"id\":\"chatcmpl-test\",\"object\":\"chat.completion\",\"created\":1700000000,\"model\":")
            .Append(Quote(model))
            .Append(",\"choices\":[{\"index\":0,\"finish_reason\":")
            .Append(Quote(finishReason))
            .Append(",\"message\":{\"role\":\"assistant\",\"content\":")
            .Append(Quote(content))
            .Append("},\"logprobs\":");

        if (tokens is null)
        {
            builder.Append("null");
        }
        else
        {
            builder.Append("{\"content\":[");
            for (var index = 0; index < tokens.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendToken(builder, tokens[index]);
            }

            builder.Append("]}");
        }

        builder.Append("}]");

        if (reportUsage)
        {
            builder.Append(",\"usage\":{\"prompt_tokens\":")
                .Append(input.ToString(CultureInfo.InvariantCulture))
                .Append(",\"completion_tokens\":")
                .Append(output.ToString(CultureInfo.InvariantCulture))
                .Append(",\"total_tokens\":")
                .Append((input + output).ToString(CultureInfo.InvariantCulture))
                .Append('}');
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendToken(StringBuilder builder, Token token)
    {
        builder.Append("{\"token\":")
            .Append(Quote(token.Text))
            .Append(",\"logprob\":")
            .Append(Number(token.LogProbability))
            .Append(",\"bytes\":null,\"top_logprobs\":[");

        for (var index = 0; index < token.Top.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"token\":")
                .Append(Quote(token.Top[index].Token))
                .Append(",\"logprob\":")
                .Append(Number(token.Top[index].LogProbability))
                .Append(",\"bytes\":null}");
        }

        builder.Append("]}");
    }

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record Token(string Text, double LogProbability, (string Token, double LogProbability)[] Top);
}
