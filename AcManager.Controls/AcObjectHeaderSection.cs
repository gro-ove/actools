using AcManager.Tools.Objects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls {
    public class AcObjectHeaderSection : Control {
        static AcObjectHeaderSection() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcObjectHeaderSection), new FrameworkPropertyMetadata(typeof(AcObjectHeaderSection)));
        }

        public static readonly DependencyProperty AcObjectProperty = DependencyProperty.Register(nameof(AcObject), typeof(AcCommonObject),
                                                                                          typeof(AcObjectHeaderSection));

        public AcCommonObject AcObject {
            get { return (AcCommonObject)GetValue(AcObjectProperty); }
            set { SetValue(AcObjectProperty, value); }
        }

        public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(nameof(ShowIcon), typeof (bool),
                                                                                          typeof (AcObjectHeaderSection));

        public bool ShowIcon {
            get { return (bool) GetValue(ShowIconProperty); }
            set { SetValue(ShowIconProperty, value); }
        }

        public static readonly DependencyProperty IconFilenameProperty = DependencyProperty.Register(nameof(IconFilename), typeof(string),
                                                                                          typeof(AcObjectHeaderSection));

        public string IconFilename {
            get { return (string)GetValue(IconFilenameProperty); }
            set { SetValue(IconFilenameProperty, value); }
        }

        private Image _iconImage;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_iconImage != null) {
                _iconImage.MouseDown -= IconImage_MouseDown;
            }

            _iconImage = GetTemplateChild("PART_IconImage") as Image;

            if (_iconImage != null) {
                _iconImage.MouseDown += IconImage_MouseDown;
            }
        }

        private void IconImage_MouseDown(object sender, MouseButtonEventArgs e) {
            IconMouseDown?.Invoke(sender, e);
        }

        public event MouseButtonEventHandler IconMouseDown;
    }
}
