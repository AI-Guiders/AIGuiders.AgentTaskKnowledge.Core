namespace AgentTaskKnowledge.Core;

/// <summary>Host-facing TaskKnowledge store (MCP, CIDE-in-proc, NuGet consumers).</summary>
public interface ITaskKnowledgeStore
{
    ResolvedStore ResolveStoreRoot(
        string workspacePath,
        string? activeScope = null,
        string? storeRootId = null);

    void EnsureLayout(string workspacePath, string? activeScope = null, string? storeRootId = null);

    string ReadCard(
        string workspacePath,
        string relativePath,
        string? activeScope = null,
        string? storeRootId = null);

    void WriteCard(
        string workspacePath,
        string relativePath,
        string content,
        string? activeScope = null,
        string? storeRootId = null);

    string UpsertSection(
        string workspacePath,
        string relativePath,
        string sectionId,
        string content,
        string? activeScope = null,
        string? storeRootId = null);

    IReadOnlyList<TaskCardSummary> ListTasks(
        string workspacePath,
        string? statusFilter = null,
        string? activeScope = null,
        string? storeRootId = null,
        string? epicId = null);

    TaskCardSummary? GetTask(
        string workspacePath,
        string taskId,
        string? activeScope = null,
        string? storeRootId = null);

    IReadOnlyList<TaskCardSummary> RouteNext(
        string workspacePath,
        string? query,
        int limit = 5,
        string? activeScope = null,
        string? storeRootId = null,
        string? epicId = null);

    string UpsertTask(
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
        string? storeRootId = null);

    string UpsertAnalytics(
        string workspacePath,
        string analyticsId,
        string content,
        string? activeScope = null,
        string? storeRootId = null);
}
