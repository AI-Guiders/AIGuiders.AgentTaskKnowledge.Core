namespace AgentTaskKnowledge.Core;

/// <summary>
/// Compact task view for list/route_next. Includes handoff substance (to_be, criterion)
/// so agents need not invent B from chat memory or rely on discipline to read_card.
/// </summary>
public sealed record TaskCardSummary(
    string TaskId,
    string? Title,
    string? Status,
    string? Part,
    string? EpicId,
    IReadOnlyList<string> BlockedBy,
    IReadOnlyList<string> Unlocks,
    string Path,
    string? Criterion,
    string? ToBe = null,
    string? AsIs = null);
