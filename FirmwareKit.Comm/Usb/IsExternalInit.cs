#if NETSTANDARD2_0 || NETSTANDARD2_1
// LibUsbDotNet 3.0.224 declares HotplugOptions members as init-only properties; the
// netstandard2.x targets need this polyfill so the init setters can be invoked.
// Kept in its own file because it uses a block-scoped namespace, which cannot coexist
// with the file-scoped namespaces used elsewhere in this project.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
