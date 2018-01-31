using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DirectInputPov : DirectInputButton {
        public DirectInputPovDirection Direction { get; }

        public DirectInputPov(IDirectInputDevice device, int id, DirectInputPovDirection direction) : base(device, id) {
            Direction = direction;
            SetDisplayParams(null, true);
        }

        public static string ToFullName(string shortName) {
            var value = shortName.ApartFromFirst("P");
            return value.Length == 0 || char.IsDigit(value[0]) ? $"POV{value}" : $"POV {value}";
        }

        protected override void SetDisplayName(string displayName) {
            var directionChar = @"←↑→↓"[(int)Direction];
            if (displayName?.Length > 2) {
                var index = displayName.IndexOf(';');
                if (index != -1) {
                    ShortName = displayName.Substring(0, index) + directionChar;
                    DisplayName = $@"{displayName.Substring(index + 1).ToTitle()} {directionChar}";
                } else {
                    var abbreviation = displayName.Where((x, i) => i == 0 || char.IsWhiteSpace(displayName[i - 1])).Take(3).JoinToString();
                    ShortName = abbreviation.ToUpper() + directionChar;
                    DisplayName = $@"{displayName.ToTitle()} {directionChar}";
                }
            } else {
                var shortName = displayName ?? $@"P{(Id + 1).As<string>()}";
                ShortName = $@"{shortName}{directionChar}";
                DisplayName = $@"{ToFullName(shortName)} {directionChar}";
            }
        }
    }
}