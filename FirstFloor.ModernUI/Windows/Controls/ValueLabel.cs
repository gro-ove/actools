using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(Content))]
    public class ValueLabel : Control {
        static ValueLabel() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ValueLabel), new FrameworkPropertyMetadata(typeof(ValueLabel)));
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(string),
                typeof(ValueLabel));

        public string Content {
            get { return (string)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string),
                typeof(ValueLabel), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Value {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(TextBoxAdvancement.SpecialMode),
                typeof(ValueLabel), new PropertyMetadata(TextBoxAdvancement.SpecialMode.Integer));

        public TextBoxAdvancement.SpecialMode Mode {
            get { return (TextBoxAdvancement.SpecialMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        public static readonly DependencyProperty PostfixProperty = DependencyProperty.Register(nameof(Postfix), typeof(string),
                typeof(ValueLabel));

        public string Postfix {
            get { return (string)GetValue(PostfixProperty); }
            set { SetValue(PostfixProperty, value); }
        }

        public static readonly DependencyProperty ShowZeroAsOffProperty = DependencyProperty.Register(nameof(ShowZeroAsOff), typeof(bool),
                typeof(ValueLabel));

        public bool ShowZeroAsOff {
            get { return (bool)GetValue(ShowZeroAsOffProperty); }
            set { SetValue(ShowZeroAsOffProperty, value); }
        }

        public static readonly DependencyProperty ShowPostfixProperty = DependencyProperty.Register(nameof(ShowPostfix), typeof(bool),
                typeof(ValueLabel), new PropertyMetadata(true));

        public bool ShowPostfix {
            get { return (bool)GetValue(ShowPostfixProperty); }
            set { SetValue(ShowPostfixProperty, value); }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            base.OnMouseLeftButtonUp(e);

            var t = GetTemplateChild("PART_TextBox") as TextBox;
            if (t != null && !t.IsFocused) {
                Keyboard.Focus(t);
            }
        }
    }
}
