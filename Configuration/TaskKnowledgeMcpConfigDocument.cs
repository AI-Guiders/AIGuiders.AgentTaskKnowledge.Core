namespace AgentTaskKnowledge.Core.Configuration;

/// <summary>Root document of local TOML (<c>--config</c>), schema version 1.</summary>
internal sealed class TaskKnowledgeMcpConfigDocument
{
    public int Version { get; set; } = 1;

    public TaskKnowledgeSection? TaskKnowledge { get; set; }

    public WorkspaceSection? Workspace { get; set; }

    public LocalSettings Materialize(TaskKnowledgeMcpConfigDocument embeddedDefaults)
    {
        var schemaVersion = Version > 0 ? Version : embeddedDefaults.Version;
        if (schemaVersion != 1)
            throw new InvalidOperationException($"Unsupported config schema version {schemaVersion}; expected 1.");

        var tk = TaskKnowledge?.Resolve()
            ?? throw new InvalidOperationException("[task_knowledge] is required in config.");

        var workspace = WorkspaceSection.Resolve(embeddedDefaults.Workspace, Workspace);
        var store = StoreSection.Resolve(embeddedDefaults.Workspace?.Store, Workspace?.Store);

        return new LocalSettings
        {
            SchemaVersion = schemaVersion,
            PrimaryTaskKnowledgeRoot = tk.PrimaryRoot,
            TaskKnowledgeRoots = tk.NamedRoots,
            ReadOnlyTaskKnowledgeRoots = tk.ReadOnlyRoots,
            Workspace = workspace,
            Store = store,
        };
    }
}

internal sealed class TaskKnowledgeSection
{
    public string? Primary { get; set; }

    public Dictionary<string, string>? Roots { get; set; }

    public List<ReadOnlyTaskKnowledgeEntry>? ReadOnly { get; set; }

    public ResolvedTaskKnowledge Resolve()
    {
        if (string.IsNullOrWhiteSpace(Primary))
            throw new InvalidOperationException("[task_knowledge].primary is required in config.");

        var namedRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Roots is not null)
        {
            foreach (var (key, path) in Roots)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                namedRoots[key] = Path.GetFullPath(path.Trim());
            }
        }

        var primaryRoot = TaskKnowledgeRootResolver.Resolve(Primary, namedRoots);
        var readOnly = ReadOnlyTaskKnowledgeEntry.ToRuntimeList(ReadOnly);
        foreach (var entry in readOnly)
        {
            if (namedRoots.ContainsKey(entry.Id))
                throw new InvalidOperationException(
                    $"[[task_knowledge.read_only]] id '{entry.Id}' conflicts with [task_knowledge.roots].");
        }

        return new ResolvedTaskKnowledge(primaryRoot, namedRoots, readOnly);
    }
}

internal sealed class ReadOnlyTaskKnowledgeEntry
{
    public string? Id { get; set; }

    public string? Path { get; set; }

    internal static IReadOnlyList<ReadOnlyTaskKnowledgeRoot> ToRuntimeList(List<ReadOnlyTaskKnowledgeEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
            return [];

        var list = new List<ReadOnlyTaskKnowledgeRoot>(entries.Count);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Path))
                throw new InvalidOperationException("Each [[task_knowledge.read_only]] entry requires id and path.");
            list.Add(new ReadOnlyTaskKnowledgeRoot
            {
                Id = entry.Id.Trim(),
                Path = System.IO.Path.GetFullPath(entry.Path.Trim()),
            });
        }

        return list;
    }
}

internal sealed class WorkspaceSection
{
    public static WorkspaceSection Example { get; } = new()
    {
        DefaultScope = "example",
        ScopeMap = "example/workspace-scope-map-v1.md",
        ScopeAliases = "example/scope-alias-map-v1.md",
        Store = StoreSection.Default,
    };

    public string? DefaultScope { get; set; }

    public string? ScopeMap { get; set; }

    public string? ScopeAliases { get; set; }

    public StoreSection? Store { get; set; }

    public static WorkspaceSettings Resolve(WorkspaceSection? embeddedDefaults, WorkspaceSection? user)
    {
        var merged = user is null
            ? embeddedDefaults ?? Example
            : (embeddedDefaults ?? Example).Overlay(user);
        return merged.ToRuntimeSettings();
    }

    private WorkspaceSection Overlay(WorkspaceSection higher) =>
        new()
        {
            DefaultScope = higher.DefaultScope ?? DefaultScope,
            ScopeMap = higher.ScopeMap ?? ScopeMap,
            ScopeAliases = higher.ScopeAliases ?? ScopeAliases,
            Store = higher.Store is not null ? StoreSection.Overlay(Store, higher.Store) : Store,
        };

    private WorkspaceSettings ToRuntimeSettings() =>
        new()
        {
            DefaultScope = (DefaultScope ?? Example.DefaultScope)!.Trim(),
            ScopeMapRelative = TaskKnowledgeRelativePath.Normalize(ScopeMap ?? Example.ScopeMap!),
            ScopeAliasMapRelative = TaskKnowledgeRelativePath.Normalize(ScopeAliases ?? Example.ScopeAliases!),
        };
}

internal sealed class StoreSection
{
    public static StoreSection Default { get; } = new()
    {
        DirName = ".task-knowledge",
        Mode = "scope_subdir",
        ScopeSubdir = "scopes",
    };

    public string? DirName { get; set; }

    public string? Mode { get; set; }

    public string? ScopeSubdir { get; set; }

    public static StoreSettings Resolve(StoreSection? embeddedDefaults, StoreSection? user)
    {
        var merged = Overlay(embeddedDefaults ?? Default, user);
        return merged.ToRuntimeSettings();
    }

    internal static StoreSection Overlay(StoreSection? lower, StoreSection? higher)
    {
        var baseSection = lower ?? Default;
        if (higher is null)
            return baseSection;
        return new StoreSection
        {
            DirName = higher.DirName ?? baseSection.DirName,
            Mode = higher.Mode ?? baseSection.Mode,
            ScopeSubdir = higher.ScopeSubdir ?? baseSection.ScopeSubdir,
        };
    }

    private StoreSettings ToRuntimeSettings()
    {
        var modeText = (Mode ?? Default.Mode)!.Trim().ToLowerInvariant();
        var mode = modeText switch
        {
            "workspace_local" => StoreMode.WorkspaceLocal,
            "scope_subdir" => StoreMode.ScopeSubdir,
            "hybrid" => StoreMode.Hybrid,
            _ => throw new InvalidOperationException(
                $"[workspace.store].mode must be workspace_local | scope_subdir | hybrid; got '{modeText}'."),
        };

        var dirName = (DirName ?? Default.DirName)!.Trim().Trim('/', '\\');
        if (string.IsNullOrWhiteSpace(dirName))
            throw new InvalidOperationException("[workspace.store].dir_name is required.");

        return new StoreSettings
        {
            DirName = dirName,
            Mode = mode,
            ScopeSubdir = (ScopeSubdir ?? Default.ScopeSubdir)!.Trim().Trim('/', '\\'),
        };
    }
}

internal readonly record struct ResolvedTaskKnowledge(
    string PrimaryRoot,
    IReadOnlyDictionary<string, string> NamedRoots,
    IReadOnlyList<ReadOnlyTaskKnowledgeRoot> ReadOnlyRoots);

internal static class TaskKnowledgeRootResolver
{
    internal static string Resolve(string primarySpec, IReadOnlyDictionary<string, string> namedRoots)
    {
        var spec = primarySpec.Trim();
        if (namedRoots.TryGetValue(spec, out var fromKey))
            return fromKey;
        if (LooksLikeFilesystemPath(spec))
            return Path.GetFullPath(spec);
        throw new InvalidOperationException(
            $"[task_knowledge].primary = \"{spec}\" is not an absolute path and was not found in [task_knowledge.roots].");
    }

    private static bool LooksLikeFilesystemPath(string spec) =>
        Path.IsPathRooted(spec)
        || spec.StartsWith("\\\\", StringComparison.Ordinal)
        || (spec.Length >= 2 && spec[1] == ':' && char.IsAsciiLetter(spec[0]));
}

internal static class TaskKnowledgeRelativePath
{
    internal static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Path relative to task_knowledge primary root is required.");
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
            throw new InvalidOperationException($"Path must be relative under primary root: {path}");
        return normalized;
    }
}
