using System.Windows.Input;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Profile {
    public sealed class LapTimesExtraTool : Displayable {
        public LapTimesExtraTool(string displayName, string hint, ICommand command) {
            DisplayName = displayName;
            Hint = hint;
            Command = command;
        }

        public string Hint { get; }
        public ICommand Command { get; }
    }
}