using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class TextBoxEnterKeyUpdateBehavior : Behavior<TextBox> {
        protected override void OnAttached() {
            if (AssociatedObject == null) return;
            base.OnAttached();
            AssociatedObject.KeyDown += AssociatedObject_KeyDown;
        }

        protected override void OnDetaching() {
            if (AssociatedObject == null) return;
            AssociatedObject.KeyDown -= AssociatedObject_KeyDown;
            base.OnDetaching();
        }

        private void AssociatedObject_KeyDown(object sender, KeyEventArgs e) {
            var textBox = sender as TextBox;
            if (textBox != null && e.Key == Key.Enter) {
                textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
    }
}
