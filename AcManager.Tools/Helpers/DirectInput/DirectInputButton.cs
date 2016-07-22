using System;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class DirectInputButton : BaseInputProvider<bool>, IDirectInputProvider {
        public DirectInputButton(IDirectInputDevice device, int id) : base(id) {
            Device = device;
            ShortName = (id + 1).ToInvariantString();
            DisplayName = string.Format(ToolsStrings.Input_Button, ShortName);
        }

        public IDirectInputDevice Device { get; }
    }
}