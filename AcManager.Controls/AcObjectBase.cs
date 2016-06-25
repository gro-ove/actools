using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls {
    [ContentProperty("Content")]
    public class AcObjectBase : Control {
        static AcObjectBase() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcObjectBase), new FrameworkPropertyMetadata(typeof(AcObjectBase)));
        }

        public AcObjectBase() {
            SetCurrentValue(ToolBarsProperty, new Collection<ToolBar>());
        }

        private Grid _main;
        private AcObjectHeaderSection _iconImage;
        private AcToolBar _toolBar;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_main != null) {
                _main.MouseRightButtonUp -= Main_OnMouseUp;
            }

            if (_toolBar != null) {
                _toolBar.PreviewMouseUp -= ToolBar_OnMouseUp;
            }

            if (_iconImage != null) {
                _iconImage.IconMouseDown -= Header_IconMouseDown;
            }

            _main = GetTemplateChild("PART_Main") as Grid;
            _toolBar = GetTemplateChild("PART_ToolBar") as AcToolBar;
            _iconImage = GetTemplateChild("PART_Header") as AcObjectHeaderSection;

            if (_main != null) {
                _main.MouseRightButtonUp += Main_OnMouseUp;
            }

            if (_toolBar != null) {
                _toolBar.PreviewMouseUp += ToolBar_OnMouseUp;
            }

            if (_iconImage != null) {
                _iconImage.IconMouseDown += Header_IconMouseDown;
            }
        }

        protected async void Main_OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (_toolBar == null) return;

            await Task.Delay(1);
            if (e.Handled) return;

            e.Handled = true;
            _toolBar.IsActive = !_toolBar.IsActive;
        }

        protected void ToolBar_OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;
            if (_toolBar == null) return;
            _toolBar.IsActive = false;
        }

        private void Header_IconMouseDown(object sender, MouseButtonEventArgs e) {
            IconMouseDown?.Invoke(sender, e);
        }

        public event MouseButtonEventHandler IconMouseDown;

        public static readonly DependencyProperty AcObjectProperty = DependencyProperty.Register(nameof(AcObject), typeof(AcObjectNew),
                typeof(AcObjectBase));

        public AcObjectNew AcObject {
            get { return (AcObjectNew)GetValue(AcObjectProperty); }
            set { SetValue(AcObjectProperty, value); }
        }

        public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(nameof(ShowIcon), typeof(bool),
                typeof(AcObjectBase));

        public bool ShowIcon {
            get { return (bool)GetValue(ShowIconProperty); }
            set { SetValue(ShowIconProperty, value); }
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string),
                typeof(AcObjectBase));

        public string Icon {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(nameof(IconSource), typeof(ImageSource),
                typeof(AcObjectBase));

        public ImageSource IconSource {
            get { return (ImageSource)GetValue(IconSourceProperty); }
            set { SetValue(IconSourceProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object),
                typeof(AcObjectBase));

        public object Content {
            get { return GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ToolBarsProperty = DependencyProperty.Register(nameof(ToolBars), typeof(Collection<ToolBar>),
                typeof(AcObjectBase));

        public Collection<ToolBar> ToolBars {
            get { return (Collection<ToolBar>)GetValue(ToolBarsProperty); }
            set { SetValue(ToolBarsProperty, value); }
        }
    }
}
