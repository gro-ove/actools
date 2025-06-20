﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedTrackSkinPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<TrackSkinObject> {
            public TrackObject Track { get; }

            public ViewModel(TrackObject track, [NotNull] TrackSkinObject acObject) : base(acObject) {
                Track = track;
            }

            private AsyncCommand _overrideTexturesCommand;

            public AsyncCommand OverrideTexturesCommand => _overrideTexturesCommand ?? (_overrideTexturesCommand = new AsyncCommand(async () => {
                await TrackSkinTexturesDialog.Run(SelectedObject);
            }));

            /*protected override void FilterExec(string type) {
                switch (type) {
                    case "team":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.Team) ? @"team-" : $"team:{Filter.Encode(SelectedObject.Team)}");
                        break;

                    case "driver":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.DriverName) ? @"driver-" : $"driver:{Filter.Encode(SelectedObject.DriverName)}");
                        break;

                    case "number":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.SkinNumber) ? @"number-" : $"number:{Filter.Encode(SelectedObject.SkinNumber)}");
                        break;

                    case "priority":
                        NewFilterTab(SelectedObject.Priority.HasValue ? $"priority:{SelectedObject.Priority.Value}" : @"priority-");
                        break;
                }

                base.FilterExec(type);
            }*/

            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ??
                    (_updatePreviewCommand = new AsyncCommand(() => TrackPreviewsCreator.ShotAndApply(SelectedObject),
                            () => SelectedObject.Enabled));

            private AsyncCommand _updatePreviewDirectCommand;

            public AsyncCommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ??
                    (_updatePreviewDirectCommand = new AsyncCommand(() => TrackPreviewsCreator.ApplyExisting(SelectedObject)));

            #region Presets
            public HierarchicalItemsView QuickDrivePresets {
                get => _quickDrivePresets;
                set => Apply(value, ref _quickDrivePresets);
            }

            private HierarchicalItemsView _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue), p => {
                        QuickDrive.RunAsync(trackSkin: SelectedObject, presetFilename: p.VirtualFilename).Ignore();
                    });
                }
            }
            #endregion

            private AsyncCommand _driveCommand;

            public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !await QuickDrive.RunAsync(trackSkin: SelectedObject)) {
                    DriveOptionsCommand.Execute();
                }
            }, () => SelectedObject.Enabled));

            private DelegateCommand _driveOptionsCommand;

            public DelegateCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(trackSkin: SelectedObject);
            }, () => SelectedObject.Enabled));
        }

        private string _trackId, _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _trackId = uri.GetQueryParam("TrackId");
            if (_trackId == null) throw new ArgumentException("Track ID is missing");

            _id = uri.GetQueryParam("Id");
            if (_id == null) throw new ArgumentException(ToolsStrings.Common_IdIsMissing);
        }

        private TrackObject _trackObject;
        private TrackSkinObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            do {
                _trackObject = await TracksManager.Instance.GetByIdAsync(_trackId);
                if (_trackObject == null) {
                    _object = null;
                    return;
                }

                _object = await _trackObject.SkinsManager.GetByIdAsync(_id);
            } while (_trackObject.Outdated);
        }

        void ILoadableContent.Load() {
            do {
                _trackObject = TracksManager.Instance.GetById(_trackId);
                if (_trackObject == null) {
                    _object = null;
                    return;
                }

                _object = _trackObject?.SkinsManager.GetById(_id);
            } while (_trackObject?.Outdated == true);
        }

        void ILoadableContent.Initialize() {
            if (_trackObject == null) throw new ArgumentException("Can’t find track with provided ID");
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            InitializeAcObjectPage(_model = new ViewModel(_trackObject, _object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewDirectCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift))
            });
            InitializeComponent();
        }

        private ViewModel _model;

        #region Presets (Dynamic Loading)
        private void OnDriveButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }
        #endregion
    }
}
