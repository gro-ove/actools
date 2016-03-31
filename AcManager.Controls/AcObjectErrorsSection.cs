using AcManager.Tools.Objects;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls {
    public class AcObjectErrorsSection : Control {
        static AcObjectErrorsSection() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcObjectErrorsSection), new FrameworkPropertyMetadata(typeof(AcObjectErrorsSection)));
        }

        public static readonly DependencyProperty AcObjectProperty = DependencyProperty.Register("AcObject", typeof (AcCommonObject),
                                                                                          typeof (AcObjectErrorsSection));

        public AcCommonObject AcObject {
            get { return (AcCommonObject) GetValue(AcObjectProperty); }
            set { SetValue(AcObjectProperty, value); }
        }
    }
}
