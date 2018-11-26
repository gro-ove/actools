using System;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DirectInputButton : InputProviderBase<bool>, IDirectInputProvider {
        public string DefaultName { get; }

        public DirectInputButton([NotNull] IDirectInputDevice device, int id) : base(id) {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            DefaultName = string.Format(ToolsStrings.Input_Button, (id + 1).ToInvariantString());
            SetDisplayParams(null, true);
        }

        protected override void SetDisplayName(string displayName) {
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
                ShortName = displayName?.ToTitle() ?? (Id + 1).As<string>();
                DisplayName = string.Format(ToolsStrings.Input_Button, ShortName);
            }
        }

        [NotNull]
        public IDirectInputDevice Device { get; }
    }
}