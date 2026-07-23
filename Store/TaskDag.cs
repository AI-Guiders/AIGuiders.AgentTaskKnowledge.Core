namespace AgentTaskKnowledge.Core;

/// <summary>Effective status and route_next over a task DAG.</summary>
public sealed class TaskDag
{
    public IReadOnlyList<TaskCardSummary> WithEffectiveStatus(IReadOnlyList<TaskCardSummary> cards)
    {
        var recorded = cards.ToDictionary(
            c => c.TaskId,
            c => c.Status ?? "pending",
            StringComparer.OrdinalIgnoreCase);

        return cards
            .Select(c => c with { Status = ComputeEffectiveStatus(c, recorded) })
            .OrderBy(c => c.Status == "ready" ? 0 : c.Status == "in_progress" ? 1 : 2)
            .ThenBy(c => c.TaskId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<TaskCardSummary> Filter(
        IReadOnlyList<TaskCardSummary> withEffective,
        string? statusFilter = null,
        string? epicId = null)
    {
        IEnumerable<TaskCardSummary> views = withEffective;

        if (!string.IsNullOrWhiteSpace(epicId))
        {
            var epic = epicId.Trim();
            views = views.Where(c =>
                string.Equals(c.EpicId, epic, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var want = statusFilter.Trim();
            views = views.Where(c =>
                string.Equals(c.Status, want, StringComparison.OrdinalIgnoreCase));
        }

        return views.ToList();
    }

    /// <summary>
    /// Ready/in_progress candidates. <paramref name="fullStore"/> supplies blocker graph;
    /// <paramref name="epicId"/> focuses candidates only.
    /// </summary>
    public IReadOnlyList<TaskCardSummary> RouteNext(
        IReadOnlyList<TaskCardSummary> fullStore,
        string? query,
        int limit = 5,
        string? epicId = null)
    {
        limit = Math.Clamp(limit, 1, 20);
        var byId = fullStore.ToDictionary(t => t.TaskId, StringComparer.OrdinalIgnoreCase);

        IEnumerable<TaskCardSummary> focused = fullStore;
        if (!string.IsNullOrWhiteSpace(epicId))
        {
            var epic = epicId.Trim();
            focused = fullStore.Where(t =>
                string.Equals(t.EpicId, epic, StringComparison.OrdinalIgnoreCase));
        }

        var ready = focused.Where(t =>
        {
            if (string.Equals(t.Status, "done", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
                return string.Equals(t.Status, "in_progress", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(t.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var b in t.BlockedBy)
            {
                if (!byId.TryGetValue(b, out var blocker) ||
                    !string.Equals(blocker.Status, "done", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }).ToList();

        ready = ready
            .OrderBy(t => string.Equals(t.Status, "in_progress", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(t => t.TaskId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var tokens = Tokenize(query);
            ready = ready
                .Select(t => (t, score: Score(t, tokens)))
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.t.TaskId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.t)
                .ToList();
        }

        return ready.Take(limit).ToList();
    }

    internal static string ComputeEffectiveStatus(
        TaskCardSummary card,
        IReadOnlyDictionary<string, string> statusById)
    {
        var recorded = card.Status ?? "pending";
        if (string.Equals(recorded, "done", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recorded, "in_progress", StringComparison.OrdinalIgnoreCase))
            return recorded;

        foreach (var b in card.BlockedBy)
        {
            if (!statusById.TryGetValue(b, out var st) ||
                !string.Equals(st, "done", StringComparison.OrdinalIgnoreCase))
                return "blocked";
        }

        if (string.Equals(recorded, "blocked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recorded, "pending", StringComparison.OrdinalIgnoreCase))
            return "ready";

        return recorded;
    }

    private static int Score(TaskCardSummary t, HashSet<string> tokens)
    {
        var hay = $"{t.TaskId} {t.Title} {t.Part} {t.Criterion} {t.ToBe} {t.AsIs}".ToLowerInvariant();
        var score = 0;
        foreach (var tok in tokens)
        {
            if (hay.Contains(tok, StringComparison.Ordinal))
                score += tok.Length >= 4 ? 3 : 1;
        }

        return score;
    }

    private static HashSet<string> Tokenize(string query) =>
        query.Split([' ', '\t', '\r', '\n', ',', ';', '/', '\\', ':'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Where(s => s.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
}
