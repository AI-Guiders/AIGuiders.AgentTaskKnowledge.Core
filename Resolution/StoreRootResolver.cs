namespace AgentTaskKnowledge.Core;

/// <summary>Resolves workspace → store directory (legacy / scope_subdir / hybrid / read_only).</summary>
public sealed class StoreRootResolver
{
    public const string DefaultRelativeDir = ".task-knowledge";
    public const string RelativeDirEnvVar = "AGENT_TASK_KNOWLEDGE_DIR";

    public string RelativeDirName()
    {
        if (TaskKnowledgeRuntime.IsConfigured)
            return TaskKnowledgeRuntime.Settings.Store.DirName;

        var env = Environment.GetEnvironmentVariable(RelativeDirEnvVar);
        return string.IsNullOrWhiteSpace(env) ? DefaultRelativeDir : env.Trim().Trim('/', '\\');
    }

    public ResolvedStore Resolve(
        string workspacePath,
        string? activeScope = null,
        string? storeRootId = null)
    {
        if (!TaskKnowledgeRuntime.IsConfigured)
        {
            var ws = RequireWorkspace(workspacePath);
            return new ResolvedStore(
                Path.Combine(ws, RelativeDirName()),
                ResolvedScope: null,
                ResolutionMode: "legacy");
        }

        var settings = TaskKnowledgeRuntime.Settings;
        var primary = settings.PrimaryTaskKnowledgeRoot;
        string? resolvedRootId = null;

        if (!string.IsNullOrWhiteSpace(storeRootId))
        {
            var id = storeRootId.Trim();
            var readOnly = settings.ReadOnlyTaskKnowledgeRoots
                .FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            if (readOnly is not null)
            {
                return new ResolvedStore(
                    readOnly.Path,
                    ResolvedScope: null,
                    ResolutionMode: "read_only",
                    StoreRootId: readOnly.Id);
            }

            if (settings.TaskKnowledgeRoots.TryGetValue(id, out var named))
            {
                primary = named;
                resolvedRootId = id;
            }
            else
            {
                throw new ArgumentException(
                    $"Unknown store_root_id '{id}'. Known: {TaskKnowledgeRootResolution.FormatKnownRootIds(settings)}.");
            }
        }

        var scope = WorkspaceScopeResolution.ResolveScope(workspacePath, activeScope);
        var dirName = settings.Store.DirName;
        var wsPath = RequireWorkspace(workspacePath);

        string storeDir;
        string mode;
        switch (settings.Store.Mode)
        {
            case StoreMode.WorkspaceLocal:
                storeDir = Path.Combine(wsPath, dirName);
                mode = "workspace_local";
                break;

            case StoreMode.ScopeSubdir:
                storeDir = Path.Combine(primary, settings.Store.ScopeSubdir, scope, dirName);
                mode = "scope_subdir";
                break;

            default:
                var local = Path.Combine(wsPath, dirName);
                if (Directory.Exists(local))
                {
                    storeDir = local;
                    mode = "hybrid_local";
                }
                else
                {
                    storeDir = Path.Combine(primary, settings.Store.ScopeSubdir, scope, dirName);
                    mode = "hybrid_central";
                }

                break;
        }

        return new ResolvedStore(storeDir, scope, mode, resolvedRootId);
    }

    internal static string RequireWorkspace(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new ArgumentException("workspace_path is required.");
        return Path.GetFullPath(workspacePath.Trim());
    }
}
