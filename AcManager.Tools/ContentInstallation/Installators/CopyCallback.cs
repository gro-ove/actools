using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    /// <summary>
    /// Takes file information and, if copy needed, returns destination path.
    /// </summary>
    [CanBeNull]
    public delegate string CopyCallback([NotNull] IFileInfo info);
}