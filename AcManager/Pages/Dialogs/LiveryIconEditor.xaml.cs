using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;

namespace AcManager.Pages.Dialogs {
    public partial class LiveryIconEditor : INotifyPropertyChanged {
        public CarSkinObject Skin { get; set; }

        public SettingEntry[] Styles { get; } = {
            new SettingEntry("Flat", "Flat"),
            new SettingEntry("FlatDiagonal", "Two colors (diagonal)"),
            new SettingEntry("Metallic", "Metallic"),
        };

        private SettingEntry _selectedStyle;

        public SettingEntry SelectedStyle {
            get { return _selectedStyle; }
            set {
                if (Equals(value, _selectedStyle)) return;
                _selectedStyle = value;
                OnPropertyChanged();

                try {
                    StyleObject = (FrameworkElement)Application.LoadComponent(new Uri($"/Assets/LiveryStyles/{value.Id}.xaml", UriKind.Relative));
                    OnPropertyChanged(nameof(StyleObject));

                    StyleObject.DataContext = Model;
                    StyleObject.Width = 64;
                    StyleObject.Height = 64;

                    Canvas.Children.Clear();
                    Canvas.Children.Add(StyleObject);
                } catch (Exception e) {
                    Logging.Warning("[LiveryIconEditor] Can’t change style: " + e);
                }
            }
        }

        public FrameworkElement StyleObject { get; private set; }

        public LiveryIconEditor(CarSkinObject skin) {
            Skin = skin;

            DataContext = this;
            InitializeComponent();
            
            Buttons = new[] { OkButton, CancelButton };
            SelectedStyle = Styles.FirstOrDefault();

            try {
                var bitmap = Image.FromFile(skin.PreviewImage);
                var colors = ImageUtils.GetBaseColors((Bitmap)bitmap);
                Model.ColorValue = colors.Select(x => (System.Drawing.Color?)x).FirstOrDefault()?.ToColor() ?? Color.FromRgb(255, 255, 255);
                Model.SecondaryColorValue = colors.Select(x => (System.Drawing.Color?)x).ElementAtOrDefault(1)?.ToColor() ?? Color.FromRgb(0, 0, 0);
            } catch (Exception e) {
                Logging.Warning("[LiveryIconEditor] Can’t find base colors: " + e);
                Model.ColorValue = Color.FromRgb(255, 255, 255);
                Model.SecondaryColorValue = Color.FromRgb(0, 0, 0);
            }
        }

        public StyleViewModel Model { get; } = new StyleViewModel();

        public class StyleViewModel : NotifyPropertyChanged {
            private Color _colorValue;

            public Color ColorValue {
                get { return _colorValue; }
                set {
                    if (Equals(value, _colorValue)) return;
                    _colorValue = value;
                    OnPropertyChanged();

                    Color = new SolidColorBrush(value);
                    OnPropertyChanged(nameof(Color));
                }
            }

            public SolidColorBrush Color { get; private set; }

            private Color _secondaryColorValue;

            public Color SecondaryColorValue {
                get { return _secondaryColorValue; }
                set {
                    if (Equals(value, _secondaryColorValue)) return;
                    _secondaryColorValue = value;
                    OnPropertyChanged();

                    SecondaryColor = new SolidColorBrush(value);
                    OnPropertyChanged(nameof(SecondaryColor));
                }
            }

            public SolidColorBrush SecondaryColor { get; private set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
