using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Library;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Tests;

public class LibraryQueryTests
{
    private static readonly Track[] Tracks =
    [
        new() { Title = "A", Artist = "M83", Album = "Dreaming", Genre = "Synth", FilePath = @"C:\lib\a.mp3" },
        new() { Title = "B", Artist = "M83", Album = "Dreaming", Genre = "Synth", FilePath = @"C:\lib\b.mp3" },
        new() { Title = "C", Artist = "", Album = "", Genre = "", FilePath = @"C:\lib\c.mp3" },
    ];

    [Fact]
    public void Groups_CountAndFallBackToUnknown()
    {
        var artists = LibraryQuery.Artists(Tracks);

        Assert.Equal(["M83", "Unknown"], artists.Select(group => group.Name));
        Assert.Equal("2 songs", artists[0].Subtitle);
        Assert.Equal("1 song", artists[1].Subtitle);
        Assert.Equal("M83", LibraryQuery.Albums(Tracks).First(group => group.Name == "Dreaming").Subtitle);
        Assert.Single(LibraryQuery.Folders(Tracks));
    }

    [Theory]
    [InlineData(SongSortKey.Title, false, "A B C")]
    [InlineData(SongSortKey.Title, true, "C B A")]
    [InlineData(SongSortKey.Artist, false, "C A B")]
    [InlineData(SongSortKey.Album, true, "A B C")]
    [InlineData(SongSortKey.DateAdded, false, "B C A")]
    [InlineData(SongSortKey.Duration, true, "C A B")]
    public void Sort_OrdersByKeyAndDirection(SongSortKey key, bool descending, string expected)
    {
        Track[] tracks =
        [
            new() { Title = "A", Artist = "M83", Album = "Dreaming", AddedAt = DateTimeOffset.FromUnixTimeSeconds(30), DurationSeconds = 200 },
            new() { Title = "B", Artist = "m83", Album = "Dreaming", AddedAt = DateTimeOffset.FromUnixTimeSeconds(10), DurationSeconds = 100 },
            new() { Title = "C", Artist = "", Album = "Alpha", AddedAt = DateTimeOffset.FromUnixTimeSeconds(20), DurationSeconds = 300 },
        ];

        Assert.Equal(expected, string.Join(' ', LibraryQuery.Sort(tracks, key, descending).Select(track => track.Title)));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(3725, "1:02:05")]
    public void FormatDuration(int seconds, string expected) => Assert.Equal(expected, LibraryQuery.FormatDuration(seconds));

    [Theory]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2 KB")]
    [InlineData(5L * 1024 * 1024, "5.0 MB")]
    public void FormatSize(long bytes, string expected) => Assert.Equal(expected, LibraryQuery.FormatSize(bytes));

    [Theory]
    [InlineData(7, "Good morning")]
    [InlineData(14, "Good afternoon")]
    [InlineData(22, "Good evening")]
    [InlineData(2, "Good evening")]
    public void Greeting_ByHour(int hour, string expected) =>
        Assert.Equal(expected, Greeting.For(new DateTimeOffset(2026, 1, 1, hour, 0, 0, TimeSpan.Zero)));
}

public class SmartPlaylistDraftTests
{
    [Fact]
    public void ToDefinition_SkipsEmptyRules_AndMatchesEngineInput()
    {
        var draft = new SmartPlaylistDraft { Name = "Mix", MatchMode = MatchMode.Any, Limit = 25 };
        draft.AddRule().Value = "20";
        draft.AddRule();

        var definition = draft.ToDefinition();

        Assert.Single(definition.Rules);
        Assert.Equal(new SmartRule(SmartField.PlayCount, SmartOperator.GreaterThan, "20"), definition.Rules[0]);
        Assert.Equal((MatchMode.Any, 25), (definition.MatchMode, definition.Limit));
    }

    [Fact]
    public void ChangeField_ToText_FixesOperator()
    {
        var rule = new RuleDraft();

        SmartPlaylistDraft.ChangeField(rule, SmartField.Genre);

        Assert.Equal(SmartOperator.Contains, rule.Operator);
        Assert.DoesNotContain(SmartOperator.GreaterThan, SmartPlaylistDraft.OperatorsFor(SmartField.Artist));
    }

    [Fact]
    public void Draft_DrivesTheEngine()
    {
        var draft = new SmartPlaylistDraft { Name = "Hot" };
        var rule = draft.AddRule();
        rule.Value = "5";
        var engine = new SmartPlaylistEngine(TimeProvider.System, new Random(1));

        var result = engine.Evaluate(
            [new Track { Title = "x", PlayCount = 9 }, new Track { Title = "y", PlayCount = 1 }],
            draft.ToDefinition());

        Assert.Equal("x", Assert.Single(result).Title);
    }

    [Fact]
    public void CanSave_NeedsName()
    {
        var draft = new SmartPlaylistDraft();
        Assert.False(draft.CanSave);
        draft.Name = "A";
        Assert.True(draft.CanSave);
    }
}
