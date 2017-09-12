using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernToggleButton : ModernButton {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool),
                typeof(ModernToggleButton), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsChecked {
            get => GetValue(IsCheckedProperty) as bool? == true;
            set => SetValue(IsCheckedProperty, value);
        }

        protected override void OnClick() {
            IsChecked = !IsChecked;
        }

        public static readonly DependencyProperty MoreCommandProperty = DependencyProperty.Register(nameof(MoreCommand), typeof(ICommand),
            typeof(ModernToggleButton));

        public ICommand MoreCommand {
            get => (ICommand)GetValue(MoreCommandProperty);
            set => SetValue(MoreCommandProperty, value);
        }

        private ContextMenuButton _contextMenuButton;

        public override void OnApplyTemplate() {
            if (_contextMenuButton != null) {
                _contextMenuButton.Click -= OnContextMenuButtonClick;
                _contextMenuButton.PreviewMouseDown -= OnContextMenuButtonPreviewClick;
            }

            base.OnApplyTemplate();
            _contextMenuButton = GetTemplateChild("PART_MoreButton") as ContextMenuButton;

            if (_contextMenuButton != null) {
                _contextMenuButton.Click += OnContextMenuButtonClick;
                _contextMenuButton.PreviewMouseDown += OnContextMenuButtonPreviewClick;
            }
        }

        private void OnContextMenuButtonPreviewClick(object sender, MouseButtonEventArgs e) {
            var moreCommand = MoreCommand;
            if (moreCommand != null) {
                MoreCommand?.Execute(null);
                e.Handled = true;
            }
        }

        private void OnContextMenuButtonClick(object sender, ContextMenuButtonEventArgs e) {
            var moreCommand = MoreCommand;
            if (moreCommand != null) {
                MoreCommand?.Execute(null);
                e.Handled = true;
            }
        }

        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e) {
            var moreCommand = MoreCommand;
            if (moreCommand != null) {
                MoreCommand?.Execute(null);
                e.Handled = true;
            }
        }
    }
}