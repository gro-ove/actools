using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.Objects;
using AcTools.Kn5Render.Utils;

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

        public static readonly DependencyProperty OpenShowroomProperty = DependencyProperty.Register(nameof(OpenShowroom), typeof(bool),
                typeof(CarBlock));

        public bool OpenShowroom {
            get { return (bool)GetValue(OpenShowroomProperty); }
            set { SetValue(OpenShowroomProperty, value); }
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
            get { return GetValue(PreviewContentProperty); }
            set { SetValue(PreviewContentProperty, value); }
        }

        private void PreviewImage_OnMouseDown(object sender, MouseButtonEventArgs e) {
            var list = Car.SkinsManager.LoadedOnly.Select(x => x.PreviewImage).ToList();
            var selected = new ImageViewer(list, list.IndexOf(SelectedSkin.PreviewImage))
                    .ShowDialogInSelectMode();
            SelectedSkin = Car.Skins.ElementAtOrDefault(selected ?? -1) ?? SelectedSkin;
        }

        private void ShowroomButton_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    Kn5RenderWrapper.StartBrightRoomPreview(Car.Location, SelectedSkin?.Id);
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(Car, SelectedSkin?.Id)) {
                    new CarOpenInShowroomDialog(Car, SelectedSkin?.Id).ShowDialog();
                }
            } else if (e.ChangedButton == MouseButton.Right) {
                var contextMenu = new ContextMenu();

                var item = new MenuItem { Header = "Open In Showroom" };
                item.Click += (s, args) => CarOpenInShowroomDialog.Run(Car, SelectedSkin?.Id);
                contextMenu.Items.Add(item);

                item = new MenuItem { Header = "Settings", InputGestureText = "Shift" };
                item.Click += (s, args) => new CarOpenInShowroomDialog(Car, SelectedSkin?.Id).ShowDialog();
                contextMenu.Items.Add(item);

                // TODO: Presets!

                item = new MenuItem { Header = "Open In Custom Showroom", InputGestureText = "Alt" };
                item.Click += (s, args) => Kn5RenderWrapper.StartBrightRoomPreview(Car.Location, SelectedSkin?.Id);
                contextMenu.Items.Add(item);

                contextMenu.IsOpen = true;
            }
        }
    }
}
