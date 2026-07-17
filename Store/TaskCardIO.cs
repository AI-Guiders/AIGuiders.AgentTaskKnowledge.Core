using System.Text;

namespace AgentTaskKnowledge.Core;

/// <summary>Filesystem layout and card file IO under a resolved store directory.</summary>
public sealed class TaskCardIO
{
    private readonly object _gate = new();


    public void EnsureLayout(ResolvedStore resolved)
    {
        if (resolved.ResolutionMode != "read_only")
            TaskKnowledgeRootResolution.EnsureWritableStoreDir(resolved.StoreDir);

        var root = resolved.StoreDir;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "tasks"));
        Directory.CreateDirectory(Path.Combine(root, "analytics"));
        Directory.CreateDirectory(Path.Combine(root, "epics"));
        Directory.CreateDirectory(Path.Combine(root, "memos"));
        var readme = Path.Combine(root, "README.md");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                "# TaskKnowledge store\n\nActive epics: (none yet)\n\nPlaybook: knowledge/domains/agent-operations/playbook-taskknowledge-adr-analytics.md\n",
                Encoding.UTF8);
        }
    }

    public string ResolveRelative(ResolvedStore resolved, string relativePath)
    {
        var rel = NormalizeStoreRelative(relativePath);
        var full = Path.GetFullPath(Path.Combine(resolved.StoreDir, rel));
        var root = Path.GetFullPath(resolved.StoreDir);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path escapes TaskKnowledge store.");
        return full;
    }

    public string ReadCard(ResolvedStore resolved, string relativePath)
    {
        var full = ResolveRelative(resolved, relativePath);
        if (!File.Exists(full))
            throw new FileNotFoundException($"Card not found: {relativePath}", full);
        return File.ReadAllText(full, Encoding.UTF8);
    }

    public void WriteCard(ResolvedStore resolved, string relativePath, string content)
    {
        TaskKnowledgeRootResolution.EnsureWritableStoreDir(resolved.StoreDir);
        EnsureLayout(resolved);
        var full = ResolveRelative(resolved, relativePath);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        lock (_gate)
        {
            File.WriteAllText(full, content ?? "", Encoding.UTF8);
        }
    }

    public string UpsertSection(ResolvedStore resolved, string relativePath, string sectionId, string content)
    {
        TaskKnowledgeRootResolution.EnsureWritableStoreDir(resolved.StoreDir);
        EnsureLayout(resolved);
        var full = ResolveRelative(resolved, relativePath);
        lock (_gate)
        {
            var existing = File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : "";
            var next = MarkdownSections.Upsert(existing, sectionId, content);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(full, next, Encoding.UTF8);
            return next;
        }
    }

    public IReadOnlyList<TaskCardSummary> LoadTaskCards(ResolvedStore resolved)
    {
        EnsureLayout(resolved);
        var dir = Path.Combine(resolved.StoreDir, "tasks");
        if (!Directory.Exists(dir))
            return [];

        var cards = new List<TaskCardSummary>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var md = File.ReadAllText(file, Encoding.UTF8);
            var rel = Path.Combine("tasks", Path.GetFileName(file)).Replace('\\', '/');
            cards.Add(TaskCardParser.Parse(id, rel, md));
        }

        return cards;
    }

    public string? TryReadExisting(ResolvedStore resolved, string relativePath)
    {
        var full = ResolveRelative(resolved, relativePath);
        return File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : null;
    }

    private static string NormalizeStoreRelative(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("relative_path is required.");
        var p = relativePath.Replace('\\', '/').Trim().TrimStart('/');
        if (p.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(p))
            throw new ArgumentException("relative_path must be store-relative without '..'.");
        return p;
    }
}
