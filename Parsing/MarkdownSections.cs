using System.Text;
using System.Text.RegularExpressions;

namespace AgentTaskKnowledge.Core;

/// <summary>AN-style <!-- section:id --> blocks (adapted, store-local).</summary>
public static partial class MarkdownSections
{
    [GeneratedRegex(
        @"<!--\s*section:(?<id>[A-Za-z0-9._-]+)\s*-->\s*(?<content>.*?)\s*<!--\s*/section:\k<id>\s*-->",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex SectionBlock();

    public static string Upsert(string existing, string sectionId, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        content ??= "";
        existing ??= "";

        var startMarker = $"<!-- section:{sectionId} -->";
        var endMarker = $"<!-- /section:{sectionId} -->";
        var sectionBlock = $"{startMarker}\n{content.TrimEnd()}\n{endMarker}";
        var start = existing.IndexOf(startMarker, StringComparison.Ordinal);
        var end = start >= 0 ? existing.IndexOf(endMarker, start, StringComparison.Ordinal) : -1;
        if (start >= 0 && end >= 0)
        {
            var before = existing[..start].TrimEnd('\r', '\n');
            var after = existing[(end + endMarker.Length)..].TrimStart('\r', '\n');
            return JoinBlocks(before, sectionBlock, after);
        }

        return JoinBlocks(existing, sectionBlock);
    }

    public static string? TryGet(string document, string sectionId)
    {
        foreach (Match m in SectionBlock().Matches(document ?? ""))
        {
            if (string.Equals(m.Groups["id"].Value, sectionId, StringComparison.OrdinalIgnoreCase))
                return m.Groups["content"].Value.Trim();
        }

        return null;
    }

    private static string JoinBlocks(params string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p))
                continue;
            if (sb.Length > 0)
                sb.Append("\n\n");
            sb.Append(p.TrimEnd());
        }

        if (sb.Length > 0)
            sb.Append('\n');
        return sb.ToString();
    }
}
