namespace AgentTaskKnowledge.Core;

/// <summary>
/// Instance facade over resolve → IO → DAG → document build.
/// Reads <see cref="TaskKnowledgeRuntime"/> when configured; otherwise legacy per-workspace layout.
/// </summary>
public sealed class TaskKnowledgeStore : ITaskKnowledgeStore
{
    private readonly StoreRootResolver _resolver;
    private readonly TaskCardIO _io;
    private readonly TaskDag _dag;
    private readonly TaskCardDocument _documents;

    public TaskKnowledgeStore()
        : this(new StoreRootResolver(), new TaskCardIO(), new TaskDag(), new TaskCardDocument())
    {
    }

    public TaskKnowledgeStore(
        StoreRootResolver resolver,
        TaskCardIO io,
        TaskDag dag,
        TaskCardDocument documents)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _io = io ?? throw new ArgumentNullException(nameof(io));
        _dag = dag ?? throw new ArgumentNullException(nameof(dag));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
    }

    public ResolvedStore ResolveStoreRoot(
        string workspacePath,
        string? activeScope = null,
        string? storeRootId = null) =>
        _resolver.Resolve(workspacePath, activeScope, storeRootId);

    public void EnsureLayout(string workspacePath, string? activeScope = null, string? storeRootId = null) =>
        _io.EnsureLayout(ResolveStoreRoot(workspacePath, activeScope, storeRootId));

    public string ReadCard(
        string workspacePath,
        string relativePath,
        string? activeScope = null,
        string? storeRootId = null) =>
        _io.ReadCard(ResolveStoreRoot(workspacePath, activeScope, storeRootId), relativePath);

    public void WriteCard(
        string workspacePath,
        string relativePath,
        string content,
        string? activeScope = null,
        string? storeRootId = null) =>
        _io.WriteCard(ResolveStoreRoot(workspacePath, activeScope, storeRootId), relativePath, content);

    public string UpsertSection(
        string workspacePath,
        string relativePath,
        string sectionId,
        string content,
        string? activeScope = null,
        string? storeRootId = null) =>
        _io.UpsertSection(
            ResolveStoreRoot(workspacePath, activeScope, storeRootId),
            relativePath,
            sectionId,
            content);

    public IReadOnlyList<TaskCardSummary> ListTasks(
        string workspacePath,
        string? statusFilter = null,
        string? activeScope = null,
        string? storeRootId = null,
        string? epicId = null)
    {
        var resolved = ResolveStoreRoot(workspacePath, activeScope, storeRootId);
        var cards = _io.LoadTaskCards(resolved);
        var withEffective = _dag.WithEffectiveStatus(cards);
        return _dag.Filter(withEffective, statusFilter, epicId);
    }

    public TaskCardSummary? GetTask(
        string workspacePath,
        string taskId,
        string? activeScope = null,
        string? storeRootId = null)
    {
        var tid = TaskCardDocument.RequireToken(taskId, "task_id");
        return ListTasks(workspacePath, activeScope: activeScope, storeRootId: storeRootId)
            .FirstOrDefault(t => string.Equals(t.TaskId, tid, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<TaskCardSummary> RouteNext(
        string workspacePath,
        string? query,
        int limit = 5,
        string? activeScope = null,
        string? storeRootId = null,
        string? epicId = null)
    {
        var full = ListTasks(workspacePath, activeScope: activeScope, storeRootId: storeRootId);
        return _dag.RouteNext(full, query, limit, epicId);
    }

    public string UpsertTask(
        string workspacePath,
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
        string? activeScope = null,
        string? storeRootId = null)
    {
        var resolved = ResolveStoreRoot(workspacePath, activeScope, storeRootId);
        TaskKnowledgeRootResolution.EnsureWritableStoreDir(resolved.StoreDir);
        var rel = _documents.TaskRelativePath(taskId);
        var prior = _io.TryReadExisting(resolved, rel);
        var markdown = _documents.BuildTaskMarkdown(
            taskId, title, part, epicId, asIs, toBe, why, criterion,
            blockedBy, unlocks, memberPaths, sources, status, prior, rel);
        _io.WriteCard(resolved, rel, markdown);
        return rel;
    }

    public string UpsertAnalytics(
        string workspacePath,
        string analyticsId,
        string content,
        string? activeScope = null,
        string? storeRootId = null)
    {
        var rel = _documents.AnalyticsRelativePath(analyticsId);
        WriteCard(workspacePath, rel, content, activeScope, storeRootId);
        return rel;
    }
}
