namespace Adjudge.Tests.Abstractions;

public sealed class EnumMembersTests
{
    [Fact]
    public void Values_WhenMembersHaveNegativeValues_AreOrderedAscending()
    {
        EnumMembers<Temperature>.Values.ShouldBe([Temperature.Freezing, Temperature.Cold, Temperature.Warm, Temperature.Hot]);
    }

    [Fact]
    public void Top_WhenTwoMembersTie_IsTheLowestUnderlyingValue()
    {
        var distribution = new Distribution<Temperature>(new Dictionary<Temperature, double>
        {
            [Temperature.Hot] = 0.5,
            [Temperature.Freezing] = 0.5,
        });

        distribution.Top.ShouldBe(Temperature.Freezing);
    }

    [Fact]
    public void From_WhenMembersHaveNegativeValues_UsesTheirAscendingPositions()
    {
        var rating = Rating<Temperature>.From(
            new Distribution<Temperature>(new Dictionary<Temperature, double> { [Temperature.Warm] = 1 }),
            new Confidence(1, ConfidenceSource.Derived));

        rating.Value.ShouldBe(2, 1e-9);
    }
}
