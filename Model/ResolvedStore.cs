namespace AgentTaskKnowledge.Core;

public sealed record ResolvedStore(
    string StoreDir,
    string? ResolvedScope,
    string ResolutionMode,
    string? StoreRootId = null);
