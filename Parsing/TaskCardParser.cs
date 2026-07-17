using System.Text.RegularExpressions;

namespace AgentTaskKnowledge.Core;

/// <summary>Best-effort parse of task md cards (bullet meta or section:meta).</summary>
public static partial class TaskCardParser
{
    [GeneratedRegex(@"^\s*-\s*task_id:\s*`?(?<v>[^`\r\n]+)`?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TaskIdLine();

    [GeneratedRegex(@"^\s*-\s*status:\s*`?(?<v>[^`\r\n]+)`?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex StatusLine();

    [GeneratedRegex(@"^\s*-\s*title:\s*`?(?<v>[^`\r\n]+)`?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TitleLine();

    [GeneratedRegex(@"^\s*-\s*part:\s*`?(?<v>[^`\r\n]+)`?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PartLine();

    [GeneratedRegex(@"^\s*-\s*epic_id:\s*`?(?<v>[^`\r\n]+)`?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex EpicLine();

    [GeneratedRegex(@"^\s*-\s*criterion:\s*`?(?<v>[^`\r\n]+)`?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex CriterionLine();

    [GeneratedRegex(@"^\s*-\s*blocked_by:\s*(?<v>.+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockedByLine();

    [GeneratedRegex(@"^\s*-\s*unlocks:\s*(?<v>.+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex UnlocksLine();

    [GeneratedRegex(@"^#\s+Task\s+[—\-]\s+(?<t>.+)\s*$", RegexOptions.Multiline)]
    private static partial Regex TitleHeading();

    public static TaskCardSummary Parse(string taskIdFallback, string path, string markdown)
    {
        var body = MarkdownSections.TryGet(markdown, "meta") ?? markdown;
        var substance = MarkdownSections.TryGet(markdown, "substance") ?? markdown;

        var taskId = MatchOne(TaskIdLine(), body) ?? taskIdFallback;
        var status = MatchOne(StatusLine(), body);
        var title = MatchOne(TitleLine(), body) ?? MatchOne(TitleHeading(), markdown);
        var part = MatchOne(PartLine(), body);
        var epic = MatchOne(EpicLine(), body);
        var criterion = MatchOne(CriterionLine(), substance) ?? MatchOne(CriterionLine(), body);
        var blocked = ParseList(MatchOne(BlockedByLine(), body));
        var unlocks = ParseList(MatchOne(UnlocksLine(), body));

        return new TaskCardSummary(
            TaskId: taskId.Trim(),
            Title: title?.Trim(),
            Status: status?.Trim().Trim('`'),
            Part: part?.Trim().Trim('`'),
            EpicId: epic?.Trim().Trim('`'),
            BlockedBy: blocked,
            Unlocks: unlocks,
            Path: path,
            Criterion: criterion?.Trim().Trim('`'));
    }

    private static string? MatchOne(Regex re, string text)
    {
        var m = re.Match(text ?? "");
        return m.Success ? m.Groups[m.Groups["v"].Success ? "v" : "t"].Value.Trim() : null;
    }

    private static IReadOnlyList<string> ParseList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        var s = raw.Trim();
        if (s is "[]" or "—" or "-")
            return [];

        var list = new List<string>();
        foreach (Match m in BacktickToken().Matches(s))
        {
            var t = m.Groups[1].Value.Trim();
            if (t.Length > 0)
                list.Add(t);
        }

        if (list.Count > 0)
            return list;

        s = s.Trim().TrimStart('[').TrimEnd(']');
        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = part.Trim().Trim('`');
            if (t.Length > 0)
                list.Add(t);
        }

        return list;
    }

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex BacktickToken();
}
