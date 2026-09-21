namespace Adjudge.Tests.Abstractions;

public sealed class DistributionTests
{
    [Fact]
    public void Constructor_WhenValuesDoNotSumToOne_NormalisesThem()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 2,
            [Intent.Tracking] = 2,
        });

        distribution.Probabilities[Intent.Billing].ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void Constructor_WhenMemberIsMissing_FillsItWithZero()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 1 });

        distribution.Probabilities[Intent.Returns].ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenMemberIsMissing_StillExposesEveryMember()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 1 });

        distribution.Probabilities.Count.ShouldBe(3);
    }

    [Fact]
    public void Constructor_WhenEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>()));
    }

    [Fact]
    public void Constructor_WhenAllValuesAreZero_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 0,
            [Intent.Tracking] = 0,
        }));
    }

    [Fact]
    public void Constructor_WhenAValueIsNegative_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 1,
            [Intent.Tracking] = -0.5,
        }));
    }

    [Fact]
    public void Top_WhenOneOptionLeads_IsThatOption()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 0.2,
            [Intent.Tracking] = 0.8,
        });

        distribution.Top.ShouldBe(Intent.Tracking);
    }

    [Fact]
    public void Top_WhenTwoOptionsTie_IsTheLowestEnumValue()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Tracking] = 0.5,
            [Intent.Returns] = 0.5,
        });

        distribution.Top.ShouldBe(Intent.Tracking);
    }

    [Fact]
    public void Margin_WhenTopLeadsSecond_IsTheDifference()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 0.6,
            [Intent.Tracking] = 0.3,
            [Intent.Returns] = 0.1,
        });

        distribution.Margin.ShouldBe(0.3, 1e-9);
    }

    [Fact]
    public void Confidence_WhenUniform_IsZero()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 1,
            [Intent.Tracking] = 1,
            [Intent.Returns] = 1,
        });

        distribution.Confidence.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Confidence_WhenOnlyOneMemberExists_IsOne()
    {
        var distribution = new Distribution<Lone>(new Dictionary<Lone, double> { [Lone.Only] = 0.3 });

        distribution.Confidence.ShouldBe(1);
    }

    [Fact]
    public void Confidence_WhenAllTheMassIsOnOneOption_IsOne()
    {
        var distribution = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Returns] = 1 });

        distribution.Confidence.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Confidence_WhenTwoOptionsAndTopIsThreeQuarters_IsAHalf()
    {
        var distribution = new Distribution<Pair>(new Dictionary<Pair, double>
        {
            [Pair.Yes] = 0.75,
            [Pair.No] = 0.25,
        });

        distribution.Confidence.ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void Margin_WhenOnlyOneMemberExists_IsTheWholeMass()
    {
        var distribution = new Distribution<Lone>(new Dictionary<Lone, double> { [Lone.Only] = 4 });

        distribution.Margin.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Constructor_WhenAKeyIsNotADeclaredMember_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = 1,
            [(Intent)99] = 1,
        }));
    }

    [Fact]
    public void Constructor_WhenAValueIsNotFinite_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = double.PositiveInfinity,
        }));
    }

    [Fact]
    public void Constructor_WhenAValueIsNaN_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = double.NaN,
        }));
    }

    [Fact]
    public void Constructor_WhenTheTotalIsNotFinite_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Intent>(new Dictionary<Intent, double>
        {
            [Intent.Billing] = double.MaxValue,
            [Intent.Tracking] = double.MaxValue,
        }));
    }

    [Fact]
    public void Constructor_WhenTheEnumDeclaresNoMembers_Throws()
    {
        Should.Throw<ArgumentException>(() => new Distribution<Blank>(new Dictionary<Blank, double>()));
    }

    [Fact]
    public void Constructor_WhenTheDictionaryIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new Distribution<Intent>(null!));
    }

    [Fact]
    public void Constructor_WhenTheCallerMutatesTheDictionaryAfterwards_KeepsItsOwnCopy()
    {
        var probabilities = new Dictionary<Intent, double> { [Intent.Billing] = 1 };
        var distribution = new Distribution<Intent>(probabilities);

        probabilities[Intent.Tracking] = 3;

        distribution.Probabilities[Intent.Tracking].ShouldBe(0);
    }

    [Fact]
    public void Margin_WhenTwoOptionsTie_IsZero()
    {
        var distribution = new Distribution<Pair>(new Dictionary<Pair, double>
        {
            [Pair.Yes] = 0.5,
            [Pair.No] = 0.5,
        });

        distribution.Margin.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Equals_WhenTheProbabilitiesMatch_IsTrue()
    {
        var left = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 2, [Intent.Tracking] = 2 });
        var right = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 0.5, [Intent.Tracking] = 0.5 });

        left.ShouldBe(right);
    }

    [Fact]
    public void GetHashCode_WhenTheProbabilitiesMatch_IsTheSame()
    {
        var left = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 2, [Intent.Tracking] = 2 });
        var right = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 0.5, [Intent.Tracking] = 0.5 });

        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_WhenAMemberProbabilityDiffers_IsFalse()
    {
        var left = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 0.6, [Intent.Tracking] = 0.4 });
        var right = new Distribution<Intent>(new Dictionary<Intent, double> { [Intent.Billing] = 0.6, [Intent.Returns] = 0.4 });

        left.ShouldNotBe(right);
    }
}
