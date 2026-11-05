using System.Text.RegularExpressions;

namespace ParoPlayer.Core.Tests;

/// <summary>
/// Source-level guard for rules Roslyn cannot express (leading underscore, mobile UI rules).
/// </summary>
public partial class CodingRulesTests
{
    [GeneratedRegex(@"(?<![\w@""'/-])_[A-Za-z]\w*")]
    private static partial Regex UnderscorePrefixedName();

    [GeneratedRegex(@"\.GetAwaiter\(\)\s*\.GetResult\(\)|\.Result\b(?!\w)|\.Wait\(\)")]
    private static partial Regex BlockingTaskAccess();

    [GeneratedRegex(@"<table\b", RegexOptions.IgnoreCase)]
    private static partial Regex TableElement();

    [Fact]
    public void NoMemberStartsWithUnderscore() => AssertNoMatches(UnderscorePrefixedName(), "*.cs");

    [Fact]
    public void NoBlockingOnTasks() => AssertNoMatches(BlockingTaskAccess(), "*.cs");

    [Fact]
    public void NoTableElementsInRazor() => AssertNoMatches(TableElement(), "*.razor");

    [Fact]
    public void NoTableElementsInWwwrootHtml() => AssertNoMatches(TableElement(), "*.html", requireWwwroot: true);

    private static void AssertNoMatches(Regex pattern, string filePattern, bool requireWwwroot = false)
    {
        var violations = SourceFiles(filePattern, requireWwwroot)
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, number: index + 1))
                .Where(x => !x.line.TrimStart().StartsWith("//") && pattern.IsMatch(x.line))
                .Select(x => $"{Path.GetRelativePath(RepoRoot(), x.file)}:{x.number}: {x.line.Trim()}"))
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> SourceFiles(string filePattern, bool requireWwwroot)
    {
        var src = Path.Combine(RepoRoot(), "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(src, filePattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}")
                        && !file.Contains($"{separator}bin{separator}")
                        && !file.Contains($"{separator}node_modules{separator}"))
            .Where(file => !requireWwwroot || file.Contains($"{separator}wwwroot{separator}"));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("global.json not found above the test output folder.");
    }
}
