using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public interface INumberInputConverter {
        double? TryToParse([NotNull] string value);

        [CanBeNull]
        string BackToString(double value);
    }
}