using System.Text.RegularExpressions;
using Gauge.Localization;

namespace Gauge.Tests;

/// <summary>
/// Structural invariants of the translation table that a translator could easily break:
/// every key must have all three languages, and the format placeholders must line up so
/// no language drops or adds a <c>{0}</c>/<c>{1}</c> argument.
/// </summary>
public sealed class LocalizationTableTests
{
    [Fact]
    public void EveryEntryHasExactlyThreeNonEmptyLanguages()
    {
        foreach (var (key, values) in Strings.Table)
        {
            Assert.True(values.Length == 3, $"'{key}' must have 3 languages but has {values.Length}.");
            for (var i = 0; i < values.Length; i++)
            {
                Assert.False(string.IsNullOrEmpty(values[i]), $"'{key}' is empty for language index {i}.");
            }
        }
    }

    [Fact]
    public void PlaceholderIndicesAreIdenticalAcrossLanguages()
    {
        foreach (var (key, values) in Strings.Table)
        {
            var reference = Placeholders(values[0]!);
            for (var i = 1; i < values.Length; i++)
            {
                var other = Placeholders(values[i]!);
                Assert.True(reference.SetEquals(other),
                    $"'{key}' placeholder set differs at language index {i}: " +
                    $"[{Join(reference)}] vs [{Join(other)}].");
            }
        }
    }

    // Matches the index of each composite-format hole, e.g. "{0}" and "{0:0}" → 0.
    private static HashSet<int> Placeholders(string template)
        => Regex.Matches(template, @"\{(\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

    private static string Join(IEnumerable<int> indices) => string.Join(",", indices.OrderBy(i => i));
}
