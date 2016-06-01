using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class DirectInputButton : BaseInputProvider<bool>, IDirectInputProvider {
        public DirectInputButton(DirectInputDevice device, int id) : base(id) {
            Device = device;
            ShortName = (id + 1).ToInvariantString();
            DisplayName = $"Button {ShortName}";
        }

        public DirectInputDevice Device { get; }
    }
}