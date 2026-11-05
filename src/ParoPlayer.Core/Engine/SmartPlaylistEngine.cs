using System.Globalization;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Engine;

public class SmartPlaylistEngine(TimeProvider timeProvider, Random random) : ISmartPlaylistEngine
{
    public IReadOnlyList<Track> Evaluate(IEnumerable<Track> tracks, SmartPlaylistDefinition definition)
    {
        var now = timeProvider.GetUtcNow();
        var matching = tracks.Where(track => Matches(track, definition, now));

        IEnumerable<Track> sorted = definition.SortBy switch
        {
            SmartSort.MostPlayed => matching.OrderByDescending(track => track.PlayCount),
            SmartSort.Newest => matching.OrderByDescending(track => track.AddedAt),
            SmartSort.Random => matching.OrderBy(track => random.Next()),
            _ => matching.OrderBy(track => track.Title, StringComparer.OrdinalIgnoreCase),
        };

        return (definition.Limit is { } limit ? sorted.Take(Math.Max(limit, 0)) : sorted).ToList();
    }

    private static bool Matches(Track track, SmartPlaylistDefinition definition, DateTimeOffset now)
    {
        if (definition.Rules.Count == 0)
        {
            return true;
        }

        return definition.MatchMode == MatchMode.All
            ? definition.Rules.All(rule => Matches(track, rule, now))
            : definition.Rules.Any(rule => Matches(track, rule, now));
    }

    private static bool Matches(Track track, SmartRule rule, DateTimeOffset now) => rule.Field switch
    {
        SmartField.PlayCount => CompareNumber(track.PlayCount, rule),
        SmartField.Year => CompareNumber(track.Year, rule),
        SmartField.Rating => CompareNumber(track.Rating, rule),
        SmartField.DurationSeconds => CompareNumber(track.DurationSeconds, rule),
        SmartField.AddedDaysAgo => CompareNumber(Math.Floor((now - track.AddedAt).TotalDays), rule),
        SmartField.Genre => CompareText(track.Genre, rule),
        SmartField.Artist => CompareText(track.Artist, rule),
        SmartField.Album => CompareText(track.Album, rule),
        SmartField.Title => CompareText(track.Title, rule),
        _ => false,
    };

    private static bool CompareNumber(double actual, SmartRule rule)
    {
        if (!double.TryParse(rule.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
        {
            return false;
        }

        return rule.Operator switch
        {
            SmartOperator.GreaterThan => actual > expected,
            SmartOperator.LessThan => actual < expected,
            SmartOperator.Equals => actual == expected,
            SmartOperator.NotEquals => actual != expected,
            _ => false,
        };
    }

    private static bool CompareText(string actual, SmartRule rule) => rule.Operator switch
    {
        SmartOperator.Equals => string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase),
        SmartOperator.NotEquals => !string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase),
        SmartOperator.Contains => actual.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
