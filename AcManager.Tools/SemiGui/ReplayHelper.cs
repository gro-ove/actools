using System;
using System.IO;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class ReplayHelper : NotifyPropertyChanged {
        public bool IsReplayAvailable { get; }

        public readonly string OriginalReplayFilename;

        [CanBeNull]
        public readonly string RenamedReplayFilename;

        internal ReplayHelper(Game.StartProperties startProperties, Game.Result result) {
            OriginalReplayFilename = Path.Combine(FileUtils.GetReplaysDirectory(), ReplayObject.PreviousReplayName);

            var replayName = GetReplayName(startProperties, result);
            RenamedReplayFilename = replayName == null ? null : FileUtils.EnsureUnique(Path.Combine(FileUtils.GetReplaysDirectory(), replayName));

            IsReplayAvailable = replayName != null && File.Exists(OriginalReplayFilename);
            if (IsReplayAvailable && SettingsHolder.Drive.AutoSaveReplays) {
                IsReplayRenamed = true;
            }
        }

        private bool _isReplayRenamed;

        public bool IsReplayRenamed {
            get { return _isReplayRenamed; }
            set {
                if (!IsReplayAvailable || RenamedReplayFilename == null || Equals(_isReplayRenamed, value)) return;

                try {
                    if (value) {
                        File.Move(OriginalReplayFilename, RenamedReplayFilename);
                    } else {
                        File.Move(RenamedReplayFilename, OriginalReplayFilename);
                    }

                    _isReplayRenamed = value;
                    OnPropertyChanged();
                } catch (Exception e) {
                    NonfatalError.Notify(value ? ToolsStrings.ReplayHelper_CannotSaveReplay : ToolsStrings.ReplayHelper_CannotUnsaveReplay, e);
                }
            }
        }

        [CanBeNull]
        private static string GetReplayName(Game.StartProperties startProperties, Game.Result result) {
            if (startProperties == null || result == null) return null;

            var s = SettingsHolder.Drive.ReplaysNameFormat;
            if (string.IsNullOrEmpty(s)) {
                s = SettingsHolder.Drive.DefaultReplaysNameFormat;
            }
            
            return FileUtils.EnsureFileNameIsValid(VariablesReplacement.Process(s, startProperties, result));
        }
    }
}