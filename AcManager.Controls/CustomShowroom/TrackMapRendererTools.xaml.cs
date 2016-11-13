using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.CustomShowroom {
    public partial class TrackMapRendererTools {
        private ViewModel Model => (ViewModel)DataContext;

        public TrackMapRendererTools(TrackObjectBase track, TrackMapPreparationRenderer renderer) {
            DataContext = new ViewModel(track, renderer);
            InitializeComponent();
            Buttons = new Button[0];
        }

        public class SurfaceDescription {
            public string Key { get; }

            public bool IsValidTrack { get; }

            public bool IsPitlane { get; }

            public double DirtAdditive { get; }

            public double Friction { get; }

            [CanBeNull]
            public string AudioEffect { get; }

            private SurfaceDescription(IniFileSection section) {
                Key = section.GetNonEmpty("KEY") ?? "";
                IsValidTrack = section.GetBool("IS_VALID_TRACK", false);
                IsPitlane = section.GetBool("IS_PITLANE", false);
                DirtAdditive = section.GetDouble("DIRT_ADDITIVE", 0.0);
                Friction = section.GetDouble("FRICTION", 0.8);
                AudioEffect = section.GetNonEmpty("WAV");
            }

            private string _description;

            public string Description => _description ?? (_description = new[] {
                $"Grip: {Friction * 100d:F1}%",
                IsPitlane ? "pitlane" : null,
                IsValidTrack ? null : "offroad",
                DirtAdditive > 0 ? "dirt" : null,
                AudioEffect == null ? null : "has sound"
            }.NonNull().JoinToString(@"; "));

            public bool ShouldBeVisibleOnMap() {
                return IsValidTrack && Friction > 0.9 && !string.Equals(AudioEffect, @"kerb.wav", StringComparison.OrdinalIgnoreCase);
            }

            public static IEnumerable<SurfaceDescription> Load(string filename) {
                return new IniFile(filename).GetSections("SURFACE")
                                            .Where(x => x.GetNonEmpty("KEY") != null)
                                            .Select(x => new SurfaceDescription(x));
            }

            public static IEnumerable<SurfaceDescription> LoadDefault() {
                var root = AcRootDirectory.Instance.Value;
                if (root == null) return new SurfaceDescription[0];
                return Load(Path.Combine(root, @"system", @"data", @"surfaces.ini"));
            }

            public static IEnumerable<SurfaceDescription> LoadAll(string filename) {
                return Load(filename).Concat(LoadDefault()).Distinct(new DistinctComparer()).OrderBy(x => x.Key);
            }

            private class DistinctComparer : IEqualityComparer<SurfaceDescription> {
                public bool Equals(SurfaceDescription x, SurfaceDescription y) {
                    return x.Key.Equals(y.Key, StringComparison.Ordinal);
                }

                public int GetHashCode(SurfaceDescription obj) {
                    return obj.Key.GetHashCode();
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged, INotifyDataErrorInfo, ITrackMapRendererFilter {
            public TrackObjectBase Track { get; }

            public TrackMapPreparationRenderer Renderer { get; }

            private Regex _regex;

            public ViewModel(TrackObjectBase track, TrackMapPreparationRenderer renderer) {
                Track = track;
                Renderer = renderer;
                Renderer.SetFilter(this);

                Surfaces = SurfaceDescription.LoadAll(Path.Combine(track.DataDirectory, "surfaces.ini")).ToList();
                UpdateFilter(Surfaces.Where(x => x.ShouldBeVisibleOnMap()));

                _useFxaa = Renderer.UseFxaa;
                _margin = Renderer.Margin;
                _scale = Renderer.Scale;
            }

            private bool _nonUserChange;
            private string _userFilter;

            public void UpdateFilter(IEnumerable<SurfaceDescription> ofType) {
                _nonUserChange = true;
                var surfaces = ofType.ToList();
                Filter = surfaces.Any() ? $@"\d+({surfaces.Select(x => Regex.Escape(x.Key)).JoinToString('|')})" : _userFilter ?? @"\d+(ROAD|ASPHALT)";
                _nonUserChange = false;
            }

            public List<SurfaceDescription> Surfaces { get; }

            private string _filterError;

            [CanBeNull]
            public string FilterError {
                set {
                    if (Equals(value, _filterError)) return;
                    _filterError = value;
                    OnErrorsChanged(nameof(Filter));
                }
            }

            private string _filter;

            public string Filter {
                get { return _filter; }
                set {
                    if (Equals(value, _filter)) return;

                    if (!_nonUserChange) {
                        _userFilter = value;
                    }

                    _filter = value;
                    OnPropertyChanged();

                    try {
                        _regex = new Regex(value, RegexOptions.Compiled);
                        FilterError = null;
                    } catch (Exception e) {
                        var s = e.Message.Split(new[] { @" - " }, 2, StringSplitOptions.None);
                        FilterError = s.Length == 2 ? s[1] : e.Message;
                    }

                    Renderer.Update();
                }
            }

            public IEnumerable GetErrors(string propertyName) {
                switch (propertyName) {
                    case nameof(Filter):
                        return string.IsNullOrWhiteSpace(_filterError) ? null : new[] { _filterError };
                    default:
                        return null;
                }
            }

            public bool HasErrors => !string.IsNullOrWhiteSpace(_filterError);
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            bool ITrackMapRendererFilter.Filter(string name) {
                return _regex?.IsMatch(name ?? "") != false;
            }

            private bool _useFxaa;

            public bool UseFxaa {
                get { return _useFxaa; }
                set {
                    if (Equals(value, _useFxaa)) return;
                    _useFxaa = value;
                    OnPropertyChanged();
                    Renderer.UseFxaa = value;
                    Renderer.IsDirty = true;
                }
            }

            private double _margin;

            public double Margin {
                get { return _margin; }
                set {
                    value = value.Clamp(0, 200);
                    if (Equals(value, _margin)) return;
                    _margin = value;
                    OnPropertyChanged();
                    Renderer.Margin = (float)value;
                    Renderer.IsDirty = true;
                }
            }

            private double _scale;

            public double Scale {
                get { return _scale; }
                set {
                    value = value.Clamp(0.00001, 100);
                    if (Equals(value, _scale)) return;
                    _scale = value;
                    OnPropertyChanged();
                    Renderer.Scale = (float)value;
                    Renderer.IsDirty = true;
                }
            }

            private DelegateCommand _cameraToStartCommand;

            public DelegateCommand CameraToStartCommand => _cameraToStartCommand ?? (_cameraToStartCommand = new DelegateCommand(() => {
                Renderer.MoveCameraToStart();
            }));

            private DelegateCommand _resetCameraCommand;

            public DelegateCommand ResetCameraCommand => _resetCameraCommand ?? (_resetCameraCommand = new DelegateCommand(() => {
                ((IKn5ObjectRenderer)Renderer).ResetCamera();
            }));

            private DelegateCommand _saveCommand;

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
                var mapPng = Track.MapImage;
                if (File.Exists(mapPng)) {
                    FileUtils.Recycle(mapPng);
                }

                Renderer.Shot(mapPng);

                var mapIni = Path.Combine(Track.DataDirectory, "map.ini");
                if (File.Exists(mapIni)) {
                    FileUtils.Recycle(mapIni);
                }

                Renderer.SaveInformation(mapIni);
            }));
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            Model.UpdateFilter(SurfacesListBox.SelectedItems.OfType<SurfaceDescription>());
        }
    }
}
