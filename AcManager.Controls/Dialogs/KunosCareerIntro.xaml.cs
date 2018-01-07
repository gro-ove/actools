using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Presentation;
using AcManager.Tools.Objects;

namespace AcManager.Controls.Dialogs {
    public partial class KunosCareerIntro {
        public KunosCareerObject CareerObject { get; }

        public KunosCareerIntro(KunosCareerObject careerObject) {
            DataContext = this;
            CareerObject = careerObject;
            Title = CareerObject.Name;
            InitializeComponent();
            Owner = null;
            Buttons = new Button[] { };

            if (AppAppearanceManager.Instance.BlurImageViewerBackground) {
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                BlurBackground = true;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                Close();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs routedEventArgs) {
            Close();
        }
    }
}
