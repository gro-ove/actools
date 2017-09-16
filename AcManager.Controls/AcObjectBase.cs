using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using AcManager.Controls.Presentation;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls {
    [ContentProperty(nameof(Content))]
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
                _main.MouseRightButtonUp -= OnMainMouseUp;
            }

            if (_toolBar != null) {
                _toolBar.PreviewMouseUp -= OnToolBarMouseUp;
            }

            if (_iconImage != null) {
                _iconImage.IconMouseUp -= OnHeaderIconMouseUp;
            }

            _main = GetTemplateChild(@"PART_Main") as Grid;
            _toolBar = GetTemplateChild(@"PART_ToolBar") as AcToolBar;
            _iconImage = GetTemplateChild(@"PART_Header") as AcObjectHeaderSection;

            if (_main != null) {
                _main.MouseRightButtonUp += OnMainMouseUp;
            }

            if (_toolBar != null) {
                _toolBar.PreviewMouseUp += OnToolBarMouseUp;
            }

            if (_iconImage != null) {
                _iconImage.IconMouseUp += OnHeaderIconMouseUp;
            }
        }

        protected async void OnMainMouseUp(object sender, MouseButtonEventArgs e) {
            if (_toolBar == null || !AppAppearanceManager.Instance.PopupToolBars) return;

            await Task.Delay(1);
            if (e.Handled) return;

            e.Handled = true;
            _toolBar.IsActive = !_toolBar.IsActive;
        }

        protected void OnToolBarMouseUp(object sender, MouseButtonEventArgs e) {
            /*if (e.ChangedButton != MouseButton.Left) return;
            if (_toolBar == null) return;
            _toolBar.IsActive = false;*/
        }

        private void OnHeaderIconMouseUp(object sender, MouseButtonEventArgs e) {
            IconMouseUp?.Invoke(sender, e);
        }

        public event MouseButtonEventHandler IconMouseUp;

        public static readonly DependencyProperty AcObjectProperty = DependencyProperty.Register(nameof(AcObject), typeof(AcObjectNew),
                typeof(AcObjectBase));

        public AcObjectNew AcObject {
            get => (AcObjectNew)GetValue(AcObjectProperty);
            set => SetValue(AcObjectProperty, value);
        }

        public static readonly DependencyProperty IsNameReadOnlyProperty = DependencyProperty.Register(nameof(IsNameReadOnly), typeof(bool),
                typeof(AcObjectBase), new PropertyMetadata(false));

        public bool IsNameReadOnly {
            get => GetValue(IsNameReadOnlyProperty) as bool? ?? default(bool);
            set => SetValue(IsNameReadOnlyProperty, value);
        }

        public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(nameof(ShowIcon), typeof(bool),
                typeof(AcObjectBase));

        public bool ShowIcon {
            get => GetValue(ShowIconProperty) as bool? ?? default(bool);
            set => SetValue(ShowIconProperty, value);
        }

        public static readonly DependencyProperty IconBackgroundProperty = DependencyProperty.Register(nameof(IconBackground), typeof(Brush),
                typeof(AcObjectBase));

        public Brush IconBackground {
            get => (Brush)GetValue(IconBackgroundProperty);
            set => SetValue(IconBackgroundProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string),
                typeof(AcObjectBase));

        public string Icon {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(nameof(IconSource), typeof(ImageSource),
                typeof(AcObjectBase));

        public ImageSource IconSource {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object),
                typeof(AcObjectBase));

        public object Content {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ToolBarsProperty = DependencyProperty.Register(nameof(ToolBars), typeof(Collection<ToolBar>),
                typeof(AcObjectBase));

        public Collection<ToolBar> ToolBars {
            get => (Collection<ToolBar>)GetValue(ToolBarsProperty);
            set => SetValue(ToolBarsProperty, value);
        }

        public static readonly DependencyProperty ScrollableProperty = DependencyProperty.Register(nameof(Scrollable), typeof(bool),
                typeof(AcObjectBase), new PropertyMetadata(true));

        public bool Scrollable {
            get => GetValue(ScrollableProperty) as bool? ?? default(bool);
            set => SetValue(ScrollableProperty, value);
        }

        public static readonly DependencyProperty HeaderPaddingProperty = DependencyProperty.Register(nameof(HeaderPadding), typeof(Thickness),
                typeof(AcObjectBase));

        public Thickness HeaderPadding {
            get => GetValue(HeaderPaddingProperty) as Thickness? ?? default(Thickness);
            set => SetValue(HeaderPaddingProperty, value);
        }
    }
}
