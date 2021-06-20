using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class SelectableTextBlock : BetterTextBox {
        public SelectableTextBlock() {
            DefaultStyleKey = typeof(SelectableTextBlock);
            IsReadOnly = true;
        }

        static SelectableTextBlock() {
            var defaultMetadata = (FrameworkPropertyMetadata)TextProperty.GetMetadata(typeof(TextBox));
            var newMetadata = new FrameworkPropertyMetadata(
                    defaultMetadata.DefaultValue,
                    FrameworkPropertyMetadataOptions.None,
                    defaultMetadata.PropertyChangedCallback,
                    defaultMetadata.CoerceValueCallback,
                    defaultMetadata.IsAnimationProhibited,
                    defaultMetadata.DefaultUpdateSourceTrigger);
            TextProperty.OverrideMetadata(typeof(SelectableTextBlock), newMetadata);
            var sealedProperty = typeof(PropertyMetadata).GetProperty("Sealed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sealedProperty != null) {
                sealedProperty.SetValue(newMetadata, false);
                newMetadata.BindsTwoWayByDefault = false;
                newMetadata.Journal = false;
                sealedProperty.SetValue(newMetadata, true);
            }
        }

        private bool _tooltipSet;

        protected override void OnMouseEnter(MouseEventArgs e) {
            if (!_tooltipSet) {
                _tooltipSet = true;
                if (ToolTip == null) {
                    var toolTipText = new TextBlock();
                    toolTipText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) {
                        Source = this,
                        StringFormat = "{0} (click to copy)"
                    });
                    ToolTip = new ToolTip { Content = toolTipText };
                }
            }
            base.OnMouseEnter(e);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
            base.OnPreviewMouseUp(e);
            if (e.ClickCount == 1 && SelectionLength == 0) {
                ClipboardHelper.SetText(Text);
            }
        }
    }
}