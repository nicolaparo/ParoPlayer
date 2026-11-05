using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParoPlayer.Core.Models;

public enum MatchMode
{
    All,
    Any,
}

public enum SmartSort
{
    MostPlayed,
    Newest,
    Random,
    Title,
}

public enum SmartField
{
    PlayCount,
    AddedDaysAgo,
    Year,
    Rating,
    DurationSeconds,
    Genre,
    Artist,
    Album,
    Title,
}

public enum SmartOperator
{
    GreaterThan,
    LessThan,
    Equals,
    Contains,
    NotEquals,
}

public record SmartRule(SmartField Field, SmartOperator Operator, string Value);

public record SmartPlaylistDefinition(
    MatchMode MatchMode,
    IReadOnlyList<SmartRule> Rules,
    SmartSort SortBy = SmartSort.Title,
    int? Limit = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static SmartPlaylistDefinition FromJson(string json) =>
        JsonSerializer.Deserialize<SmartPlaylistDefinition>(json, JsonOptions)
        ?? throw new JsonException("Smart playlist definition is empty.");
}
