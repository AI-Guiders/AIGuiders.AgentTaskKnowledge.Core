using System.Reflection;

namespace AgentTaskKnowledge.Core;

internal static class BundledTaskKnowledgeContent
{
    internal static bool TryReadEmbeddedText(string resourceRelativePath, out string text)
    {
        text = "";
        var assembly = typeof(BundledTaskKnowledgeContent).Assembly;
        var name = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceRelativePath.Replace('/', '.'), StringComparison.OrdinalIgnoreCase)
                                 || n.EndsWith(resourceRelativePath, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return false;

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
            return false;

        using var reader = new StreamReader(stream);
        text = reader.ReadToEnd();
        return true;
    }
}
