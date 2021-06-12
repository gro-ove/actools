using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    [ContentProperty(nameof(NonNull))]
    public class LazierSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(Lazier),
                typeof(LazierSwitch), new FrameworkPropertyMetadata(null, OnValuePropertyChanged));

        protected static void OnValuePropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is LazierSwitch b)) return;
            (e.OldValue as Lazier)?.UnsubscribeWeak(b.OnValueContentChanged);
            (e.NewValue as Lazier)?.SubscribeWeak(b.OnValueContentChanged);
            (e.NewValue as Lazier)?.StartSetting();
            OnChildDefiningPropertyChanged(sender, e);
        }

        private void OnValueContentChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(Lazier.IsSet) || args.PropertyName == nameof(Lazier.GenericValue)) {
                UpdateActiveChild();
            }
        }

        public Lazier Value {
            get => GetValue(ValueProperty) as Lazier;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty NullProperty = DependencyProperty.Register(nameof(Null), typeof(UIElement),
                typeof(LazierSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement Null {
            get => (UIElement)GetValue(NullProperty);
            set => SetValue(NullProperty, value);
        }

        public static readonly DependencyProperty NonNullProperty = DependencyProperty.Register(nameof(NonNull), typeof(UIElement),
                typeof(LazierSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement NonNull {
            get => (UIElement)GetValue(NonNullProperty);
            set => SetValue(NonNullProperty, value);
        }

        public static readonly DependencyProperty LoadingProperty = DependencyProperty.Register(nameof(Loading), typeof(UIElement),
                typeof(LazierSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement Loading {
            get => (UIElement)GetValue(LoadingProperty);
            set => SetValue(LoadingProperty, value);
        }

        protected override UIElement GetChild() {
            if (Value == null) return NonNull;
            if (Value.IsSet) return Value.GenericValue == null ? Null : NonNull;
            return Loading;
        }
    }
}