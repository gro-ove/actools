using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public partial class TrackOutlineRendererTools {
        private ViewModel Model => (ViewModel)DataContext;

        public TrackOutlineRendererTools(TrackObjectBase track, TrackOutlineRenderer renderer) {
            DataContext = new ViewModel(track, renderer);
            InitializeComponent();
            Buttons = new Button[0];

            if (Model.Layouts != null) {
                foreach (var layout in Model.Layouts.Where(x => Model.ExtraLayoutIds?.Contains(x.LayoutId) == true)) {
                    LayoutsListBox.SelectedItems.Add(layout);
                }
            }

            LayoutsListBox.SelectionChanged += OnSelectionChanged;
        }

        private class PerLayoutSaveable {
            public string[] ExtraLayoutIds;

            public void Apply(TrackOutlineRenderer renderer, TrackObjectBase track) {
                var value = ExtraLayoutIds;
                renderer.SetActiveMaps(value == null ? new string[0] :
                               track.MainTrackObject.MultiLayouts?.Where(x => value.Contains(x.LayoutId)).Select(x => x.MapImage).ToArray());
            }
        }

        private class PerTrackSaveable {
            public float Rotation, Scale = 0.82f, OffsetX, OffsetY, ExtraWidth = 0.5f;
            public bool UseAiLanes;

            public void Apply(TrackOutlineRenderer renderer) {
                renderer.Rotation = Rotation;
                renderer.Scale = Scale;
                renderer.OffsetX = OffsetX;
                renderer.OffsetY = OffsetY;
                renderer.ExtraWidth = ExtraWidth;
                renderer.UseAiLanes = UseAiLanes;
            }
        }

        private class GlobalSaveable {
            public float ShadowDistance = 1f;
            public float ShadowOpacity = 0.75f, DimmedOpacity = 0.5f, DimmedWidthMultipler = 1f;

            public void Apply(TrackOutlineRenderer renderer) {
                renderer.ShadowOpacity = ShadowOpacity;
                renderer.ShadowDistance = ShadowDistance;
                renderer.DimmedOpacity = DimmedOpacity;
                renderer.DimmedWidthMultipler = DimmedWidthMultipler;
            }
        }

        public static void LoadSettings(TrackObjectBase track, TrackOutlineRenderer renderer) {
            SaveHelper<PerLayoutSaveable>.LoadOrReset(".TrackOutlineRendererTools:l:" + track.IdWithLayout, CacheStorage.Storage).Apply(renderer, track);
            SaveHelper<PerTrackSaveable>.LoadOrReset(".TrackOutlineRendererTools:" + track.Id, CacheStorage.Storage).Apply(renderer);
            SaveHelper<GlobalSaveable>.LoadOrReset(".TrackOutlineRendererTools", ValuesStorage.Storage).Apply(renderer);
        }

        public class ViewModel : NotifyPropertyChanged {
            public TrackObjectBase Track { get; }

            public TrackOutlineRenderer Renderer { get; }

            [CanBeNull]
            public List<TrackObjectBase> Layouts { get; }

            private readonly ISaveHelper _perLayoutSave, _perTrackSave, _globalSaveable;

            public ViewModel(TrackObjectBase track, TrackOutlineRenderer renderer) {
                Track = track;
                Renderer = renderer;

                var layouts = track.MainTrackObject.MultiLayouts?.ApartFrom(track).ToList();
                Layouts = layouts?.Count < 1 ? null : layouts;
                Renderer.SetActiveMaps(new string[0]);

                _perLayoutSave = new SaveHelper<PerLayoutSaveable>(".TrackOutlineRendererTools:l:" + track.IdWithLayout, () => new PerLayoutSaveable {
                    ExtraLayoutIds = ExtraLayoutIds,
                }, Load, storage: CacheStorage.Storage);

                _perTrackSave = new SaveHelper<PerTrackSaveable>(".TrackOutlineRendererTools:" + track.Id, () => new PerTrackSaveable {
                    Rotation = Renderer.Rotation,
                    Scale = Renderer.Scale,
                    OffsetX = Renderer.OffsetX,
                    OffsetY = Renderer.OffsetY,
                    ExtraWidth = Renderer.ExtraWidth,
                    UseAiLanes = Renderer.UseAiLanes,
                }, Load, storage: CacheStorage.Storage);

                _globalSaveable = new SaveHelper<GlobalSaveable>(".TrackOutlineRendererTools", () => new GlobalSaveable {
                    ShadowOpacity = Renderer.ShadowOpacity,
                    ShadowDistance = Renderer.ShadowDistance,
                    DimmedOpacity = Renderer.DimmedOpacity,
                    DimmedWidthMultipler = Renderer.DimmedWidthMultipler,
                }, Load, storage: ValuesStorage.Storage);

                _perLayoutSave.Initialize();
                _perTrackSave.Initialize();
                _globalSaveable.Initialize();

                Renderer.PropertyChanged += OnRendererPropertyChanged;
            }

            private void Load(PerLayoutSaveable o) {
                ExtraLayoutIds = o.ExtraLayoutIds;
            }

            private void Load(PerTrackSaveable o) {
                o.Apply(Renderer);
            }

            private void Load(GlobalSaveable o) {
                o.Apply(Renderer);
            }

            private DelegateCommand _resetStyleCommand;

            public DelegateCommand ResetStyleCommand => _resetStyleCommand ?? (_resetStyleCommand = new DelegateCommand(() => {
                Load(new GlobalSaveable());
            }));

            private string[] _extraLayoutIds;

            [CanBeNull]
            public string[] ExtraLayoutIds {
                get { return _extraLayoutIds; }
                set {
                    if (Equals(value, _extraLayoutIds) || _extraLayoutIds != null && value?.SequenceEqual(_extraLayoutIds) == true) {
                        return;
                    }

                    _extraLayoutIds = value;
                    OnPropertyChanged();

                    Renderer.SetActiveMaps(value == null ? new string[0] :
                            Track.MainTrackObject.MultiLayouts?.Where(x => value.Contains(x.LayoutId)).Select(x => x.MapImage).ToArray());
                    _perLayoutSave.SaveLater();
                }
            }

            private void OnRendererPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.Rotation):
                    case nameof(Renderer.Scale):
                    case nameof(Renderer.OffsetX):
                    case nameof(Renderer.OffsetY):
                    case nameof(Renderer.ExtraWidth):
                    case nameof(Renderer.UseAiLanes):
                        _perTrackSave.SaveLater();
                        break;

                    case nameof(Renderer.ShadowOpacity):
                    case nameof(Renderer.ShadowDistance):
                    case nameof(Renderer.DimmedOpacity):
                    case nameof(Renderer.DimmedWidthMultipler):
                        _globalSaveable.SaveLater();
                        break;
                }
            }

            private DelegateCommand _saveCommand;

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
                try {
                    using (var holder = FileUtils.RecycleOriginal(Track.OutlineImage)) {
                        Renderer.Shot(holder.Filename);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save image", e);
                }
            }));
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            Model.ExtraLayoutIds = LayoutsListBox.SelectedItems.OfType<TrackObjectBase>().Select(x => x.LayoutId).ToArray();
        }
    }
}
