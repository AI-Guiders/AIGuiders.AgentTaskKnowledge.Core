using System.Text;

namespace AgentTaskKnowledge.Core;

/// <summary>Workspace path → scope id via TOML scope map (ported from agent-notes NotesStorage.Scope).</summary>
public static class WorkspaceScopeResolution
{
    public static string ResolveScope(string workspacePath, string? activeScope = null)
    {
        if (!TaskKnowledgeRuntime.IsConfigured)
            return "local";

        var settings = TaskKnowledgeRuntime.Settings;
        var aliases = LoadScopeAliases(settings);
        if (!string.IsNullOrWhiteSpace(activeScope))
            return NormalizeScope(activeScope, aliases);

        var mapped = TryResolveScopeFromWorkspaceMap(workspacePath, settings);
        if (!string.IsNullOrWhiteSpace(mapped))
            return NormalizeScope(mapped, aliases);

        return NormalizeScope(settings.Workspace.DefaultScope, aliases);
    }

    private static IReadOnlyDictionary<string, string> LoadScopeAliases(LocalSettings settings)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(
            settings.PrimaryTaskKnowledgeRoot,
            settings.Workspace.ScopeAliasMapRelative.Replace('/', Path.DirectorySeparatorChar));
        MergeScopeAliasFile(path, dict);
        return dict;
    }

    private static void MergeScopeAliasFile(string path, IDictionary<string, string> sink)
    {
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path, Encoding.UTF8);
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var parsed = ParseScopeMapLine(line);
            if (parsed is null || !LooksLikeScopeAliasKey(parsed.Value.Item1))
                continue;

            var alias = parsed.Value.Item1.Trim().ToLowerInvariant();
            var canonical = parsed.Value.Item2.Trim().ToLowerInvariant();
            if (alias.Length != 0 && canonical.Length != 0)
                sink[alias] = canonical;
        }
    }

    private static bool LooksLikeScopeAliasKey(string key)
    {
        var t = key.Trim();
        if (t.Length == 0)
            return false;
        foreach (var c in t)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
                continue;
            return false;
        }

        return true;
    }

    private static string NormalizeScope(string scope, IReadOnlyDictionary<string, string> aliases)
    {
        var s = scope.Trim().ToLowerInvariant();
        return aliases.TryGetValue(s, out var mapped) ? mapped : s;
    }

    private static string? TryResolveScopeFromWorkspaceMap(string workspacePath, LocalSettings settings)
    {
        var mapPath = Path.Combine(
            settings.PrimaryTaskKnowledgeRoot,
            settings.Workspace.ScopeMapRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(mapPath))
            return null;

        var mapContent = File.ReadAllText(mapPath, Encoding.UTF8);
        var normalizedWorkspace = NormalizePathKey(workspacePath);
        var lines = mapContent.Replace("\r\n", "\n").Split('\n');
        string? bestScope = null;
        var bestKeyLength = -1;
        foreach (var line in lines)
        {
            var parsed = ParseScopeMapLine(line);
            if (parsed is null)
                continue;

            var (workspaceKey, scope) = parsed.Value;
            var normalizedKey = NormalizePathKey(workspaceKey);
            if (!IsPrefixPathMatch(normalizedWorkspace, normalizedKey))
                continue;

            if (normalizedKey.Length <= bestKeyLength)
                continue;

            bestKeyLength = normalizedKey.Length;
            bestScope = scope;
        }

        return bestScope;
    }

    internal static (string workspaceKey, string scope)? ParseScopeMapLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            return null;

        if (line.StartsWith('-'))
            line = line[1..].Trim();

        var arrowParts = line.Split("=>", StringSplitOptions.TrimEntries);
        if (arrowParts.Length == 2)
            return (arrowParts[0], arrowParts[1].ToLowerInvariant());

        var colonParts = line.Split(':', 2, StringSplitOptions.TrimEntries);
        if (colonParts.Length == 2)
            return (colonParts[0], colonParts[1].ToLowerInvariant());

        var eqParts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (eqParts.Length == 2)
            return (eqParts[0], eqParts[1].ToLowerInvariant());

        return null;
    }

    private static string NormalizePathKey(string path) =>
        path.Trim().Replace('/', '\\').TrimEnd('\\');

    private static bool IsPrefixPathMatch(string workspacePath, string mapKeyPath)
    {
        if (string.Equals(workspacePath, mapKeyPath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!workspacePath.StartsWith(mapKeyPath, StringComparison.OrdinalIgnoreCase))
            return false;

        return workspacePath.Length > mapKeyPath.Length && workspacePath[mapKeyPath.Length] == '\\';
    }
}
