using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class ReplayHelper : NotifyPropertyChanged {
        public bool IsAvailable { get; }
        public bool IsRenameable { get; }

        [CanBeNull]
        private readonly string _originalFilename;

        [CanBeNull]
        private string _renamedFilename;

        [NotNull]
        public string Name {
            get => _name;
            set {
                if (Equals(_name, value) || !IsRenameable) return;

                if (SettingsHolder.Drive.AutoAddReplaysExtension && !value.EndsWith(ReplayObject.ReplayExtension,
                        StringComparison.OrdinalIgnoreCase)) {
                    value += ReplayObject.ReplayExtension;
                }

                var renamed = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetReplaysDirectory(), value));

                if (IsKept && _renamedFilename != null) {
                    try {
                        File.Move(_renamedFilename, renamed);
                    } catch (Exception e) {
                        NonfatalError.Notify(ToolsStrings.ReplayHelper_CannotSaveReplay, e);
                        return;
                    }
                }

                _renamedFilename = renamed;
                _name = value.ApartFromLast(ReplayObject.ReplayExtension);

                OnPropertyChanged();
                OnPropertyChanged(nameof(_renamedFilename));

                if (IsKept) {
                    OnPropertyChanged(nameof(Filename));
                }
            }
        }

        public string Filename => IsKept ? _renamedFilename : _originalFilename;

        private static bool DoesReplayFit([CanBeNull] Game.StartProperties startProperties, string fileName) {
            var carId = startProperties?.BasicProperties?.CarId ?? "";
            var trackId = new[]{
                startProperties?.BasicProperties?.TrackId,
                startProperties?.BasicProperties?.TrackConfigurationId
            }.NonNull().JoinToString('_');
            return fileName.Contains(carId + "_" + trackId);
        }

        internal ReplayHelper([CanBeNull] Game.StartProperties startProperties, Game.Result result) {
            var directory = FileUtils.GetReplaysDirectory();

            if (AcSettingsHolder.Replay.Autosave) {
                var autosave = new DirectoryInfo(Path.Combine(directory, ReplayObject.AutosaveCategory));
                if (!autosave.Exists) {
                    _originalFilename = null;
                    _renamedFilename = null;
                    return;
                }

                var file = autosave.GetFiles()
                                   .Where(x => DoesReplayFit(startProperties, x.Name))
                                   .OrderByDescending(x => x.LastWriteTime)
                                   .FirstOrDefault();
                _originalFilename = file?.FullName;
                _renamedFilename = file == null ? null : Path.Combine(directory, file.Name);
                _name = file?.Name;
                IsAvailable = file != null;
            } else {
                IsRenameable = true;

                _originalFilename = Path.Combine(directory, ReplayObject.PreviousReplayName);
                _renamedFilename = FileUtils.EnsureUnique(_originalFilename);
                Name = GetReplayName(startProperties, result);

                IsAvailable = File.Exists(_originalFilename);
                if (IsAvailable && SettingsHolder.Drive.AutoSaveReplays) {
                    IsKept = true;
                }
            }
        }

        private bool _isKept;
        private string _name;

        public bool IsKept {
            get => _isKept;
            set {
                if (!IsAvailable || Equals(_isKept, value) || _originalFilename == null || _renamedFilename == null) {
                    Logging.Warning("Cannot change state");
                    return;
                }

                try {
                    if (value) {
                        File.Move(_originalFilename, _renamedFilename);
                    } else {
                        File.Move(_renamedFilename, _originalFilename);
                    }

                    _isKept = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Filename));
                } catch (Exception e) {
                    NonfatalError.Notify(value ? ToolsStrings.ReplayHelper_CannotSaveReplay : ToolsStrings.ReplayHelper_CannotUnsaveReplay, e);
                }
            }
        }

        public void ToggleKept() {
            IsKept = !IsKept;
        }

        public Task Play() {
            return GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                Filename = Filename
            }));
        }

        [NotNull]
        private static string GetReplayName([CanBeNull] Game.StartProperties startProperties, [CanBeNull] Game.Result result) {
            if (startProperties == null) return $"_autosave_{DateTime.Now.ToMillisecondsTimestamp()}.acreplay";

            var s = SettingsHolder.Drive.ReplaysNameFormat;
            if (string.IsNullOrEmpty(s)) {
                s = SettingsHolder.Drive.DefaultReplaysNameFormat;
            }

            return FileUtils.EnsureFileNameIsValid(VariablesReplacement.Process(s, startProperties, result));
        }
    }
}