using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsDamageDisplayer {
        private static readonly string[] Images = {
            "engine", "flatspot_fl", "flatspot_fr", "flatspot_rl", "flatspot_rr", "front", "gearbox", "left",
            "rear", "right", "sus_fl", "sus_fr", "sus_rl", "sus_rr", "tyre_fl", "tyre_fr", "tyre_rl", "tyre_rr"
        };

        private readonly string _directory;

        public AcSettingsDamageDisplayer() {
            _directory = Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "texture", "damage");

            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);

            AcSettingsHolder.DamageDisplayer.SubscribeWeak(this, OnDamageDisplayerPropertyChanged);
            UpdatePosition();

            AcSettingsHolder.Video.SubscribeWeak(this, OnVideoPropertyChanged);
            UpdateResolution();

            var watcher = new DirectoryWatcher(_directory, "*.png");
            watcher.Update += OnWatcherUpdate;
            this.OnActualUnload(watcher);
        }

        private void OnVideoPropertyChanged(object sender, PropertyChangedEventArgs e) {
            UpdateResolution();
        }

        private void UpdateResolution() {
            var width = (AcSettingsHolder.Video.Resolution?.Width ?? 1920).Clamp(100, 10000);
            var height = (AcSettingsHolder.Video.Resolution?.Height ?? 1080).Clamp(100, 10000);

            XSlider.Minimum = -512d;
            XSlider.Maximum = width;
            XSlider.TickFrequency = (width + 512d) / 10d;
            YSlider.Maximum = height / 2d;
            YSlider.Minimum = -height / 2d - 512d;
            YSlider.TickFrequency = (height + 512d) / 10d;
            MainCanvas.Width = width;
            MainCanvas.Height = height;
        }

        private readonly Busy _reloadBusy = new Busy(true);
        private void OnWatcherUpdate(object sender, FileSystemEventArgs e) {
            _reloadBusy.DoDelay(UpdateImages, 200);
        }

        private readonly Busy _positionBusy = new Busy();
        private void OnDamageDisplayerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            _positionBusy.Do(() => {
                switch (e.PropertyName) {
                    case nameof(AcSettingsHolder.DamageDisplayer.X):
                        ImagesThumb.SetValue(Canvas.LeftProperty, (double)AcSettingsHolder.DamageDisplayer.X);
                        break;
                    case nameof(AcSettingsHolder.DamageDisplayer.Y):
                        ImagesThumb.SetValue(Canvas.TopProperty, MainCanvas.Height / 2d + AcSettingsHolder.DamageDisplayer.Y);
                        break;
                }
            });
        }

        private void UpdatePosition() {
            _positionBusy.Do(() => {
                ImagesThumb.SetValue(Canvas.LeftProperty, (double)AcSettingsHolder.DamageDisplayer.X);
                ImagesThumb.SetValue(Canvas.TopProperty, MainCanvas.Height / 2d + AcSettingsHolder.DamageDisplayer.Y);
            });
        }

        private bool _loaded;
        private Panel _imagesWrapper;

        private void OnThumbLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            _imagesWrapper = (Panel)sender;
            UpdateImages();
        }

        private void UpdateImages() {
            if (_imagesWrapper == null) return;
            _imagesWrapper.Children.Clear();
            foreach (var s in Directory.GetFiles(_directory, "*.png").Where(
                    x => Array.IndexOf(Images, Path.GetFileNameWithoutExtension(x).ToLowerInvariant()) != -1)) {
                _imagesWrapper.Children.Add(new BetterImage {
                    Filename = s
                });
            }
        }

        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e) {
            _positionBusy.Do(() => {
                AcSettingsHolder.DamageDisplayer.X += e.HorizontalChange.RoundToInt();
                AcSettingsHolder.DamageDisplayer.Y += e.VerticalChange.RoundToInt();
            });

            UpdatePosition();
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() {}

            public DamageDisplayerSettings DamageDisplayer => AcSettingsHolder.DamageDisplayer;
        }
    }
}
