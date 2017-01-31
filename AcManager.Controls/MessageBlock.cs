using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls {
    [ContentProperty(nameof(Children))]
    public class MessageBlock : ContentControl {
        static MessageBlock() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MessageBlock), new FrameworkPropertyMetadata(typeof(MessageBlock)));
        }

        public MessageBlock() {
            Children = new UIElementCollection(this, this);
        }

        public static readonly DependencyProperty ChildrenProperty = DependencyProperty.Register(nameof(Children), typeof(UIElementCollection),
                typeof(MessageBlock));

        public UIElementCollection Children {
            get { return (UIElementCollection)GetValue(ChildrenProperty); }
            set { SetValue(ChildrenProperty, value); }
        }

        private Panel _panel;
        private Button _closeButton;

        public override void OnApplyTemplate() {
            if (_closeButton != null) {
                _closeButton.Click -= OnCloseButtonClick;
            }

            base.OnApplyTemplate();

            _panel = GetTemplateChild(@"PART_Panel") as Panel;
            if (_panel != null) {
                var newChildren = _panel.Children;
                if (!ReferenceEquals(Children, newChildren)) {
                    var children = Children.OfType<UIElement>().ToList();
                    Children.Clear();

                    foreach (var child in children) {
                        newChildren.Add(child);
                    }

                    Children = newChildren;
                }
            }

            _closeButton = GetTemplateChild(@"PART_CloseButton") as Button;
            if (_closeButton != null) {
                _closeButton.Click += OnCloseButtonClick;
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e) {
            if (CloseKey != null) {
                ValuesStorage.Set("msgblock:" + CloseKey, true);
                SetClosed(true);
            }
        }

        public new static readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(nameof(Visibility), typeof(Visibility),
                typeof(MessageBlock), new PropertyMetadata(Visibility.Visible, (o, e) => {
                    var b = (MessageBlock)o;
                    b._visibility = (Visibility)e.NewValue;
                    b.UpdateVisibility();
                }));

        private Visibility _visibility = Visibility.Visible;

        public new Visibility Visibility {
            get { return _visibility; }
            set { SetValue(VisibilityProperty, value); }
        }

        public static readonly DependencyProperty CloseKeyProperty = DependencyProperty.Register(nameof(CloseKey), typeof(string),
                typeof(MessageBlock), new PropertyMetadata(null, (o, e) => {
                    var b = (MessageBlock)o;
                    b._closeKey = (string)e.NewValue;
                    b.OnCloseKeyChanged();
                }));

        private string _closeKey;

        [CanBeNull]
        public string CloseKey {
            get { return _closeKey; }
            set { SetValue(CloseKeyProperty, value); }
        }

        private void OnCloseKeyChanged() {
            SetClosed(ValuesStorage.GetBool("msgblock:" + CloseKey));
        }

        private void SetClosed(bool value) {
            SetValue(ClosedPropertyKey, value);
            UpdateVisibility();
        }

        private void UpdateVisibility() {
            base.Visibility = Closed ? Visibility.Collapsed : Visibility;
        }

        public static readonly DependencyPropertyKey ClosedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Closed), typeof(bool),
                typeof(MessageBlock), new PropertyMetadata(false));

        public static readonly DependencyProperty ClosedProperty = ClosedPropertyKey.DependencyProperty;

        public bool Closed => (bool)GetValue(ClosedProperty);

        public static readonly DependencyProperty CloseButtonContentProperty = DependencyProperty.Register(nameof(CloseButtonContent), typeof(object),
                typeof(MessageBlock), new PropertyMetadata(null));

        public object CloseButtonContent {
            get { return GetValue(CloseButtonContentProperty); }
            set { SetValue(CloseButtonContentProperty, value); }
        }

        public static readonly DependencyProperty CloseButtonContentStringFormatProperty = DependencyProperty.Register(nameof(CloseButtonContentStringFormat), typeof(string),
                typeof(MessageBlock));

        public string CloseButtonContentStringFormat {
            get { return (string)GetValue(CloseButtonContentStringFormatProperty); }
            set { SetValue(CloseButtonContentStringFormatProperty, value); }
        }
    }
}