# AIGuiders.AgentTaskKnowledge.Core

.NET library **`AgentTaskKnowledge.Core`**: markdown TaskKnowledge store (analytics + task DAG), workspace-aware resolution via TOML (`--config`), epic focus for `route_next`. Used by **[agent-task-knowledge-mcp](https://github.com/AI-Guiders/agent-task-knowledge-mcp)** and in-proc hosts (e.g. Cascade IDE).

Not KB/ADR — workflow cards under resolved `.task-knowledge/`.

## Install

```bash
dotnet add package AIGuiders.AgentTaskKnowledge.Core
```

Public API: `ITaskKnowledgeStore` / `TaskKnowledgeStore`, `TaskKnowledgeBootstrap`, `TaskKnowledgeRuntime`, `LocalSettingsLoader`.

## Build

```bash
dotnet test AgentTaskKnowledge.Core.Tests -c Release
dotnet pack -c Release -o nupkg
```

Version/metadata: `AgentTaskKnowledge.Core.csproj`. License: [MIT](LICENSE).
