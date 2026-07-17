using AgentTaskKnowledge.Core.Configuration;

namespace AgentTaskKnowledge.Core;

public static class LocalSettingsLoader
{
    public static LocalSettings Load(string configPath)
    {
        var user = TaskKnowledgeMcpToml.DeserializeFile<TaskKnowledgeMcpConfigDocument>(configPath);
        var defaults = TaskKnowledgeMcpToml.DeserializeEmbedded<TaskKnowledgeMcpConfigDocument>(
            "task-knowledge-mcp.defaults.toml");
        return user.Materialize(defaults);
    }
}
