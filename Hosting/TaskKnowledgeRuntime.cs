namespace AgentTaskKnowledge.Core;

/// <summary>Process-wide settings from <c>--config</c>.</summary>
public static class TaskKnowledgeRuntime
{
    public static bool IsConfigured { get; private set; }

    public static LocalSettings Settings { get; private set; } = null!;

    public static string? ConfigPath { get; private set; }

    public static void Initialize(LocalSettings settings, string? configPath)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ConfigPath = configPath;
        IsConfigured = true;
    }

    public static void ResetForTests()
    {
        IsConfigured = false;
        Settings = null!;
        ConfigPath = null;
    }
}
