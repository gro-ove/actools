using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IInputProvider : IWithId<int> {
        string DisplayName { get; }
        string ShortName { get; }
        string DefaultShortName { get; }
        string DefaultDisplayName { get; }
        void SetDisplayParams([CanBeNull] string displayName, bool isVisible);
        bool IsVisible { get; }
    }
}