using System.Globalization;
using Adjudge;
using Adjudge.Jev;
using Adjudge.OpenAI;
using Adjudge.Providers;
using Adjudge.Sample.Cascade;

var missing = new[]
    {
        JevOptions.ApiKeyVariable,
        OpenAIOptions.ApiKeyVariable,
        OpenAIOptions.BaseUrlVariable,
        OpenAIOptions.ModelVariable,
    }
    .Where(variable => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
    .ToArray();

if (missing.Length > 0)
{
    Console.Error.WriteLine($"Set {string.Join(", ", missing)} and run the sample again.");
    return 1;
}

var rules = RulesProvider.Create<TicketContext>(r => r
    .Classify<TicketIntent>("intent", c => c
        .When(
            TicketIntent.Billing,
            ctx => ctx.Message.Contains("charged", StringComparison.OrdinalIgnoreCase) ||
                ctx.Message.Contains("refund", StringComparison.OrdinalIgnoreCase))
        .When(
            TicketIntent.OrderTracking,
            ctx => ctx.Message.Contains("where is my order", StringComparison.OrdinalIgnoreCase)))
    .Assert("abusive", ctx => ctx.Message.Contains("clowns", StringComparison.OrdinalIgnoreCase)));

var cascade = new CascadeProvider(
    new CascadeStage(rules),
    new CascadeStage(new JevProvider(new JevOptions()), AcceptWhen.ConfidenceAtLeast(0.7)),
    new CascadeStage(new OpenAIProvider(new OpenAIOptions())));

var engine = new DecisionEngine(cascade);
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
    Console.WriteLine($"  intent    {answer.Intent.Value} (confidence {Format(answer.Intent.Confidence.Value)}) via {Via(result, "intent")}");
    Console.WriteLine($"  urgency   {answer.Urgency.Nearest} ({Format(answer.Urgency.Value)}) via {Via(result, "urgency")}");
    Console.WriteLine($"  abusive   {Format(answer.Abusive.Probability)} via {Via(result, "abusive")}");
    Console.WriteLine($"  decision  {result.Id} using {result.Model ?? "mixed"}");
    Console.WriteLine($"  stages    called {Meta(result, "stages.called")}");
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

static string Via(DecisionResult<TicketTriage> result, string question) =>
    Meta(result, $"question.{question}.provider");

static string Meta(DecisionResult<TicketTriage> result, string key) =>
    result.Metadata.TryGetValue(key, out var value) ? value : "unknown";

static string Format(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
