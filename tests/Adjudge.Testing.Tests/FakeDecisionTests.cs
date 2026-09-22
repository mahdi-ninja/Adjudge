namespace Adjudge.Testing.Tests;

public sealed class FakeDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task DecideAsync_WithScriptedValue_WrapsItInAResult()
    {
        var before = DateTimeOffset.UtcNow;
        var decision = new FakeDecision<Ticket, string>().Returns("triaged");

        var result = await decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("triaged");
        result.DefinitionId.ShouldBe("fake");
        result.Provider.ShouldBe("fake");
        result.Model.ShouldBeNull();
        result.Usage.ShouldBeNull();
        result.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedFunction_SeesTheContext()
    {
        var decision = new FakeDecision<Ticket, string>().Returns(ticket => ticket.Message.ToUpperInvariant());

        var result = await decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("HELLO");
    }

    [Fact]
    public async Task DecideAsync_WithScriptedResult_PassesItThrough()
    {
        var scripted = new DecisionResult<string>(
            Guid.NewGuid(),
            "support.triage",
            "jev",
            "jev-1",
            "triaged",
            new Usage(1, 2),
            Now,
            new Dictionary<string, string> { ["request_id"] = "req_1" });
        var decision = new FakeDecision<Ticket, string>().Returns(scripted);

        var result = await decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(scripted);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedValue_WrapsItWithEmptyMetadata()
    {
        var decision = new FakeDecision<Ticket, string>().Returns("triaged");

        var result = await decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken);

        result.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_AfterWithMetadata_WrapsTheValueWithThatMetadata()
    {
        var decision = new FakeDecision<Ticket, string>().Returns("triaged").WithMetadata("stage", "cheap");

        var result = await decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken);

        result.Metadata["stage"].ShouldBe("cheap");
    }

    [Fact]
    public async Task DecideAsync_AfterThrows_Throws()
    {
        var decision = new FakeDecision<Ticket, string>().Returns("triaged").Throws(new TimeoutException("boom"));

        await Should.ThrowAsync<TimeoutException>(() => decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecideAsync_WithoutScript_Throws()
    {
        var decision = new FakeDecision<Ticket, string>();

        await Should.ThrowAsync<InvalidOperationException>(() => decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecideAsync_WhenCalled_RecordsContextsAndCalls()
    {
        var decision = new FakeDecision<Ticket, string>().Returns("triaged");

        await decision.DecideAsync(new Ticket("one"), TestContext.Current.CancellationToken);
        await decision.DecideAsync(new Ticket("two"), TestContext.Current.CancellationToken);

        decision.Calls.ShouldBe(2);
        decision.Contexts.Select(c => c.Message).ShouldBe(["one", "two"]);
    }
}
