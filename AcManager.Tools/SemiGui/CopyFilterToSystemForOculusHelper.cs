using System;
using System.IO;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.SemiGui {
    public class CopyFilterToSystemForOculusHelper : Game.AdditionalProperties, IDisposable {
        private static string Destination => Path.Combine(AcRootDirectory.Instance.RequireValue, @"system", @"cfg", @"ppfilters", @"default.ini");

        private static string Backup => Path.Combine(AcRootDirectory.Instance.RequireValue, @"system", @"cfg", @"ppfilters", @"default.ini~cm_bak");

        public static void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            try {
                var backup = Backup;
                if (File.Exists(backup)) {
                    var destination = Destination;

                    if (File.Exists(destination)) {
                        File.Delete(destination);
                    }

                    File.Move(backup, destination);
                }
            } catch (Exception e) {
                Logging.Warning("[CopyFilterToSystemForOculusHelper] Revert(): " + e);
            }
        }

        public override IDisposable Set() {
            var selectedFilter = AcSettingsHolder.Video.PostProcessingFilter;
            if (string.IsNullOrEmpty(selectedFilter) || string.Equals(selectedFilter, @"default", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            var source = Path.Combine(AcRootDirectory.Instance.RequireValue, @"system", @"cfg", @"ppfilters", selectedFilter +  @".ini");
            if (!File.Exists(source)) return null;

            try {
                var backup = Backup;
                if (File.Exists(backup)) {
                    File.Delete(backup);
                }

                var destination = Destination;
                if (File.Exists(destination)) {
                    File.Move(destination, backup);
                }

                FileUtils.Hardlink(source, destination);
            } catch (Exception e) {
                Logging.Warning("[CopyFilterToSystemForOculusHelper] Set(): " + e);
            }

            return this;
        }

        public void Dispose() {
            Revert();
        }
    }
}