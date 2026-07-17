using System.Text;
using Xunit;
using AgentTaskKnowledge.Core;

namespace AgentTaskKnowledge.Core.Tests;

public sealed class TaskKnowledgeStoreTests
{
    [Fact]
    public void Upsert_list_route_roundtrip_legacy()
    {
        TaskKnowledgeRuntime.ResetForTests();
        var root = Path.Combine(Path.GetTempPath(), "atk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new TaskKnowledgeStore();
        try
        {
            store.UpsertTask(
                root, "A", title: "First", part: "core", epicId: "e1",
                asIs: "none", toBe: "done A", why: "test", criterion: "file A exists",
                blockedBy: null, unlocks: ["B"], memberPaths: null, sources: null,
                status: "done");

            store.UpsertTask(
                root, "B", title: "Second", part: "core", epicId: "e1",
                asIs: "blocked", toBe: "done B", why: "test", criterion: "file B exists",
                blockedBy: ["A"], unlocks: null, memberPaths: null, sources: null,
                status: "pending");

            var next = store.RouteNext(root, query: null, limit: 5);
            Assert.Contains(next, t => t.TaskId == "B");
            Assert.Equal("ready", next.First(t => t.TaskId == "B").Status);

            var focused = store.RouteNext(root, query: null, limit: 5, epicId: "e1");
            Assert.Contains(focused, t => t.TaskId == "B");
            Assert.Empty(store.RouteNext(root, query: null, limit: 5, epicId: "nope"));

            var md = store.ReadCard(root, "tasks/B.md");
            Assert.Contains("section:meta", md, StringComparison.Ordinal);
            Assert.Contains("criterion:", md, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void UpsertSection_roundtrip()
    {
        TaskKnowledgeRuntime.ResetForTests();
        var root = Path.Combine(Path.GetTempPath(), "atk-sec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new TaskKnowledgeStore();
        try
        {
            store.WriteCard(root, "analytics/demo.md", "# Demo\n");
            store.UpsertSection(root, "analytics/demo.md", "facts", "- f1: hello\n");
            var md = store.ReadCard(root, "analytics/demo.md");
            Assert.Contains("<!-- section:facts -->", md, StringComparison.Ordinal);
            Assert.Equal("- f1: hello", MarkdownSections.TryGet(md, "facts"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Hybrid_prefers_local_then_central()
    {
        TaskKnowledgeRuntime.ResetForTests();
        var primary = Path.Combine(Path.GetTempPath(), "atk-primary-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(Path.GetTempPath(), "atk-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(primary);
        Directory.CreateDirectory(workspace);

        var scopeMap = Path.Combine(primary, "scope-map.md");
        File.WriteAllText(scopeMap, $"{workspace} => test-scope\n", Encoding.UTF8);

        var configToml = Path.Combine(primary, "tk.toml");
        File.WriteAllText(configToml,
            $"""
            version = 1
            [task_knowledge]
            primary = "{primary.Replace('\\', '/')}"
            [workspace]
            default_scope = "fallback"
            scope_map = "scope-map.md"
            scope_aliases = "missing-aliases.md"
            [workspace.store]
            mode = "hybrid"
            """, Encoding.UTF8);

        try
        {
            TaskKnowledgeRuntime.Initialize(LocalSettingsLoader.Load(configToml), configToml);
            var store = new TaskKnowledgeStore();

            var central = store.ResolveStoreRoot(workspace);
            Assert.Equal("hybrid_central", central.ResolutionMode);
            Assert.Equal("test-scope", central.ResolvedScope);
            Assert.EndsWith(
                Path.Combine("scopes", "test-scope", ".task-knowledge"),
                central.StoreDir,
                StringComparison.OrdinalIgnoreCase);

            Directory.CreateDirectory(Path.Combine(workspace, ".task-knowledge"));
            var local = store.ResolveStoreRoot(workspace);
            Assert.Equal("hybrid_local", local.ResolutionMode);
            Assert.Equal(Path.Combine(workspace, ".task-knowledge"), local.StoreDir);
        }
        finally
        {
            TaskKnowledgeRuntime.ResetForTests();
            try { Directory.Delete(primary, recursive: true); } catch { /* ignore */ }
            try { Directory.Delete(workspace, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Cross_epic_blocker_still_blocks_when_focusing_epic()
    {
        TaskKnowledgeRuntime.ResetForTests();
        var root = Path.Combine(Path.GetTempPath(), "atk-xepic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new TaskKnowledgeStore();
        try
        {
            store.UpsertTask(
                root, "blocker", title: "Other epic blocker", part: "x", epicId: "epic-a",
                asIs: null, toBe: null, why: null, criterion: null,
                blockedBy: null, unlocks: null, memberPaths: null, sources: null,
                status: "pending");

            store.UpsertTask(
                root, "focused", title: "Focused", part: "y", epicId: "epic-b",
                asIs: null, toBe: null, why: null, criterion: null,
                blockedBy: ["blocker"], unlocks: null, memberPaths: null, sources: null,
                status: "pending");

            var next = store.RouteNext(root, query: null, limit: 10, epicId: "epic-b");
            Assert.Empty(next);

            var listed = store.ListTasks(root, epicId: "epic-b");
            Assert.Single(listed);
            Assert.Equal("blocked", listed[0].Status);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }
}
