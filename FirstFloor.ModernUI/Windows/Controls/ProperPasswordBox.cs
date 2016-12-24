using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Use it only if password is not really important — it will be stored in
    /// memory in plain view.
    /// </summary>
    public class ProperPasswordBox : Control {
        public ProperPasswordBox() {
            DefaultStyleKey = typeof(ProperPasswordBox);
            TogglePasswordVisibilityCommand = new DelegateCommand(TogglePasswordVisibility);
        }

        private PasswordBox _passwordBox;
        private TextBox _textBox;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_passwordBox != null) {
                _passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                _passwordBox.PreviewKeyDown -= FocusAdvancement.OnKeyDown;
            }

            _passwordBox = GetTemplateChild("PART_PasswordBox") as PasswordBox;
            _textBox = GetTemplateChild("PART_TextBox") as TextBox;

            if (_passwordBox != null) {
                _passwordBox.Password = Password;
                _passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                _passwordBox.PreviewKeyDown += FocusAdvancement.OnKeyDown;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            if (_skipNext) return;
            Password = _passwordBox.Password;
        }

        protected override void OnGotFocus(RoutedEventArgs e) {
            base.OnGotFocus(e);

            if (VisiblePassword) {
                _textBox?.Focus();
            } else {
                _passwordBox?.Focus();
            }
        }

        public void SelectAll() {
            if (VisiblePassword) {
                _textBox?.SelectAll();
            } else {
                _passwordBox?.SelectAll();
            }
        }

        private bool _skipNext;

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(string),
                typeof(ProperPasswordBox), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

        public string Password {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        private static void OnPasswordChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ProperPasswordBox)o).OnPasswordChanged((string)e.NewValue);
        }

        private void OnPasswordChanged(string newValue) {
            if (_passwordBox == null || !VisiblePassword) return;
            _skipNext = true;
            _passwordBox.Password = newValue;
            _skipNext = false;
        }

        public static readonly DependencyProperty VisiblePasswordProperty = DependencyProperty.Register(nameof(VisiblePassword), typeof(bool),
                typeof(ProperPasswordBox));

        public bool VisiblePassword {
            get { return (bool)GetValue(VisiblePasswordProperty); }
            set { SetValue(VisiblePasswordProperty, value); }
        }

        public static readonly DependencyProperty TogglePasswordVisibilityCommandProperty = DependencyProperty.Register(
                nameof(TogglePasswordVisibilityCommand), typeof(DelegateCommand),
                typeof(ProperPasswordBox));

        public DelegateCommand TogglePasswordVisibilityCommand {
            get { return (DelegateCommand)GetValue(TogglePasswordVisibilityCommandProperty); }
            set { SetValue(TogglePasswordVisibilityCommandProperty, value); }
        }

        public void TogglePasswordVisibility() {
            VisiblePassword = !VisiblePassword;
            if (_textBox == null || _passwordBox == null) return;

            if (_textBox.IsFocused) {
                _passwordBox.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_passwordBox, new object[] {
                    _textBox.SelectionStart, _textBox.SelectionLength
                });
                _passwordBox.Focus();
            } else if (_passwordBox.IsFocused) {
                _textBox.Focus();
            }
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(ProperPasswordBox));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        public static readonly DependencyProperty CaretBrushProperty = DependencyProperty.Register(nameof(CaretBrush), typeof(Brush),
                typeof(ProperPasswordBox));

        public Brush CaretBrush {
            get { return (Brush)GetValue(CaretBrushProperty); }
            set { SetValue(CaretBrushProperty, value); }
        }

        public static readonly DependencyProperty SelectionBrushProperty = DependencyProperty.Register(nameof(SelectionBrush), typeof(Brush),
                typeof(ProperPasswordBox));

        public Brush SelectionBrush {
            get { return (Brush)GetValue(SelectionBrushProperty); }
            set { SetValue(SelectionBrushProperty, value); }
        }
    }
}
