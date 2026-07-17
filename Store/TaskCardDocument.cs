using System.Text;
using System.Text.RegularExpressions;

namespace AgentTaskKnowledge.Core;

/// <summary>Builds task / analytics markdown documents for upsert.</summary>
public sealed partial class TaskCardDocument
{
    public static readonly string[] AllowedStatus =
        ["pending", "ready", "in_progress", "done", "blocked"];

    public string BuildTaskMarkdown(
        string taskId,
        string? title,
        string? part,
        string? epicId,
        string? asIs,
        string? toBe,
        string? why,
        string? criterion,
        IReadOnlyList<string>? blockedBy,
        IReadOnlyList<string>? unlocks,
        IReadOnlyList<string>? memberPaths,
        IReadOnlyList<string>? sources,
        string? status,
        string? priorMarkdown,
        string relativePath)
    {
        var tid = RequireToken(taskId, "task_id");
        TaskCardSummary? priorCard = priorMarkdown is null
            ? null
            : TaskCardParser.Parse(tid, relativePath, priorMarkdown);

        title ??= priorCard?.Title ?? tid;
        part ??= priorCard?.Part;
        epicId ??= priorCard?.EpicId;
        status = NormalizeStatus(string.IsNullOrWhiteSpace(status) ? priorCard?.Status : status);
        blockedBy ??= priorCard?.BlockedBy;
        unlocks ??= priorCard?.Unlocks;

        var priorSubstance = priorMarkdown is null ? null : MarkdownSections.TryGet(priorMarkdown, "substance");
        asIs ??= ExtractBullet(priorSubstance, "as_is");
        toBe ??= ExtractBullet(priorSubstance, "to_be");
        why ??= ExtractBullet(priorSubstance, "why");
        criterion ??= priorCard?.Criterion ?? ExtractBullet(priorSubstance, "criterion");

        var meta = new StringBuilder();
        meta.AppendLine($"- task_id: `{tid}`");
        meta.AppendLine($"- title: {title}");
        if (!string.IsNullOrWhiteSpace(part))
            meta.AppendLine($"- part: `{part}`");
        if (!string.IsNullOrWhiteSpace(epicId))
            meta.AppendLine($"- epic_id: `{epicId}`");
        meta.AppendLine($"- status: `{status}`");
        meta.AppendLine($"- blocked_by: {FormatList(blockedBy)}");
        meta.AppendLine($"- unlocks: {FormatList(unlocks)}");

        var substance = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(asIs))
            substance.AppendLine($"- as_is: {asIs.Trim()}");
        if (!string.IsNullOrWhiteSpace(toBe))
            substance.AppendLine($"- to_be: {toBe.Trim()}");
        if (!string.IsNullOrWhiteSpace(why))
            substance.AppendLine($"- why: {why.Trim()}");
        if (!string.IsNullOrWhiteSpace(criterion))
            substance.AppendLine($"- criterion: {criterion.Trim()}");

        var bounds = new StringBuilder();
        bounds.AppendLine($"- member_paths: {FormatList(memberPaths)}");
        bounds.AppendLine($"- sources: {FormatList(sources)}");

        var doc = new StringBuilder();
        doc.AppendLine($"# Task — {title}");
        doc.AppendLine();
        doc.Append(MarkdownSections.Upsert("", "meta", meta.ToString()));
        doc.AppendLine();
        doc.Append(MarkdownSections.Upsert("", "substance", substance.ToString()));
        doc.AppendLine();
        doc.Append(MarkdownSections.Upsert("", "bounds", bounds.ToString()));
        doc.AppendLine();
        doc.Append(MarkdownSections.Upsert("", "verify", "- criterion_met: no\n- evidence:\n"));
        return doc.ToString();
    }

    public string TaskRelativePath(string taskId) =>
        $"tasks/{SanitizeFileToken(RequireToken(taskId, "task_id"))}.md";

    public string AnalyticsRelativePath(string analyticsId) =>
        $"analytics/{SanitizeFileToken(RequireToken(analyticsId, "analytics_id"))}.md";

    internal static string RequireToken(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.");
        return value.Trim();
    }

    internal static string SanitizeFileToken(string token)
    {
        var s = SafeFileChars().Replace(token.Trim(), "-");
        return string.IsNullOrWhiteSpace(s) ? "card" : s;
    }

    internal static string NormalizeStatus(string? status)
    {
        var s = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();
        if (!AllowedStatus.Contains(s, StringComparer.Ordinal))
            throw new ArgumentException($"status must be one of: {string.Join(", ", AllowedStatus)}.");
        return s;
    }

    private static string FormatList(IReadOnlyList<string>? items)
    {
        if (items is null || items.Count == 0)
            return "[]";
        return "[" + string.Join(", ", items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => $"`{i.Trim()}`")) + "]";
    }

    private static string? ExtractBullet(string? markdown, string key)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;
        var re = new Regex($@"^\s*-\s*{Regex.Escape(key)}:\s*(?<v>.+)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var m = re.Match(markdown);
        return m.Success ? m.Groups["v"].Value.Trim() : null;
    }

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex SafeFileChars();
}
