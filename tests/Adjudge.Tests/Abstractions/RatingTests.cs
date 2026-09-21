namespace Adjudge.Tests.Abstractions;

public sealed class RatingTests
{
    [Fact]
    public void From_WhenAllTheMassIsOnTheSecondLevel_ValueIsThatIndex()
    {
        var rating = Rating<Urgency>.From(
            new Distribution<Urgency>(new Dictionary<Urgency, double> { [Urgency.Medium] = 1 }),
            new Confidence(1, ConfidenceSource.Derived));

        rating.Value.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void From_WhenMassIsSplitAcrossLevels_ValueIsTheWeightedPosition()
    {
        var rating = Rating<Urgency>.From(
            new Distribution<Urgency>(new Dictionary<Urgency, double>
            {
                [Urgency.Low] = 0.5,
                [Urgency.Critical] = 0.5,
            }),
            new Confidence(0, ConfidenceSource.Derived));

        rating.Value.ShouldBe(1.5, 1e-9);
    }

    [Fact]
    public void From_WhenLevelsHaveNonContiguousValues_UsesPositionNotUnderlyingValue()
    {
        var rating = Rating<Urgency>.From(
            new Distribution<Urgency>(new Dictionary<Urgency, double> { [Urgency.Critical] = 1 }),
            new Confidence(1, ConfidenceSource.Derived));

        rating.Value.ShouldBe(3, 1e-9);
    }

    [Fact]
    public void From_WhenValueIsExactlyHalfway_NearestRoundsAwayFromZero()
    {
        var rating = Rating<Urgency>.From(
            new Distribution<Urgency>(new Dictionary<Urgency, double>
            {
                [Urgency.Low] = 0.5,
                [Urgency.Medium] = 0.5,
            }),
            new Confidence(0, ConfidenceSource.Derived));

        rating.Nearest.ShouldBe(Urgency.Medium);
    }

    [Fact]
    public void From_WhenValueIsBelowHalfway_NearestIsTheLowerLevel()
    {
        var rating = Rating<Urgency>.From(
            new Distribution<Urgency>(new Dictionary<Urgency, double>
            {
                [Urgency.Low] = 0.6,
                [Urgency.Medium] = 0.4,
            }),
            new Confidence(0, ConfidenceSource.Derived));

        rating.Nearest.ShouldBe(Urgency.Low);
    }

    [Fact]
    public void From_WhenGivenADistribution_CarriesItOnTheRating()
    {
        var distribution = new Distribution<Urgency>(new Dictionary<Urgency, double> { [Urgency.High] = 1 });

        var rating = Rating<Urgency>.From(distribution, new Confidence(1, ConfidenceSource.Derived));

        rating.Distribution.ShouldBe(distribution);
    }

    [Fact]
    public void From_WhenAllTheMassIsOnTheLastLevel_NearestIsThatLevel()
    {
        var rating = Rating<Urgency>.From(
            new Distribution<Urgency>(new Dictionary<Urgency, double> { [Urgency.Critical] = 1 }),
            new Confidence(1, ConfidenceSource.Derived));

        rating.Nearest.ShouldBe(Urgency.Critical);
    }

    [Fact]
    public void From_WhenTheDistributionIsUninitialised_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            Rating<Urgency>.From(default, new Confidence(1, ConfidenceSource.Derived)));
    }
}
