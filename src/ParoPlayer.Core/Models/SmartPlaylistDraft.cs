namespace ParoPlayer.Core.Models;

public class RuleDraft
{
    public SmartField Field { get; set; } = SmartField.PlayCount;

    public SmartOperator Operator { get; set; } = SmartOperator.GreaterThan;

    public string Value { get; set; } = "";
}

/// <summary>Editable state of the smart playlist builder. Turns into an immutable <see cref="SmartPlaylistDefinition"/>.</summary>
public class SmartPlaylistDraft
{
    public static readonly IReadOnlyList<SmartField> Fields = Enum.GetValues<SmartField>();

    public static readonly IReadOnlyList<SmartSort> Sorts = Enum.GetValues<SmartSort>();

    public static readonly IReadOnlyList<int> Limits = [25, 50, 100];

    private static readonly SmartOperator[] NumberOperators =
        [SmartOperator.GreaterThan, SmartOperator.LessThan, SmartOperator.Equals, SmartOperator.NotEquals];

    private static readonly SmartOperator[] TextOperators =
        [SmartOperator.Contains, SmartOperator.Equals, SmartOperator.NotEquals];

    public string Name { get; set; } = "";

    public MatchMode MatchMode { get; set; } = MatchMode.All;

    public SmartSort SortBy { get; set; } = SmartSort.MostPlayed;

    public int Limit { get; set; } = 50;

    public List<RuleDraft> Rules { get; } = [];

    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    public RuleDraft AddRule()
    {
        var rule = new RuleDraft();
        Rules.Add(rule);
        return rule;
    }

    public void RemoveRule(RuleDraft rule) => Rules.Remove(rule);

    /// <summary>Keeps the operator valid after the field changed (text fields have no greater/less than).</summary>
    public static void ChangeField(RuleDraft rule, SmartField field)
    {
        rule.Field = field;
        if (!OperatorsFor(field).Contains(rule.Operator))
        {
            rule.Operator = OperatorsFor(field)[0];
        }
    }

    /// <summary>Rules with an empty value are ignored so the preview stays useful while typing.</summary>
    public SmartPlaylistDefinition ToDefinition() => new(
        MatchMode,
        [.. Rules.Where(rule => !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => new SmartRule(rule.Field, rule.Operator, rule.Value.Trim()))],
        SortBy,
        Limit);

    public static IReadOnlyList<SmartOperator> OperatorsFor(SmartField field) => IsText(field) ? TextOperators : NumberOperators;

    public static bool IsText(SmartField field) => field is SmartField.Genre or SmartField.Artist or SmartField.Album or SmartField.Title;

    public static string Label(SmartField field) => field switch
    {
        SmartField.PlayCount => "Play count",
        SmartField.AddedDaysAgo => "Added (days ago)",
        SmartField.Year => "Year",
        SmartField.Rating => "Rating",
        SmartField.DurationSeconds => "Duration (s)",
        SmartField.Genre => "Genre",
        SmartField.Artist => "Artist",
        SmartField.Album => "Album",
        _ => "Title",
    };

    public static string Label(SmartOperator op) => op switch
    {
        SmartOperator.GreaterThan => "is greater than",
        SmartOperator.LessThan => "is less than",
        SmartOperator.Equals => "is",
        SmartOperator.NotEquals => "is not",
        _ => "contains",
    };

    public static string Label(SmartSort sort) => sort switch
    {
        SmartSort.MostPlayed => "Most played",
        SmartSort.Newest => "Newest first",
        SmartSort.Random => "Random",
        _ => "Title",
    };

    public static string Label(MatchMode mode) => mode == MatchMode.All ? "All of the rules" : "Any of the rules";

    /// <summary>Short summary shown under a smart playlist name, e.g. "2 rules, most played".</summary>
    public static string Describe(SmartPlaylistDefinition definition)
    {
        var rules = definition.Rules.Count == 1 ? "1 rule" : $"{definition.Rules.Count} rules";
        return $"{rules}, {Label(definition.SortBy).ToLowerInvariant()}";
    }
}
