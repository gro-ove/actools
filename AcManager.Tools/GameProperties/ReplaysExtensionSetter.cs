using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class ReplaysExtensionSetter : IDisposable {
        private readonly string[] _previous;

        private ReplaysExtensionSetter() {
            try {
                _previous = Directory.GetFiles(AcPaths.GetReplaysDirectory());
            } catch (Exception e) {
                Logging.Error("Can’t get files: " + e);
                _previous = new string[0];
            }
        }

        [CanBeNull]
        public static ReplaysExtensionSetter OnlyNewIfEnabled() {
            return SettingsHolder.Drive.AutoAddReplaysExtension ? new ReplaysExtensionSetter() : null;
        }

        private static IEnumerable<string> GetWithoutExtension() {
            try {
                return Directory.GetFiles(AcPaths.GetReplaysDirectory(), "*", SearchOption.AllDirectories)
                                .Where(file => !file.EndsWith(ReplayObject.ReplayExtension, StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(Path.GetFileName(file), ReplayObject.PreviousReplayName, StringComparison.OrdinalIgnoreCase));
            } catch (Exception e) {
                Logging.Error("Can’t get files without extension: " + e);
                return new string[0];
            }
        }

        public void Dispose() {
            try {
                foreach (var file in GetWithoutExtension().Where(x => !_previous.Contains(x))) {
                    File.Move(file, file + ReplayObject.ReplayExtension);
                }
            } catch (Exception e) {
                Logging.Error("Can’t rename new: " + e);
            }
        }

        public static void RenameAll() {
            try {
                foreach (var file in GetWithoutExtension()) {
                    File.Move(file, file + ReplayObject.ReplayExtension);
                }
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.Replay_CannotRenameReplays, ToolsStrings.Replay_CannotRenameReplays_Commentary, e);
            }
        }

        public static bool HasWithoutExtension() {
            return GetWithoutExtension().Any();
        }

        public static bool HasWithExtension() {
            try {
                return Directory.GetFiles(AcPaths.GetReplaysDirectory()).Any(file => file.EndsWith(ReplayObject.ReplayExtension));
            } catch (Exception e) {
                Logging.Error("Can’t get files with extension: " + e);
                return false;
            }
        }
    }
}