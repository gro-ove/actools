using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
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

        public readonly string OriginalFilename;

        [NotNull]
        public string RenamedFilename { get; private set; }

        [NotNull]
        public string Name {
            get { return _name; }
            set {
                if (Equals(_name, value)) return;

                if (SettingsHolder.Drive.AutoAddReplaysExtension && !value.EndsWith(ReplayObject.ReplayExtension,
                        StringComparison.OrdinalIgnoreCase)) {
                    value += ReplayObject.ReplayExtension;
                }

                var renamed = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetReplaysDirectory(), value));

                if (IsRenamed) {
                    try {
                        File.Move(RenamedFilename, renamed);
                    } catch (Exception e) {
                        NonfatalError.Notify(ToolsStrings.ReplayHelper_CannotSaveReplay, e);
                        return;
                    }
                }

                RenamedFilename = renamed;
                _name = value.ApartFromLast(ReplayObject.ReplayExtension);

                OnPropertyChanged();
                OnPropertyChanged(nameof(RenamedFilename));

                if (IsRenamed) {
                    OnPropertyChanged(nameof(Filename));
                }
            }
        }

        public string Filename => IsRenamed ? RenamedFilename : OriginalFilename;

        internal ReplayHelper(Game.StartProperties startProperties, Game.Result result) {
            OriginalFilename = Path.Combine(FileUtils.GetReplaysDirectory(), ReplayObject.PreviousReplayName);
            RenamedFilename = FileUtils.EnsureUnique(OriginalFilename);
            Name = GetReplayName(startProperties, result);

            IsAvailable = File.Exists(OriginalFilename);
            if (IsAvailable && SettingsHolder.Drive.AutoSaveReplays) {
                IsRenamed = true;
            }
        }

        private bool _isRenamed;
        private string _name;

        public bool IsRenamed {
            get { return _isRenamed; }
            set {
                if (!IsAvailable || Equals(_isRenamed, value)) {
                    Logging.Warning("Cannot change state");
                    return;
                }

                try {
                    if (value) {
                        File.Move(OriginalFilename, RenamedFilename);
                    } else {
                        File.Move(RenamedFilename, OriginalFilename);
                    }

                    _isRenamed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Filename));
                } catch (Exception e) {
                    NonfatalError.Notify(value ? ToolsStrings.ReplayHelper_CannotSaveReplay : ToolsStrings.ReplayHelper_CannotUnsaveReplay, e);
                }
            }
        }

        public void Rename() {
            IsRenamed = !IsRenamed;
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