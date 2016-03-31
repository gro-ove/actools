using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernToggleButton : ModernButton {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool),
                typeof(ModernToggleButton), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsChecked {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        protected override void OnClick() {
            IsChecked = !IsChecked;
        }
    }
}