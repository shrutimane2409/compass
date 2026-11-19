using Compass.Domain;
using static Compass.Domain.Tests.TestData;

namespace Compass.Domain.Tests;

public class CostCalculatorTests
{
    private readonly CostCalculator _sut = new();

    [Fact]
    public void Total_is_tuition_plus_living_for_duration_plus_one_off()
    {
        var cost = _sut.Calculate(Course(fee: 20000m, months: 12), monthlyLivingEur: 1500m, oneOffEur: 1000m);
        Assert.Equal(20000m, cost.Tuition);
        Assert.Equal(18000m, cost.Living);
        Assert.Equal(39000m, cost.Total);
    }

    [Theory]
    [InlineData(39000, 40000, Affordability.Affordable)]
    [InlineData(40000, 40000, Affordability.Affordable)]
    [InlineData(43000, 40000, Affordability.Stretch)]
    [InlineData(44000, 40000, Affordability.Stretch)]
    [InlineData(44001, 40000, Affordability.OverBudget)]
    public void Affordability_bands(double total, double budget, Affordability expected)
    {
        var cost = new CostBreakdown((decimal)total, 0, 0);
        Assert.Equal(expected, _sut.Assess(cost, (decimal)budget));
    }

    [Fact]
    public void Negative_living_cost_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Calculate(Course(), -1m, 0m));
}
