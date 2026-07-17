namespace AgentTaskKnowledge.Core;

/// <summary>Resolves TaskKnowledge store roots from TOML primary / named / read-only ids.</summary>
public static class TaskKnowledgeRootResolution
{
    public static void EnsureWritableStoreDir(string storeDir)
    {
        if (!TaskKnowledgeRuntime.IsConfigured)
            return;

        var settings = TaskKnowledgeRuntime.Settings;
        foreach (var ro in settings.ReadOnlyTaskKnowledgeRoots)
        {
            if (PathsEqual(ro.Path, storeDir))
                throw new InvalidOperationException(
                    $"TaskKnowledge store '{storeDir}' is read-only (id '{ro.Id}'). Writes go to primary scoped store.");
        }
    }

    internal static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path.GetFullPath(b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    internal static string FormatKnownRootIds(LocalSettings settings)
    {
        var ids = settings.TaskKnowledgeRoots.Keys
            .Concat(settings.ReadOnlyTaskKnowledgeRoots.Select(r => r.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", ids);
    }
}
