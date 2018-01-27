using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Controls.Helpers {
    public static class CupUi {
        public static ICupSupportedObject GetObject(DependencyObject obj) {
            return (ICupSupportedObject)obj.GetValue(ObjectProperty);
        }

        public static void SetObject(DependencyObject obj, ICupSupportedObject value) {
            obj.SetValue(ObjectProperty, value);
        }

        public static readonly DependencyProperty ObjectProperty = DependencyProperty.RegisterAttached("Object", typeof(ICupSupportedObject),
                typeof(CupUi), new UIPropertyMetadata(OnObjectChanged));

        private static void OnObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ButtonBase element && e.NewValue is ICupSupportedObject newValue) {
                element.PreviewMouseLeftButtonUp -= OnButtonMouseUp;
                element.PreviewMouseLeftButtonUp += OnButtonMouseUp;

                element.Command = new AsyncCommand(() => CupClient.Instance.InstallUpdateAsync(newValue.CupContentType, newValue.Id));
                element.SetBinding(UIElement.VisibilityProperty, new Binding {
                    Path = new PropertyPath(nameof(newValue.IsCupUpdateAvailable)),
                    Converter = new BooleanToVisibilityConverter(),
                    Source = newValue
                });

                ToolTips.SetCupUpdate(d, newValue);
                ContextMenus.SetCupUpdate(d, newValue);
            } else {
                ((FrameworkElement)d).Visibility = Visibility.Collapsed;
            }
        }

        private static void OnButtonMouseUp(object sender, MouseButtonEventArgs mouseButtonEventArgs) {
            if (Keyboard.Modifiers != ModifierKeys.None) {
                var cup = GetObject((DependencyObject)sender);
                if (cup == null) return;

                mouseButtonEventArgs.Handled = true;
                new CupInformationDialog(cup).ShowDialogAsync().Forget();
            }
        }
    }
}