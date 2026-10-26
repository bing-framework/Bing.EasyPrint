using System;

namespace Bing.EasyPrint.Templates;

internal static class TemplateInputDiagnostic
{
    private static readonly object Key = new object();

    internal static void Mark(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
            current.Data[Key] = true;
    }

    internal static bool IsMarked(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
            if (current.Data[Key] is true) return true;
        return false;
    }
}
