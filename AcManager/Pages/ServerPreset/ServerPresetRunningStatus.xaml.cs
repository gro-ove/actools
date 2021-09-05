using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetRunningStatus {
        public ServerPresetRunningStatus() {
            InitializeComponent();

            CompositionTargetEx.Rendering += OnRendering;
            this.OnActualUnload(() => CompositionTargetEx.Rendering -= OnRendering);
        }

        private static TimeSpan _last = TimeSpan.Zero;

        private void OnRendering(object sender, EventArgs e) {
            var args = (RenderingEventArgs)e;

            if (args.RenderingTime == _last) return;
            _last = args.RenderingTime;

            UpdateMap();
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
                RenderTransform = new TranslateTransform(),
                ToolTip = FindResource(@"DriverToolTip"),
                ContextMenu = FindResource(@"DriverContextMenu") as ContextMenu
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

            var mapWidth = TrackMap.ActualWidth;
            var mapHeight = TrackMap.ActualHeight;
            foreach (var item in leaderboard.ConnectedOnly) {
                var driver = item.Driver;
                var location = item.Location;
                if (driver == null || location.PositionX == 0f && location.PositionZ == 0f) continue;

                var image = MapCreateItem(driver.CarSkin?.LiveryImage);
                if (image.Filename != driver.CarSkin?.LiveryImage) {
                    image.Filename = driver.CarSkin?.LiveryImage;
                    image.DataContext = item;
                }
                if (image.Visibility != Visibility.Visible) {
                    image.Visibility = Visibility.Visible;
                }
                var transform = (TranslateTransform)image.RenderTransform;
                var newX = _mapParams.GetRelativeX(location.PositionX) * mapWidth;
                var newY = _mapParams.GetRelativeY(location.PositionZ) * mapHeight;
                if ((newX - transform.X).Abs() > 10 || (newY - transform.Y).Abs() > 10) {
                    transform.X = newX;
                    transform.Y = newY;
                } else {
                    transform.X += (newX - transform.X) * 0.2;
                    transform.Y += (newY - transform.Y) * 0.2;
                }
            }

            foreach (var image in _mapItemPool) {
                image.Visibility = Visibility.Collapsed;
            }
        }

        private void OnChatTextKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                Model?.CmPlugin?.Chat.SendChatCommand.ExecuteAsync();
            }
        }

        private void OnChatTextKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
            }
        }
    }
}