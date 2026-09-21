using System.Globalization;
using Adjudge;
using Adjudge.Jev;
using Adjudge.Sample.Hosted;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(JevOptions.ApiKeyVariable)))
{
    Console.Error.WriteLine($"Set {JevOptions.ApiKeyVariable} to a Jev API key and run the sample again.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging
    .AddFilter("System.Net.Http", LogLevel.Warning)
    .AddFilter("Polly", LogLevel.Warning);
builder.Services
    .AddAdjudge()
    .AddJev()
    .AddDecision<TicketTriageDecision, TicketContext, TicketTriage>();
builder.Services.AddSingleton<TicketService>();

using var host = builder.Build();
var service = host.Services.GetRequiredService<TicketService>();

var tickets = new TicketContext[]
{
    new("I was charged twice for order 4471 and nobody has replied in a week.", "4471"),
    new("Your useless support team are a bunch of clowns. Fix it now.", null),
    new("Hi, quick question about my thing from last month.", null),
};

foreach (var ticket in tickets)
{
    var (routing, result) = await service.TriageAsync(ticket).ConfigureAwait(false);
    var answer = result.Value;

    Console.WriteLine(ticket.Message);
    Console.WriteLine($"  routing   {routing}");
    Console.WriteLine($"  intent    {answer.Intent.Value} (confidence {Format(answer.Intent.Confidence.Value)})");
    Console.WriteLine($"  urgency   {answer.Urgency.Nearest} ({Format(answer.Urgency.Value)})");
    Console.WriteLine($"  abusive   {Format(answer.Abusive.Probability)}");
    Console.WriteLine($"  decision  {result.Id} using {result.Model ?? "unknown"}");
    Console.WriteLine();
}

return 0;

static string Format(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
