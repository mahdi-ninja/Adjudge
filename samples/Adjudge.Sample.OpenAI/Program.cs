using System.Globalization;
using Adjudge;
using Adjudge.OpenAI;
using Adjudge.Sample.OpenAI;

var missing = new[] { OpenAIOptions.ApiKeyVariable, OpenAIOptions.BaseUrlVariable, OpenAIOptions.ModelVariable }
    .Where(variable => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
    .ToArray();

if (missing.Length > 0)
{
    Console.Error.WriteLine($"Set {string.Join(", ", missing)} and run the sample again.");
    return 1;
}

var options = new OpenAIOptions();
if (args.Length > 0 && string.Equals(args[0], "sampling", StringComparison.OrdinalIgnoreCase))
{
    options.Strategy = ConfidenceStrategy.Sampling;
}

var engine = new DecisionEngine(new OpenAIProvider(options));
var triage = engine.Create(new TicketTriageDecision());

var tickets = new TicketContext[]
{
    new("I was charged twice for order 4471 and nobody has replied in a week.", "4471"),
    new("Your useless support team are a bunch of clowns. Fix it now.", null),
    new("Hi, quick question about my thing from last month.", null),
};

foreach (var ticket in tickets)
{
    var result = await triage.DecideAsync(ticket).ConfigureAwait(false);
    var answer = result.Value;

    Console.WriteLine(ticket.Message);
    Console.WriteLine($"  routing   {Route(answer)}");
    Console.WriteLine($"  intent    {answer.Intent.Value} (confidence {Format(answer.Intent.Confidence.Value)})");
    Console.WriteLine($"  urgency   {answer.Urgency.Nearest} ({Format(answer.Urgency.Value)})");
    Console.WriteLine($"  abusive   {Format(answer.Abusive.Probability)}");
    Console.WriteLine($"  decision  {result.Id} using {result.Model ?? "unknown"}");
    Console.WriteLine($"  strategy  {result.Provider} {options.Strategy}");
    Console.WriteLine();
}

return 0;

static string Route(TicketTriage answer)
{
    if (answer.Abusive.Probability >= 0.7)
    {
        return "quarantine";
    }

    return answer.Intent.Confidence.Value switch
    {
        >= 0.85 => $"auto:{answer.Intent.Value}",
        >= 0.5 => $"review:{answer.Intent.Value}",
        _ => "human",
    };
}

static string Format(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
