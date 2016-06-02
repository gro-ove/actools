using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class ReplaysExtensionSetter : IDisposable {
        private readonly string[] _previous;

        private ReplaysExtensionSetter() {
            _previous = Directory.GetFiles(FileUtils.GetReplaysDirectory());
        }

        [CanBeNull]
        public static ReplaysExtensionSetter OnlyNewIfEnabled() {
            return SettingsHolder.Drive.AutoAddReplaysExtension ? new ReplaysExtensionSetter() : null;
        }

        private static IEnumerable<string> GetWithoutExtension() {
            try {
                return Directory.GetFiles(FileUtils.GetReplaysDirectory())
                                .Where(file => !file.EndsWith(ReplayObject.ReplayExtension) && !string.Equals(Path.GetFileName(file), "cr",
                                        StringComparison.OrdinalIgnoreCase));
            } catch (Exception e) {
                Logging.Error("[REPLAYSEXTENSIONSETTER] Can't get files without extension: " + e);
                return new string[0];
            }
        } 

        public void Dispose() {
            try {
                foreach (var file in GetWithoutExtension().Where(x => !_previous.Contains(x))) {
                    File.Move(file, file + ReplayObject.ReplayExtension);
                }
            } catch (Exception e) {
                Logging.Error("[REPLAYSEXTENSIONSETTER] Can't rename new: " + e);
            }
        }

        public static void RenameAll() {
            try {
                foreach (var file in GetWithoutExtension()) {
                    File.Move(file, file + ReplayObject.ReplayExtension);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t rename replays", "Make sure none of those files are busy at the moment.", e);
            }
        }

        public static bool HasWithoutExtension() {
            return GetWithoutExtension().Any();
        }

        public static bool HasWithExtension() {
            try {
                return Directory.GetFiles(FileUtils.GetReplaysDirectory()).Any(file => file.EndsWith(ReplayObject.ReplayExtension));
            } catch (Exception e) {
                Logging.Error("[REPLAYSEXTENSIONSETTER] Can't get files with extension: " + e);
                return false;
            }
        }
    }
}