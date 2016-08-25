using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;

namespace AcManager.Controls.UserControls {
    [ContentProperty(nameof(PreviewContent))]
    public partial class CarBlock {
        public CarBlock() {
            InitializeComponent();
            InnerCarBlockPanel.DataContext = this;
        }

        public FrameworkElement BrandArea => InnerBrandArea;
        public FrameworkElement ClassArea => InnerClassArea;
        public FrameworkElement YearArea => InnerYearArea;
        public FrameworkElement CountryArea => InnerCountryArea;

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
            var list = Car.SkinsManager.EnabledOnly.Select(x => x.PreviewImage).ToList();
            var selected = new ImageViewer(list, list.IndexOf(SelectedSkin.PreviewImage))
                    .ShowDialogInSelectMode();
            SelectedSkin = Car.EnabledOnlySkins.ElementAtOrDefault(selected ?? -1) ?? SelectedSkin;
        }

        private void ShowroomButton_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    e.Handled = true;
                    CustomShowroomWrapper.StartAsync(Car, SelectedSkin);
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(Car, SelectedSkin?.Id)) {
                    e.Handled = true;
                    new CarOpenInShowroomDialog(Car, SelectedSkin?.Id).ShowDialog();
                }
            } else if (e.ChangedButton == MouseButton.Right) {
                e.Handled = true;
                var contextMenu = new ContextMenu();

                var item = new MenuItem { Header = ControlsStrings.Car_OpenInShowroom };
                item.Click += (s, args) => CarOpenInShowroomDialog.Run(Car, SelectedSkin?.Id);
                contextMenu.Items.Add(item);

                item = new MenuItem { Header = ControlsStrings.Common_Presets };
                foreach (var menuItem in PresetsMenuHelper.GroupPresets(CarOpenInShowroomDialog.PresetableKeyValue, p => {
                    CarOpenInShowroomDialog.RunPreset(p.Filename, Car, SelectedSkin?.Id);
                })) {
                    item.Items.Add(menuItem);
                }
                contextMenu.Items.Add(item);

                item = new MenuItem { Header = ControlsStrings.Common_Settings, InputGestureText = UiStrings.KeyShift };
                item.Click += (s, args) => new CarOpenInShowroomDialog(Car, SelectedSkin?.Id).ShowDialog();
                contextMenu.Items.Add(item);

                // TODO: Presets!

                item = new MenuItem { Header = ControlsStrings.Car_OpenInCustomShowroom, InputGestureText = UiStrings.KeyAlt };
                item.Click += (s, args) => CustomShowroomWrapper.StartAsync(Car, SelectedSkin);
                contextMenu.Items.Add(item);

                contextMenu.IsOpen = true;
            }
        }
    }
}
