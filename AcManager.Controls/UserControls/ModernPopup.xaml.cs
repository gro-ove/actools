using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace AcManager.Controls.UserControls {
    [ContentProperty(nameof(Content))]
    public partial class ModernPopup {
        public ModernPopup() {
            InitializeComponent();
            Root.DataContext = this;
            CustomPopupPlacementCallback += OnPlacementCallback;    
        }

        private static CustomPopupPlacement[] OnPlacementCallback(Size popupSize, Size targetSize, Point offset) {
            return new[] {
                new CustomPopupPlacement {
                    Point = new Point(0, targetSize.Height)
                }
            };
        }

        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(nameof(Padding), typeof(Thickness),
                typeof(ModernPopup), new PropertyMetadata(new Thickness(4d)));

        public Thickness Padding {
            get { return (Thickness)GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object),
                typeof(ModernPopup));

        public object Content {
            get { return GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }
    }
}
