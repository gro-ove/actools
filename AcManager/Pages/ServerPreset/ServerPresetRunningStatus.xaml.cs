using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetRunningStatus {
        public ServerPresetRunningStatus() {
            InitializeComponent();

            var plugin = Model?.SelectedObject.CmPlugin;
            if (plugin != null) {
                plugin.Updated += OnPluginUpdated;
                this.OnActualUnload(() => plugin.Updated -= OnPluginUpdated);
            }
        }

        [CanBeNull]
        private SelectedPage.ViewModel Model => DataContext as SelectedPage.ViewModel;

        private class MapParams {
            public TrackObjectBase Track { get; }

            private readonly double _offsetX;
            private readonly double _offsetY;
            private readonly double _configSizeX;
            private readonly double _configSizeY;

            public MapParams(TrackObjectBase track) {
                Track = track;

                var config = new IniFile(Path.Combine(track.DataDirectory, "map.ini"));
                _offsetX = config["PARAMETERS"].GetDouble("X_OFFSET", 0d);
                _offsetY = config["PARAMETERS"].GetDouble("Z_OFFSET", 0d);
                var scaleFactor = config["PARAMETERS"].GetDouble("SCALE_FACTOR", 1d);
                _configSizeX = config["PARAMETERS"].GetDouble("WIDTH", 1e3) * scaleFactor;
                _configSizeY = config["PARAMETERS"].GetDouble("HEIGHT", 1e3) * scaleFactor;
            }

            public double GetRelativeX(double worldX) {
                return (worldX + _offsetX) / _configSizeX;
            }

            public double GetRelativeY(double worldZ) {
                return (worldZ + _offsetY) / _configSizeY;
            }
        }

        private MapParams _mapParams;
        private readonly Busy _mapBusy = new Busy();

        private void OnTrackMapSizeChanged(object sender, SizeChangedEventArgs e) {
            _mapBusy.Yield(UpdateMap);
        }

        private readonly List<BetterImage> _mapItemPool = new List<BetterImage>();

        private BetterImage MapCreateItem(string preferredFilename) {
            if (_mapItemPool.Count > 0) {
                var preferred = _mapItemPool.FindIndex(x => x.Filename == preferredFilename);
                if (preferred == -1) {
                    preferred = _mapItemPool.Count - 1;
                }

                var ret = _mapItemPool[preferred];
                _mapItemPool.RemoveAt(preferred);
                return ret;
            }

            const double dotSize = 20d;
            var created = new BetterImage {
                Width = dotSize,
                Height = dotSize,
                Margin = new Thickness(-dotSize / 2, -dotSize / 2, 0, 0),
                Clip = new EllipseGeometry { RadiusX = dotSize / 2, RadiusY = dotSize / 2, Center = new Point(dotSize / 2, dotSize / 2) },
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TranslateTransform()
            };
            TrackMapItems.Children.Add(created);
            return created;
        }

        private void UpdateMap() {
            var track = Model?.Track;
            var plugin = Model?.SelectedObject.CmPlugin;
            var leaderboard = plugin?.Leaderboard;
            if (track == null || leaderboard == null) return;

            if (_mapParams == null || _mapParams.Track != Model.Track) {
                _mapParams = new MapParams(Model.Track);
            }

            foreach (var image in TrackMapItems.Children.OfType<BetterImage>()) {
                _mapItemPool.Add(image);
            }

            foreach (var item in leaderboard.Leaderboard) {
                var driver = item.Driver;
                var location = item.Location;
                if (driver == null || location == null) continue;

                var image = MapCreateItem(driver.CarSkin?.LiveryImage);
                image.Filename = driver.CarSkin?.LiveryImage;
                image.Visibility = Visibility.Visible;
                ((TranslateTransform)image.RenderTransform).X = _mapParams.GetRelativeX(location.PositionX) * TrackMap.ActualWidth;
                ((TranslateTransform)image.RenderTransform).Y = _mapParams.GetRelativeY(location.PositionZ) * TrackMap.ActualHeight;
            }

            foreach (var image in _mapItemPool) {
                image.Visibility = Visibility.Collapsed;
            }
        }

        private void OnPluginUpdated(object sender, EventArgs args) {
            _mapBusy.Yield(UpdateMap);
        }
    }
}