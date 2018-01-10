using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DirectInputButton : BaseInputProvider<bool>, IDirectInputProvider {
        public string DefaultName { get; }

        public DirectInputButton(IDirectInputDevice device, int id, string displayName = null) : base(id) {
            Device = device;
            DefaultName = string.Format(ToolsStrings.Input_Button, (id + 1).ToInvariantString());

            if (displayName?.Length > 2) {
                var index = displayName.IndexOf(';');
                if (index != -1) {
                    ShortName = displayName.Substring(0, index);
                    DisplayName = displayName.Substring(index + 1).ToTitle();
                } else {
                    var abbreviation = displayName.Where((x, i) => i == 0 || char.IsWhiteSpace(displayName[i - 1])).Take(3).JoinToString();
                    ShortName = abbreviation.ToUpper();
                    DisplayName = displayName.ToTitle();
                }
            } else {
                ShortName = displayName?.ToTitle() ?? (id + 1).ToInvariantString();
                DisplayName = string.Format(ToolsStrings.Input_Button, ShortName);
            }
        }

        public IDirectInputDevice Device { get; }
    }
}