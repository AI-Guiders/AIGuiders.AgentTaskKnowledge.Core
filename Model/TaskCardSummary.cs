namespace AgentTaskKnowledge.Core;

public sealed record TaskCardSummary(
    string TaskId,
    string? Title,
    string? Status,
    string? Part,
    string? EpicId,
    IReadOnlyList<string> BlockedBy,
    IReadOnlyList<string> Unlocks,
    string Path,
    string? Criterion);
