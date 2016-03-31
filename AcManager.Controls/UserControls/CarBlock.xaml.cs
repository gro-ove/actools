using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Controls.UserControls {
    [ContentProperty("PreviewContent")]
    public partial class CarBlock {
        public CarBlock() {
            InitializeComponent();
            InnerCarBlockPanel.DataContext = this;
        }

        public static readonly DependencyProperty ShowSkinsAndPreviewProperty = DependencyProperty.Register(nameof(ShowSkinsAndPreview), typeof(bool),
                typeof(CarBlock), new PropertyMetadata(true));

        public bool ShowSkinsAndPreview {
            get { return (bool)GetValue(ShowSkinsAndPreviewProperty); }
            set { SetValue(ShowSkinsAndPreviewProperty, value); }
        }

        public static readonly DependencyProperty SelectSkinProperty = DependencyProperty.Register(nameof(SelectSkin), typeof(bool),
                typeof(CarBlock));

        public bool SelectSkin {
            get { return (bool)GetValue(SelectSkinProperty); }
            set { SetValue(SelectSkinProperty, value); }
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(CarBlock));

        public CarObject Car {
            get { return (CarObject)GetValue(CarProperty); }
            set { SetValue(CarProperty, value); }
        }

        public static readonly DependencyProperty SelectedSkinProperty = DependencyProperty.Register(nameof(SelectedSkin), typeof(CarSkinObject),
                typeof(CarBlock));

        public CarSkinObject SelectedSkin {
            get { return (CarSkinObject)GetValue(SelectedSkinProperty); }
            set { SetValue(SelectedSkinProperty, value); }
        }

        public static readonly DependencyProperty PreviewContentProperty = DependencyProperty.Register(nameof(PreviewContent), typeof(object),
                typeof(CarBlock));

        public object PreviewContent {
            get { return (object)GetValue(PreviewContentProperty); }
            set { SetValue(PreviewContentProperty, value); }
        }

        private void PreviewImage_OnMouseDown(object sender, MouseButtonEventArgs e) {
            var selected = new ImageViewer(
                Car.Skins.Select(x => x.PreviewImage),
                Car.Skins.IndexOf(SelectedSkin)
            ).ShowDialogInSelectMode();
            SelectedSkin = Car.Skins.ElementAtOrDefault(selected ?? -1) ?? SelectedSkin;
        }
    }
}
