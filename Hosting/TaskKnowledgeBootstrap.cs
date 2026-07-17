namespace AgentTaskKnowledge.Core;

/// <summary>CLI / env resolution for TaskKnowledge MCP TOML.</summary>
public static class TaskKnowledgeBootstrap
{
    public const string ConfigEnvVar = "AGENT_TASK_KNOWLEDGE_CONFIG";

    public const int ExitInvalidConfig = 1;

    public static string? LoadedConfigPath { get; private set; }

    public static string? ResolveConfigPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--config" or "--config-file")
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing path after {arg}.");
                return args[i + 1].Trim();
            }

            const string prefix = "--config=";
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
                return arg[prefix.Length..].Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable(ConfigEnvVar);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }

    /// <summary>Load settings for MCP startup. Returns 0 on success (config optional).</summary>
    public static int TryLoadSettings(string[] args, out LocalSettings? settings, out string? errorMessage)
    {
        settings = null;
        errorMessage = null;
        LoadedConfigPath = null;
        string? configPath;
        try
        {
            configPath = ResolveConfigPath(args);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return ExitInvalidConfig;
        }

        if (configPath is null)
            return 0;

        try
        {
            LoadedConfigPath = Path.GetFullPath(configPath);
            settings = LocalSettingsLoader.Load(configPath);
            return 0;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to load config '{configPath}': {ex.Message}";
            return ExitInvalidConfig;
        }
    }
}
