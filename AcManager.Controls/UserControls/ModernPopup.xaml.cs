using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;

namespace AcManager.Controls.UserControls {
    [ContentProperty(nameof(Content))]
    public partial class ModernPopup {
        public ModernPopup() {
            InitializeComponent();
            Root.DataContext = this;
            CustomPopupPlacementCallback += OnPlacementCallback;
        }

        private static CustomPopupPlacement[] OnPlacementCallback(Size popupSize, Size targetSize, Point offset) {
            return new[] {
                new CustomPopupPlacement {
                    Point = new Point(0, targetSize.Height)
                }
            };
        }

        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(nameof(Padding), typeof(Thickness),
                typeof(ModernPopup), new PropertyMetadata(new Thickness(4d)));

        public Thickness Padding {
            get => GetValue(PaddingProperty) as Thickness? ?? default;
            set => SetValue(PaddingProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object),
                typeof(ModernPopup), new PropertyMetadata(OnContentChanged));

        public object Content {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        private static void OnContentChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernPopup)o).OnContentChanged(e.OldValue as FrameworkElement, e.NewValue as FrameworkElement);
        }

        private void OnContentChanged(FrameworkElement oldValue, FrameworkElement newValue) {
            if (oldValue != null) {
                BindingOperations.ClearBinding(oldValue, DataContextProperty);
            }

            if (newValue != null && newValue.DataContext == null) {
                newValue.SetBinding(DataContextProperty, new Binding {
                    Path = new PropertyPath(nameof(DataContext)),
                    Source = this
                });
            }
        }
    }
}
