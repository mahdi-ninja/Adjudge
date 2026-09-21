using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge.Tests.Abstractions;

public sealed class DecisionContextTests
{
    [Fact]
    public void AsJson_WhenValueIsSerialised_ExposesItsProperties()
    {
        var context = new DecisionContext(new Ticket("late"), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        context.AsJson().GetProperty("subject").GetString().ShouldBe("late");
    }

    [Fact]
    public void AsJson_WhenCalledTwice_ReturnsTheCachedElement()
    {
        var context = new DecisionContext(new Ticket("late"));

        var first = context.AsJson();

        context.AsJson().GetRawText().ShouldBe(first.GetRawText());
    }

    [Fact]
    public void AsJson_WhenBuiltFromAnElement_ReturnsThatElement()
    {
        var element = JsonDocument.Parse("""{"subject":"late"}""").RootElement;

        DecisionContext.FromJson(new Ticket("late"), element).AsJson().GetProperty("subject").GetString().ShouldBe("late");
    }

    [Fact]
    public void Value_WhenConstructed_IsTheOriginalObject()
    {
        var ticket = new Ticket("late");

        DecisionContext.FromJson(ticket, JsonDocument.Parse("{}").RootElement).Value.ShouldBeSameAs(ticket);
    }

    [Fact]
    public void Constructor_WhenValueIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => DecisionContext.FromJson(null!, JsonDocument.Parse("{}").RootElement));
    }

    [Fact]
    public void AsJson_WhenCalledTwice_SerialisesOnce()
    {
        var converter = new CountingTicketConverter();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { converter } };
        var context = new DecisionContext(new Ticket("late"), options);

        context.AsJson();
        context.AsJson();

        converter.Writes.ShouldBe(1);
    }

    [Fact]
    public void AsJson_WhenCalledConcurrently_SerialisesOnce()
    {
        var converter = new CountingTicketConverter();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { converter } };
        var context = new DecisionContext(new Ticket("late"), options);
        var texts = new string[64];

        Parallel.For(0, texts.Length, index => texts[index] = context.AsJson().GetRawText());

        converter.Writes.ShouldBe(1);
        texts.ShouldAllBe(text => text == texts[0]);
    }
}
