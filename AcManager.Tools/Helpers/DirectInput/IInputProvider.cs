using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IInputProvider : IWithId<int> {
        string DisplayName { get; }

        string ShortName { get; }
    }
}