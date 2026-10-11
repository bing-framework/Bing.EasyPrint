using System;

namespace Bing.EasyPrint.Templates;

/// <summary>为已有异常附加绑定字段路径，保持旧调用方观察到的异常类型。</summary>
internal static class TemplateBindingDiagnostic
{
    private static readonly object Key = new object();

    internal static void Attach(Exception error, string fieldPath)
    {
        if (GetFieldPath(error) == null)
            error.Data[Key] = new FieldReference(fieldPath);
    }

    internal static string GetFieldPath(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
            if (current.Data[Key] is FieldReference reference) return reference.Path;
        return null;
    }

    private sealed class FieldReference
    {
        internal FieldReference(string path) { Path = path; }
        internal string Path { get; }
    }
}
