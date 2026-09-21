using System.Text.Json;

namespace Adjudge.Tests.Core;

public sealed class ContextSerializerTests
{
    private static readonly Ticket Context = new("I was charged twice");

    [Fact]
    public void From_SerializerContextWithTheType_SerialisesTheContext()
    {
        var serialise = ContextSerializer.From(ProbeJsonContext.Default);

        serialise(Context).GetProperty("message").GetString().ShouldBe(Context.Message);
    }

    [Fact]
    public void From_SerializerContextWithoutTheType_ThrowsNamingTheType()
    {
        var serialise = ContextSerializer.From(ProbeJsonContext.Default);

        Action serialiseUnregistered = () => serialise(new Unregistered("nope"));

        serialiseUnregistered.ShouldThrow<InvalidOperationException>().Message.ShouldContain(nameof(Unregistered));
    }

    [Fact]
    public void From_TypeInfo_SerialisesTheContext()
    {
        var serialise = ContextSerializer.From(ProbeJsonContext.Default.Ticket);

        serialise(Context).GetProperty("message").GetString().ShouldBe(Context.Message);
    }

    [Fact]
    public void From_TypeInfoAndAContextOfAnotherType_Throws()
    {
        var serialise = ContextSerializer.From(ProbeJsonContext.Default.Ticket);

        Action serialiseOther = () => serialise(new Unregistered("nope"));

        serialiseOther.ShouldThrow<InvalidOperationException>().Message.ShouldContain(nameof(Unregistered));
    }

    [Fact]
    public async Task DecisionEngine_ConstructedWithASerialiser_SendsTheJsonItProduces()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var engine = new DecisionEngine(
            provider,
            new DecisionEngineOptions(),
            _ => JsonDocument.Parse("""{"distinctive":"marker"}""").RootElement.Clone());
        var decision = engine.Create<TriageDecision, Ticket, Triage>();

        await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        provider.Requests.Single().Context.AsJson().GetProperty("distinctive").GetString().ShouldBe("marker");
    }

    [Fact]
    public async Task DecisionEngine_ConstructedWithoutASerialiser_SerialisesByReflection()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();

        await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        provider.Requests.Single().Context.AsJson().GetProperty("message").GetString().ShouldBe(Context.Message);
    }
}
