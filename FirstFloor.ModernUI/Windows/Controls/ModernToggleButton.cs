using System.Windows;
using System.Windows.Input;

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

        public static readonly DependencyProperty MoreCommandProperty = DependencyProperty.Register(nameof(MoreCommand), typeof(ICommand),
            typeof(ModernToggleButton));

        public ICommand MoreCommand {
            get { return (ICommand)GetValue(MoreCommandProperty); }
            set { SetValue(MoreCommandProperty, value); }
        }
    }
}