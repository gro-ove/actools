using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class DirectInputAxle : BaseInputProvider<double> {
        public DirectInputAxle(DirectInputDevice device, int id) : base(id) {
            Device = device;
            ShortName = (id + 1).ToInvariantString();
            DisplayName = $"Axle {ShortName}";
        }

        public DirectInputDevice Device { get; }
    }
}