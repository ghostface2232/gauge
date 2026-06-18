using Gauge.Models;
using Gauge.ViewModels;

namespace Gauge.Tests;

public sealed class UsageLevelClassifierTests
{
    [Theory]
    [InlineData(0.6999, UsageLevel.Ok)]
    [InlineData(0.70, UsageLevel.Caution)]
    [InlineData(0.8999, UsageLevel.Caution)]
    [InlineData(0.90, UsageLevel.Danger)]
    public void UsesSharedSeventyAndNinetyPercentBoundaries(double ratio, UsageLevel expected)
        => Assert.Equal(expected, UsageLevelClassifier.Classify(ratio));
}
