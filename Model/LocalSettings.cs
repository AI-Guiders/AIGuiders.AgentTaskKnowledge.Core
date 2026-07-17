namespace AgentTaskKnowledge.Core;

/// <summary>Effective settings after merging embedded defaults with user TOML (<c>--config</c>).</summary>
public sealed class LocalSettings
{
    public required int SchemaVersion { get; init; }

    /// <summary>Absolute path to primary TaskKnowledge anchor root (scopes live under here).</summary>
    public required string PrimaryTaskKnowledgeRoot { get; init; }

    public required IReadOnlyDictionary<string, string> TaskKnowledgeRoots { get; init; }

    public required IReadOnlyList<ReadOnlyTaskKnowledgeRoot> ReadOnlyTaskKnowledgeRoots { get; init; }

    public required WorkspaceSettings Workspace { get; init; }

    public required StoreSettings Store { get; init; }
}

public sealed class WorkspaceSettings
{
    public required string DefaultScope { get; init; }

    /// <summary>Path relative to <see cref="LocalSettings.PrimaryTaskKnowledgeRoot"/>.</summary>
    public required string ScopeMapRelative { get; init; }

    public required string ScopeAliasMapRelative { get; init; }
}

public sealed class ReadOnlyTaskKnowledgeRoot
{
    public required string Id { get; init; }

    /// <summary>Absolute path to a <c>.task-knowledge/</c> store directory.</summary>
    public required string Path { get; init; }
}

public enum StoreMode
{
    WorkspaceLocal,
    ScopeSubdir,
    Hybrid,
}

public sealed class StoreSettings
{
    public required string DirName { get; init; }

    public required StoreMode Mode { get; init; }

    /// <summary>Subdirectory under primary for <see cref="StoreMode.ScopeSubdir"/> / hybrid central path.</summary>
    public required string ScopeSubdir { get; init; }
}
