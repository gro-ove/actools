using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class FormattedSlider : Slider {
        public FormattedSlider() {
            DefaultStyleKey = typeof(FormattedSlider);
        }

        public static readonly DependencyProperty AutoToolTipFormatProperty = DependencyProperty.Register(nameof(AutoToolTipFormat), typeof(string),
                typeof(FormattedSlider), new PropertyMetadata(null, (o, e) => {
                    ((FormattedSlider)o)._autoToolTipFormat = (string)e.NewValue;
                }));

        private string _autoToolTipFormat;

        [CanBeNull]
        public string AutoToolTipFormat {
            get => _autoToolTipFormat;
            set => SetValue(AutoToolTipFormatProperty, value);
        }

        public static readonly DependencyProperty AutoToolTipConverterProperty = DependencyProperty.Register(nameof(AutoToolTipConverter), typeof(IValueConverter),
                typeof(FormattedSlider), new PropertyMetadata(null, (o, e) => {
                    ((FormattedSlider)o)._autoToolTipConverter = (IValueConverter)e.NewValue;
                }));

        private IValueConverter _autoToolTipConverter;

        [CanBeNull]
        public IValueConverter AutoToolTipConverter {
            get => _autoToolTipConverter;
            set => SetValue(AutoToolTipConverterProperty, value);
        }

        protected override void OnThumbDragStarted(DragStartedEventArgs e) {
            base.OnThumbDragStarted(e);
            FormatAutoToolTipContent();
        }

        protected override void OnThumbDragDelta(DragDeltaEventArgs e) {
            base.OnThumbDragDelta(e);
            FormatAutoToolTipContent();
        }

        private void FormatAutoToolTipContent() {
            if (!string.IsNullOrEmpty(AutoToolTipFormat) || AutoToolTipConverter != null) {
                var autoToolTip = AutoToolTip;
                if (autoToolTip != null) {
                    autoToolTip.Content = string.Format(AutoToolTipFormat ?? @"{0}",
                            (string)AutoToolTipConverter?.Convert(autoToolTip.Content, typeof(string), null, null) ?? autoToolTip.Content);
                }
            }
        }

        private bool _autoToolTipSet;
        private ToolTip _autoToolTip;

        [CanBeNull]
        private ToolTip AutoToolTip {
            get {
                if (!_autoToolTipSet) {
                    _autoToolTipSet = true;

                    try {
                        _autoToolTip = typeof(Slider).GetField("_autoToolTip", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(this) as ToolTip;
                    } catch (Exception e) {
                        Logging.Warning(e.Message);
                    }
                }

                return _autoToolTip;
            }
        }
    }
}