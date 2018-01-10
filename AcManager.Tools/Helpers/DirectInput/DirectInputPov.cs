using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DirectInputPov : DirectInputButton {
        public DirectInputPovDirection Direction { get; }

        public DirectInputPov(IDirectInputDevice device, int id, DirectInputPovDirection direction, string displayName = null) : base(device, id, displayName) {
            Direction = direction;
            var directionChar = "←↑→↓"[(int)direction];
            ShortName = displayName != null ? $"{displayName}{directionChar}" : $"P{(id + 1).ToInvariantString()}{directionChar}";
            DisplayName = $"{displayName ?? "POV" + (id + 1).ToInvariantString()}{directionChar}";
        }
    }
}