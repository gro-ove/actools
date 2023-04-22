using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernTabLinksComboBox : BetterComboBox {
        public static readonly DependencyProperty IsAnySelectedProperty = DependencyProperty.Register(nameof(IsAnySelected), typeof(bool),
                typeof(ModernTabLinksComboBox), new PropertyMetadata(false, (o, e) => {
                    ((ModernTabLinksComboBox)o)._isAnySelected = (bool)e.NewValue;
                }));

        private bool _isAnySelected;

        public bool IsAnySelected {
            get => _isAnySelected;
            set => SetValue(IsAnySelectedProperty, value);
        }
        
        private ModernTab _parent;
        
        public ModernTabLinksComboBox() {
            PreviewMouseUp += (sender, args) => {
                if (_parent == null) return;
                var popup = this.FindVisualChild<Popup>();
                if (popup?.IsOpen == true) {
                    popup.IsOpen = false;
                    var newUrl = (SelectedItem as Link)?.Source;
                    _parent.SelectedSource = newUrl;
                }
            };
            Loaded += (sender, args) => {
                _parent = this.GetParent<ModernTab>();
                if (_parent == null) return;
                _parent.SelectedSourceChanged += OnParentSelectedSourceChanged;
            };
            Unloaded += (sender, args) => {
                if (_parent == null) return;
                _parent.SelectedSourceChanged -= OnParentSelectedSourceChanged;
            };
        }

        private void OnParentSelectedSourceChanged(object o, SourceEventArgs eventArgs) {
            var selected = ItemsSource.OfType<Link>().FirstOrDefault(x => x.Source == eventArgs.Source);
            if (selected != null && SelectedItem != selected) {
                SelectedItem = selected;
            }
            IsAnySelected = selected != null;
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            if (SelectedItem is Link selected && _parent != null && _parent.SelectedSource != selected.Source) {
                _parent.SelectedSource = selected.Source;
            }
        }
    }
}