using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Tests;

public class SmartPlaylistEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SmartPlaylistEngine engine = new(new FakeTimeProvider(Now), new Random(42));

    private static SmartPlaylistDefinition All(params SmartRule[] rules) => new(MatchMode.All, rules);

    private static SmartPlaylistDefinition Any(params SmartRule[] rules) => new(MatchMode.Any, rules);

    private static SmartRule Rule(SmartField field, SmartOperator op, string value) => new(field, op, value);

    private static string[] Titles(IEnumerable<Track> tracks) => [.. tracks.Select(track => track.Title)];

    private static readonly Track[] Library =
    [
        new() { Title = "Alpha", Artist = "The Band", Album = "First", Genre = "Rock", Year = 1999, DurationSeconds = 200, Rating = 5, PlayCount = 10, AddedAt = Now.AddDays(-1) },
        new() { Title = "beta", Artist = "Solo", Album = "Second", Genre = "Jazz", Year = 2010, DurationSeconds = 400, Rating = 3, PlayCount = 2, AddedAt = Now.AddDays(-10) },
        new() { Title = "Gamma", Artist = "the band", Album = "Third", Genre = "rock", Year = 2020, DurationSeconds = 100, Rating = 0, PlayCount = 0, AddedAt = Now.AddDays(-100) },
    ];

    [Theory]
    [InlineData(SmartField.PlayCount, SmartOperator.GreaterThan, "5", "Alpha")]
    [InlineData(SmartField.PlayCount, SmartOperator.LessThan, "2", "Gamma")]
    [InlineData(SmartField.PlayCount, SmartOperator.Equals, "2", "beta")]
    [InlineData(SmartField.PlayCount, SmartOperator.NotEquals, "2", "Alpha,Gamma")]
    [InlineData(SmartField.Year, SmartOperator.GreaterThan, "2000", "beta,Gamma")]
    [InlineData(SmartField.Year, SmartOperator.Equals, "1999", "Alpha")]
    [InlineData(SmartField.Rating, SmartOperator.GreaterThan, "3", "Alpha")]
    [InlineData(SmartField.Rating, SmartOperator.LessThan, "1", "Gamma")]
    [InlineData(SmartField.DurationSeconds, SmartOperator.GreaterThan, "300", "beta")]
    [InlineData(SmartField.DurationSeconds, SmartOperator.LessThan, "150", "Gamma")]
    [InlineData(SmartField.AddedDaysAgo, SmartOperator.LessThan, "5", "Alpha")]
    [InlineData(SmartField.AddedDaysAgo, SmartOperator.GreaterThan, "50", "Gamma")]
    [InlineData(SmartField.Genre, SmartOperator.Equals, "ROCK", "Alpha,Gamma")]
    [InlineData(SmartField.Genre, SmartOperator.NotEquals, "rock", "beta")]
    [InlineData(SmartField.Artist, SmartOperator.Contains, "BAND", "Alpha,Gamma")]
    [InlineData(SmartField.Album, SmartOperator.Contains, "ec", "beta")]
    [InlineData(SmartField.Title, SmartOperator.Equals, "BETA", "beta")]
    [InlineData(SmartField.Title, SmartOperator.Contains, "am", "Gamma")]
    public void Evaluate_AppliesFieldAndOperator(SmartField field, SmartOperator op, string value, string expected)
    {
        var result = engine.Evaluate(Library, All(Rule(field, op, value)));

        Assert.Equal(expected.Split(','), Titles(result));
    }

    [Fact]
    public void Evaluate_AllRequiresEveryRule()
    {
        var result = engine.Evaluate(Library, All(
            Rule(SmartField.Genre, SmartOperator.Equals, "rock"),
            Rule(SmartField.Year, SmartOperator.GreaterThan, "2000")));

        Assert.Equal(["Gamma"], Titles(result));
    }

    [Fact]
    public void Evaluate_AnyRequiresOneRule()
    {
        var result = engine.Evaluate(Library, Any(
            Rule(SmartField.Genre, SmartOperator.Equals, "jazz"),
            Rule(SmartField.Year, SmartOperator.LessThan, "2000")));

        Assert.Equal(["Alpha", "beta"], Titles(result));
    }

    [Fact]
    public void Evaluate_NoRulesMatchesEverything()
    {
        Assert.Equal(3, engine.Evaluate(Library, All()).Count);
    }

    [Fact]
    public void Evaluate_SortsMostPlayed()
    {
        var result = engine.Evaluate(Library, All() with { SortBy = SmartSort.MostPlayed });

        Assert.Equal(["Alpha", "beta", "Gamma"], Titles(result));
    }

    [Fact]
    public void Evaluate_SortsNewest()
    {
        var result = engine.Evaluate(Library, All() with { SortBy = SmartSort.Newest });

        Assert.Equal(["Alpha", "beta", "Gamma"], Titles(result));
    }

    [Fact]
    public void Evaluate_SortsByTitleIgnoringCase()
    {
        var result = engine.Evaluate(Library.Reverse(), All() with { SortBy = SmartSort.Title });

        Assert.Equal(["Alpha", "beta", "Gamma"], Titles(result));
    }

    [Fact]
    public void Evaluate_RandomIsDeterministicForSameSeed()
    {
        var definition = All() with { SortBy = SmartSort.Random };
        var first = new SmartPlaylistEngine(new FakeTimeProvider(Now), new Random(7)).Evaluate(Library, definition);
        var second = new SmartPlaylistEngine(new FakeTimeProvider(Now), new Random(7)).Evaluate(Library, definition);

        Assert.Equal(Titles(first), Titles(second));
        Assert.Equal(3, first.Count);
    }

    [Fact]
    public void Evaluate_AppliesLimitAfterSort()
    {
        var result = engine.Evaluate(Library, All() with { SortBy = SmartSort.MostPlayed, Limit = 2 });

        Assert.Equal(["Alpha", "beta"], Titles(result));
    }

    [Theory]
    [InlineData(SmartField.PlayCount, SmartOperator.GreaterThan, "abc")]
    [InlineData(SmartField.Year, SmartOperator.Equals, "")]
    [InlineData(SmartField.AddedDaysAgo, SmartOperator.LessThan, "soon")]
    [InlineData(SmartField.Rating, SmartOperator.Contains, "3")]
    [InlineData(SmartField.Title, SmartOperator.GreaterThan, "a")]
    [InlineData(SmartField.Genre, SmartOperator.Equals, "")]
    public void Evaluate_BadValueDoesNotThrowAndDoesNotMatch(SmartField field, SmartOperator op, string value)
    {
        var result = engine.Evaluate(Library, All(Rule(field, op, value)));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_AddedDaysAgoFollowsTimeProvider()
    {
        var time = new FakeTimeProvider(Now);
        var timeEngine = new SmartPlaylistEngine(time, new Random(1));
        var definition = All(Rule(SmartField.AddedDaysAgo, SmartOperator.LessThan, "5"));

        Assert.Equal(["Alpha"], Titles(timeEngine.Evaluate(Library, definition)));

        time.Advance(TimeSpan.FromDays(30));

        Assert.Empty(timeEngine.Evaluate(Library, definition));
    }
}
