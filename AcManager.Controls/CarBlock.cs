using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Graphs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public interface ICustomShowroomWrapper {
        // Task StartAsync(string kn5, string skinId = null, string presetFilename = null);

        [NotNull]
        Task StartAsync([CanBeNull] CarObject car, CarSkinObject skin = null, string presetFilename = null);

        [NotNull]
        string PresetableKeyValue { get; }
    }

    public interface ICarSetupsView {
        void Open(CarObject car, CarSetupsRemoteSource forceRemoteSource = CarSetupsRemoteSource.None, bool forceNewWindow = false);
    }

    [ContentProperty(nameof(PreviewContent))]
    public class CarBlock : Control {
        [CanBeNull]
        public static ICustomShowroomWrapper CustomShowroomWrapper { get; set; }

        [CanBeNull]
        public static ICarSetupsView CarSetupsView { get; set; }

        static CarBlock() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CarBlock), new FrameworkPropertyMetadata(typeof(CarBlock)));
        }

        [CanBeNull]
        public TagsList TagsList { get; private set; }

        [CanBeNull]
        public FrameworkElement BrandArea { get; private set; }

        [CanBeNull]
        public FrameworkElement ClassArea { get; private set; }

        [CanBeNull]
        public FrameworkElement YearArea { get; private set; }

        [CanBeNull]
        public FrameworkElement CountryArea { get; private set; }

        [CanBeNull]
        public Border Footer { get; private set; }

        private FrameworkElement _previewImage;
        private Button _showroomButton, _tsmSetupsButton;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            // Event handlers
            if (_previewImage != null) {
                _previewImage.MouseUp -= OnPreviewImageClick;
            }

            if (_showroomButton != null) {
                _showroomButton.Click -= OnShowroomButtonClick;
                _showroomButton.PreviewMouseRightButtonDown -= OnShowroomContextMenu;
            }

            if (_tsmSetupsButton != null) {
                _tsmSetupsButton.Click -= OnTsmSetupsButtonClick;
            }

            _previewImage = GetTemplateChild("PART_PreviewImage") as FrameworkElement;
            _showroomButton = GetTemplateChild("PART_ShowroomButton") as Button;
            _tsmSetupsButton = GetTemplateChild("PART_TsmSetupsButton") as Button;

            if (_previewImage != null) {
                _previewImage.MouseUp += OnPreviewImageClick;
            }

            if (_showroomButton != null) {
                _showroomButton.Click += OnShowroomButtonClick;
                _showroomButton.PreviewMouseRightButtonDown += OnShowroomContextMenu;
            }

            if (_tsmSetupsButton != null) {
                _tsmSetupsButton.Click += OnTsmSetupsButtonClick;
            }

            // Various areas and footer
            TagsList = GetTemplateChild("PART_TagsList") as TagsList;
            BrandArea = GetTemplateChild("PART_BrandArea") as FrameworkElement;
            ClassArea = GetTemplateChild("PART_ClassArea") as FrameworkElement;
            YearArea = GetTemplateChild("PART_YearArea") as FrameworkElement;
            CountryArea = GetTemplateChild("PART_CountryArea") as FrameworkElement;
            Footer = GetTemplateChild("PART_Footer") as Border;

            UpdateDescription();
        }

        public static readonly DependencyProperty ShowSkinsAndPreviewProperty = DependencyProperty.Register(nameof(ShowSkinsAndPreview), typeof(bool),
                typeof(CarBlock), new PropertyMetadata(true));

        public bool ShowSkinsAndPreview {
            get => GetValue(ShowSkinsAndPreviewProperty) as bool? == true;
            set => SetValue(ShowSkinsAndPreviewProperty, value);
        }

        public static readonly DependencyProperty ShowDescriptionProperty = DependencyProperty.Register(nameof(ShowDescription), typeof(bool),
                typeof(CarBlock), new PropertyMetadata(true, (o, e) => { ((CarBlock)o)._showDescription = (bool)e.NewValue; }));

        private bool _showDescription = true;

        public bool ShowDescription {
            get => _showDescription;
            set => SetValue(ShowDescriptionProperty, value);
        }

        public static readonly DependencyProperty SelectSkinProperty = DependencyProperty.Register(nameof(SelectSkin), typeof(bool),
                typeof(CarBlock));

        public bool SelectSkin {
            get => GetValue(SelectSkinProperty) as bool? == true;
            set => SetValue(SelectSkinProperty, value);
        }

        public static readonly DependencyProperty OpenShowroomProperty = DependencyProperty.Register(nameof(OpenShowroom), typeof(bool),
                typeof(CarBlock));

        public bool OpenShowroom {
            get => GetValue(OpenShowroomProperty) as bool? == true;
            set => SetValue(OpenShowroomProperty, value);
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(CarBlock), new PropertyMetadata(OnCarChanged));

        public CarObject Car {
            get => (CarObject)GetValue(CarProperty);
            set => SetValue(CarProperty, value);
        }

        private static void OnCarChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CarBlock)o).OnCarChanged((CarObject)e.NewValue);
        }

        private void OnCarChanged(CarObject newValue) {
            try {
                UpdateDescription();
                newValue?.SubscribeWeak((s, e) => {
                    if (e.PropertyName == nameof(CarObject.Description) || e.PropertyName == nameof(CarObject.SpecsTorqueCurve)
                            || e.PropertyName == nameof(CarObject.SpecsPowerCurve)) {
                        UpdateDescription();
                    }
                });
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void UpdateDescription() {
            var footer = Footer;
            if (footer == null) return;

            var car = Car;
            if (car == null) {
                footer.Child = null;
                return;
            }

            if (ShowDescription) {
                var textBox = new RichTextBox {
                    Margin = new Thickness(0, 8, 0, 0),
                    Style = (Style)FindResource(@"RichTextBox.Small.ReadOnly"),
                    IsReadOnly = true,
                    IsInactiveSelectionHighlightEnabled = false,
                    AutoWordSelection = true,
                    Focusable = false,
                    Cursor = Cursors.Arrow,
                    IsDocumentEnabled = true
                };

                var carDescription = car.Description?.Trim();
                textBox.Document.Blocks.Clear();

                var item = new Paragraph();

                if (SettingsHolder.Content.CurversInDrive) {
                    item.Inlines.Add(new Floater {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Blocks = {
                            new Paragraph(new InlineUIContainer {
                                Child = new CarGraphViewer {
                                    Car = car,
                                    Margin = new Thickness(8, 0, 0, 0),
                                    Padding = new Thickness(0, 0, 0, 0),
                                    Width = 240,
                                    Height = 160,
                                    Style = (Style)FindResource(typeof(CarGraphViewer)),
                                }
                            })
                        }
                    });
                }

                item.Inlines.Add(string.IsNullOrEmpty(carDescription) ?
                        PlaceholderTextBlock.GetPlaceholder(textBox, "Description is missing.") :
                        new Run(carDescription));

                textBox.Document.Blocks.Add(item);

                footer.Child = textBox;
            } else if (SettingsHolder.Content.CurversInDrive) {
                footer.Child = new CarGraphViewer {
                    Car = car,
                    Margin = new Thickness(3, 8, 3, 0),
                    Padding = new Thickness(0, 0, 0, 0),
                    Height = 160
                };
            } else {
                footer.Child = null;
            }
        }

        public static readonly DependencyProperty SelectedSkinProperty = DependencyProperty.Register(nameof(SelectedSkin), typeof(CarSkinObject),
                typeof(CarBlock));

        public CarSkinObject SelectedSkin {
            get => (CarSkinObject)GetValue(SelectedSkinProperty);
            set => SetValue(SelectedSkinProperty, value);
        }

        public static readonly DependencyProperty PreviewContentProperty = DependencyProperty.Register(nameof(PreviewContent), typeof(object),
                typeof(CarBlock));

        public object PreviewContent {
            get => GetValue(PreviewContentProperty);
            set => SetValue(PreviewContentProperty, value);
        }

        public static Task<object> ImageViewerImageCallback(CarSkinObject x) {
            return Task.FromResult((object)x.PreviewImage);
        }

        public static object ImageViewerDetailsCallback(CarSkinObject skin) {
            if (skin == null) return null;
            ImageViewerSkinDetails.Value?.SetValue(DataContextProperty, skin);
            return ImageViewerSkinDetails.Value;
        }

        private static readonly Lazier<DockPanel> ImageViewerSkinDetails = Lazier.Create(() => {
            var livery = new BetterImage {
                Width = 48,
                Height = 48,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            livery.SetBinding(BetterImage.FilenameProperty, new Binding(nameof(CarSkinObject.LiveryImage)));
            return new DockPanel {
                Children = {
                    livery,
                    new ContentControl {
                        Content = ToolTips.GetCarSkinToolTip()?.Content as FrameworkElement
                    }
                }
            };
        });

        private void OnPreviewImageClick(object sender, MouseButtonEventArgs e) {
            var selected = new ImageViewer<CarSkinObject>(Car.SkinsManager.Enabled, Car.SkinsManager.Enabled.IndexOf(SelectedSkin),
                    ImageViewerImageCallback, ImageViewerDetailsCallback).SelectDialog();
            if (selected != null) {
                SelectedSkin = selected;
            }
        }

        public static void OnShowroomButtonClick(CarObject car, CarSkinObject skin = null) {
            var custom = !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
                    Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ^ SettingsHolder.CustomShowroom.CustomShowroomInstead;
            if (custom) {
                CustomShowroomWrapper?.StartAsync(car, skin);
            } else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                    !CarOpenInShowroomDialog.Run(car, skin?.Id)) {
                new CarOpenInShowroomDialog(car, skin?.Id).ShowDialog();
            }
        }

        private static void ShowroomMenu(ContextMenu contextMenu, CarObject car, CarSkinObject skin) {
            var item = new MenuItem {
                Header = ControlsStrings.Car_OpenInShowroom,
                InputGestureText = SettingsHolder.CustomShowroom.CustomShowroomInstead ? UiStrings.KeyAlt : null
            };
            item.Click += (s, args) => CarOpenInShowroomDialog.Run(car, skin?.Id);
            contextMenu.Items.Add(item);

            // presets
            item = new MenuItem { Header = "Showroom presets" };
            foreach (var menuItem in PresetsMenuHelper.GroupPresets(new PresetsCategory(CarOpenInShowroomDialog.PresetableKeyValue),
                    p => CarOpenInShowroomDialog.RunPreset(p.VirtualFilename, car, skin?.Id))) {
                item.Items.Add(menuItem);
            }
            contextMenu.Items.Add(item);

            // settings
            item = new MenuItem { Header = ControlsStrings.Common_Settings, InputGestureText = UiStrings.KeyShift };
            item.Click += (s, args) => new CarOpenInShowroomDialog(car, skin?.Id).ShowDialog();
            contextMenu.Items.Add(item);
        }

        private static void ShowroomMenuCustom(ContextMenu contextMenu, CarObject car, CarSkinObject skin) {
            var item = new MenuItem {
                Header = ControlsStrings.Car_OpenInCustomShowroom,
                InputGestureText = SettingsHolder.CustomShowroom.CustomShowroomInstead ? null : UiStrings.KeyAlt,
                IsEnabled = CustomShowroomWrapper != null
            };

            item.Click += (s, args) => CustomShowroomWrapper?.StartAsync(car, skin);
            contextMenu.Items.Add(item);

            // presets
            var value = CustomShowroomWrapper?.PresetableKeyValue;
            if (value != null) {
                item = new MenuItem { Header = "Custom showroom presets" };
                foreach (var menuItem in PresetsMenuHelper.GroupPresets(new PresetsCategory(value),
                        p => CustomShowroomWrapper.StartAsync(car, skin, p.VirtualFilename))) {
                    item.Items.Add(menuItem);
                }
                contextMenu.Items.Add(item);
            }
        }

        public static void OnShowroomContextMenu(ContextMenu contextMenu, CarObject car, CarSkinObject skin = null) {
            ShowroomMenu(contextMenu, car, skin);
            contextMenu.Items.Add(new Separator());
            ShowroomMenuCustom(contextMenu, car, skin);
        }

        public static void OnShowroomContextMenu(CarObject car, CarSkinObject skin = null) {
            var contextMenu = new ContextMenu();
            OnShowroomContextMenu(contextMenu, car, skin);
            contextMenu.IsOpen = true;
        }

        private void OnShowroomContextMenu(object sender, RoutedEventArgs e) {
            e.Handled = true;
            OnShowroomContextMenu(Car, SelectedSkin);
        }

        private void OnShowroomButtonClick(object sender, EventArgs e) {
            OnShowroomButtonClick(Car, SelectedSkin);
        }

        private void OnTsmSetupsButtonClick(object sender, RoutedEventArgs e) {
            CarSetupsView?.Open(Car, CarSetupsRemoteSource.TheSetupMarket);
        }
    }
}